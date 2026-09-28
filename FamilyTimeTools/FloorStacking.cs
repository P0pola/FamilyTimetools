using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using GAT;
using HarmonyLib;
using UnityEngine;

namespace FamilyTimeTools
{
    // 地板垂直堆叠：只作用于地板，让新地板底面紧贴命中面，实现“地板贴着地板往上摞”。
    //
    // 游戏原本为什么堆不了：
    //   BlueprintTool.RequestNextPlaceable 取到命中点后，由 BuildPart.GetPlacementCell 四舍五入成整数格，
    //   再交给 GetPlacementMatrix 生成放置矩阵。地板是薄板，瞄着它的顶面再取整，Y 仍然落回同一格，
    //   于是 BuildPartManager.TryCreateBuildPart 对同 (模板 + 格子 + 朝向) 直接返回 false ——
    //   蓝图凭空消失，什么也建不成。而向上顺延一整格（1 米）又远高于地板厚度，缝隙明显。
    //
    // 这里的做法：堆叠模式下，若正在放地板、且瞄中的是已建成的另一块地板、且命中面朝上，
    //   就把新地板的高度直接算成“命中面高度 − 自身网格包围盒下沿”，即底面正好贴住命中面。
    //   X / Z 仍然按最小格子吸附，保持对齐，不产生半格。
    //
    // 连续高度绕不开 Cell 的整数存储（x 12 位 / y 8 位 / z 12 位），所以整数格只当存放键：
    //   先向上顺延到一个没被占用的格，再用差值等量的浮点偏移把矩阵平移拉回真正的高度。
    //   偏移只登记地板，其它建筑完全不受影响。
    //
    // 偏移随游戏存档一起保存：WorldSerializer 支持自定义段并按 order 排序加载，
    //   游戏自带的 build.parts 是 order 80，本模块的段排 79，
    //   于是读档时偏移表先就位，建筑创建时矩阵一次就算对。
    internal static class FloorStacking
    {
        // 落点键：模板实例 + 格子 + 朝向。BuildPart 是 ScriptableObject，按引用比较即可。
        internal readonly struct Key : IEquatable<Key>
        {
            private readonly BuildPart _part;
            private readonly int _x, _y, _z, _rot;

            internal Key(BuildPart part, Vector3Int cell, BuildPart.Rotation rot)
            {
                _part = part;
                _x = cell.x;
                _y = cell.y;
                _z = cell.z;
                _rot = (int)rot;
            }

            internal BuildPart Part => _part;
            internal Vector3Int Cell => new Vector3Int(_x, _y, _z);
            internal BuildPart.Rotation Rotation => (BuildPart.Rotation)_rot;

            public bool Equals(Key other)
            {
                return ReferenceEquals(_part, other._part)
                    && _x == other._x && _y == other._y && _z == other._z && _rot == other._rot;
            }

            public override bool Equals(object obj) => obj is Key other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _x * 73856093 ^ _y * 19349663 ^ _z * 83492791 ^ _rot * 131;
                    return hash ^ (_part == null ? 0 : RuntimeHelpers.GetHashCode(_part));
                }
            }
        }

        // 高度偏移只作用在 Y 上。
        private static readonly Dictionary<Key, float> Offsets = new Dictionary<Key, float>();
        private static readonly HashSet<Key> Parts = new HashSet<Key>();
        private static readonly HashSet<Key> Plans = new HashSet<Key>();

        // 最近一次 World.Raycast 的结果。BlueprintTool 取到命中后紧接着就调用 GetPlacementCell，
        // 两者在同一次调用栈里，记下这一帧的命中即可确定“瞄到的是什么”。
        private static World.HitInfo _hit;
        private static int _hitFrame = -1;

        // 只有 BlueprintTool.RequestNextPlaceable 内部发起的射线才算数。
        // 同一帧里其他系统（玩家手部、其他工具）也会射线，
        // 不加这层作用域的话会把本模块记下的命中冲掉，导致时灵时不灵。
        private static bool _inTool;

        // 读档期间 BuildPartManager.LoadFrom 会先删光旧零件；那会顺带清掉刚装好的偏移，
        // 所以要认读档状态。WorldSerializer 自己就有 isLoadingInProgress，覆盖整段读档，最可靠。
        private static WorldSerializer _serializer;

        private static bool Loading => _serializer != null && _serializer.isLoadingInProgress;

        private const string SectionId = "build.floor-stack";
        private const int SectionOrder = 79;
        private const int EntrySize = 15;

        private static MethodInfo _currentBuildPartGetter;

        internal static bool Enabled => EspMod.Instance != null && EspMod.Instance._stackFloor.Value;

        internal static void Reset()
        {
            Offsets.Clear();
            Parts.Clear();
            Plans.Clear();
            _hitFrame = -1;
            _inTool = false;
            _serializer = null;
        }

        private static bool Occupied(Key key) => Parts.Contains(key) || Plans.Contains(key);

        internal static bool TryGetPart(int templateIndex, out BuildPart part)
        {
            part = null;
            var manager = World.buildPartManager;
            if (manager == null) return false;
            IReadOnlyList<BuildPart> templates = manager.BuildPartTemplates;
            if (templates == null || templateIndex < 0 || templateIndex >= templates.Count) return false;
            part = templates[templateIndex];
            return part != null;
        }

        // 地板判定靠名字：本作共 15 个建造零件，其中地板是
        //   bp_hay_floor（干草地板）与 bp_wood_planks01（木板地板）。
        // 注意“木板”名字里没有 floor，所以必须连 plank 一起匹配。
        private static readonly string[] FloorKeywords = { "floor", "plank", "deck", "platform", "slab", "tile", "board" };

        internal static bool IsFloor(BuildPart part)
        {
            if (part == null) return false;
            return Matches(part.displayName) || Matches(part.name);
        }

        private static bool Matches(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < FloorKeywords.Length; i++)
            {
                if (text.IndexOf(FloorKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        internal static BuildPart.Rotation RotationOf(Quaternion rotation)
        {
            return (BuildPart.Rotation)(Mathf.RoundToInt(rotation.eulerAngles.y / 90f) & 3);
        }

        // ── 工具作用域：标记 RequestNextPlaceable 的进出 ─────────────────────────
        // 同一帧里其他系统（玩家手部、其他工具）也会发射线，
        // 不加这层作用域的话会把本模块记下的命中冲掉。
        [HarmonyPatch(typeof(BlueprintTool), "RequestNextPlaceable")]
        internal static class ToolScopePatch
        {
            private static void Prefix() => _inTool = true;

            private static void Postfix() => _inTool = false;
        }

        // ── 命中记录：只用来判断“瞄中的是不是已建成的地板” ──────────────────────────────
        [HarmonyPatch(typeof(World), nameof(World.Raycast))]
        internal static class RaycastPatch
        {
            private static void Postfix(ref World.HitInfo hitInfo, bool __result)
            {
                if (!_inTool || !__result) return;
                _hit = hitInfo;
                _hitFrame = Time.frameCount;
            }
        }

        // ── 放置解析：地板贴住命中面，整数格只作为存放键 ────────────────────────────────
        [HarmonyPatch(typeof(BuildPart), nameof(BuildPart.GetPlacementCell))]
        internal static class PlacementCellPatch
        {
            private static void Postfix(BuildPart __instance, BuildPart.Rotation rotation, ref Vector3Int __result)
            {
                if (!Enabled || !IsFloor(__instance)) return;

                // 先判断“是不是在叠已建成地板的顶面”，能确定就把贴面高度算出来。
                float targetY = float.NaN;
                if (_hitFrame == Time.frameCount && _hit.normal.y > 0.5f)
                {
                    var manager = World.buildPartManager;
                    int templateIndex;
                    Vector3Int hitCell;
                    BuildPart.Rotation hitRotation;
                    if (manager != null && manager.TryGetBuildPart(_hit, out templateIndex, out hitCell, out hitRotation))
                    {
                        BuildPart hitPart;
                        if (TryGetPart(templateIndex, out hitPart) && IsFloor(hitPart))
                        {
                            // 被叠的那块地板顶面（用它自己的放置矩阵，含它自身的偏移）。
                            Bounds hitBounds = hitPart.GetCombinedMeshBounds();
                            float top = hitPart.GetPlacementMatrix(hitCell, hitRotation).GetPosition().y + hitBounds.max.y;
                            // 新地板底面贴住顶面：矩阵平移 = 顶面 − 包围盒下沿。
                            targetY = top - __instance.GetCombinedMeshBounds().min.y;
                        }
                    }
                }

                // 整数格只当存放键：只要该键已被占用就向上顺延，
                // 否则 BuildPartManager.TryCreateBuildPart 会直接返回 false，蓝图静默消失。
                // 这一段必须与上面是否识别出目标地板无关，否则一次丢帧就会退回原格子。
                int guard = 0;
                while (Occupied(new Key(__instance, __result, rotation)) && guard++ < 64)
                    __result.y += 1;

                if (float.IsNaN(targetY)) return;

                // 矩阵平移量的 Y = cell.y + pivot.y - (rot * pivot).y，偏移要相对这个基准算。
                Vector3 pivot = __instance.GetPlacementPivot();
                Quaternion turn = Quaternion.Euler(0f, 90f * (int)rotation, 0f);
                float baseY = __result.y + pivot.y - (turn * pivot).y;

                Offsets[new Key(__instance, __result, rotation)] = targetY - baseY;
            }
        }

        // ── 放置矩阵：只给地板加回 Y 偏移 ───────────────────────────────────────────────
        [HarmonyPatch(typeof(BuildPart), nameof(BuildPart.GetPlacementMatrix), new Type[] { typeof(Vector3Int), typeof(Quaternion) })]
        internal static class PlacementMatrixPatch
        {
            private static void Postfix(BuildPart __instance, Vector3Int gridPosition, ref Matrix4x4 __result)
            {
                if (Offsets.Count == 0) return;
                float offset;
                if (!Offsets.TryGetValue(new Key(__instance, gridPosition, RotationOf(__result.rotation)), out offset)) return;
                Vector3 position = __result.GetPosition();
                __result = Matrix4x4.TRS(new Vector3(position.x, position.y + offset, position.z), __result.rotation, Vector3.one);
            }
        }

        // ── 重叠校验：只在“当前手持的是地板”时放行，其余建筑照旧互相挡住 ─────────────────
        // CanPlaceAt 是“能不能放”的唯一收口（同步与异步碰撞回调都走它），
        // 又能从 __instance 直接取到当前手持零件，因此在这里一刀切。
        private static BuildPart CurrentPart(BlueprintTool tool)
        {
            if (_currentBuildPartGetter == null)
                _currentBuildPartGetter = AccessTools.PropertyGetter(typeof(BlueprintTool), "currentBuildPart");
            if (_currentBuildPartGetter == null) return null;
            return (BuildPart)_currentBuildPartGetter.Invoke(tool, null);
        }

        [HarmonyPatch(typeof(BlueprintTool), "CanPlaceAt")]
        internal static class CanPlaceAtPatch
        {
            private static bool Prefix(BlueprintTool __instance, Matrix4x4 matrix, ref bool __result)
            {
                if (!Enabled) return true;
                BuildPart part = CurrentPart(__instance);
                if (!IsFloor(part)) return true;
                // 地板：跳过固体建筑与蓝图的重叠校验，但保留“不能埋进地形”。
                __result = World.heightfield == null
                    || World.heightfield.IsAboveSurface(part.GetCombinedMeshBounds(), matrix, 0.5f);
                return false;
            }
        }

        [HarmonyPatch(typeof(BuildPlanManager), nameof(BuildPlanManager.OverlapsActiveBuildPlans))]
        internal static class PlanOverlapPatch
        {
            private static bool Prefix(BuildPart candidate, ref bool __result)
            {
                if (!Enabled || !IsFloor(candidate)) return true;
                __result = false;
                return false;
            }
        }

        // ── 存/读档：偏移表挂在游戏自带世界存档的独立段里 ────────────────────────────────
        private static IEnumerator SaveSection(MemoryStream stream)
        {
            var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write((byte)70);
            writer.Write((byte)83);
            writer.Write((byte)80);

            // 只存真正建成的零件：预览阶段会预写偏移，那些不能落盘。
            var manager = World.buildPartManager;
            var entries = new List<KeyValuePair<Key, float>>();
            foreach (KeyValuePair<Key, float> pair in Offsets)
            {
                if (!Parts.Contains(pair.Key)) continue;
                if (manager == null || manager.GetTemplateIndex(pair.Key.Part) < 0) continue;
                entries.Add(pair);
            }
            writer.Write(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                Key key = entries[i].Key;
                Vector3Int cell = key.Cell;
                writer.Write(manager.GetTemplateIndex(key.Part));
                writer.Write((short)cell.x);
                writer.Write((short)cell.y);
                writer.Write((short)cell.z);
                writer.Write((byte)key.Rotation);
                writer.Write(entries[i].Value);
            }
            writer.Flush();
            yield break;
        }

        private static IEnumerator LoadSection(MemoryStream stream)
        {
            Offsets.Clear();
            Plans.Clear();
            if (stream.Length < 7) yield break;

            var reader = new BinaryReader(stream, Encoding.UTF8, true);
            if (reader.ReadByte() != 70 || reader.ReadByte() != 83 || reader.ReadByte() != 80) yield break;
            int count = reader.ReadInt32();
            if (count < 0 || count > (stream.Length - stream.Position) / EntrySize) yield break;

            for (int i = 0; i < count; i++)
            {
                int templateIndex = reader.ReadInt32();
                int x = reader.ReadInt16();
                int y = reader.ReadInt16();
                int z = reader.ReadInt16();
                var rotation = (BuildPart.Rotation)reader.ReadByte();
                float offset = reader.ReadSingle();

                BuildPart part;
                if (!TryGetPart(templateIndex, out part)) continue;
                Offsets[new Key(part, new Vector3Int(x, y, z), rotation)] = offset;
            }
            yield break;
        }

        [HarmonyPatch(typeof(BuildPartManager), nameof(BuildPartManager.RegisterWorldSerialization))]
        internal static class RegisterSectionPatch
        {
            private static void Postfix(BuildPartManager __instance, WorldSerializer serializer)
            {
                if (serializer == null) return;
                _serializer = serializer;
                serializer.Register(SectionId, __instance, SectionOrder, SaveSection, LoadSection);
            }
        }

        // 旧存档没有本模块的段，LoadSectionsRoutine 会直接跳过，
        // 偏移表就不会被清。在读档起点先清一次，避免上一局的偏移污染新世界。
        [HarmonyPatch(typeof(WorldSerializer), nameof(WorldSerializer.Load))]
        internal static class LoadStartPatch
        {
            private static void Prefix(WorldSerializer __instance)
            {
                _serializer = __instance;
                Offsets.Clear();
                Plans.Clear();
            }
        }

        // ── 占用登记：成品走 TryCreateBuildPart，蓝图走 CreateBuildPlan，各自独立记账 ────
        [HarmonyPatch(typeof(BuildPartManager), "TryCreateBuildPart")]
        internal static class PartCreatedPatch
        {
            private static void Postfix(int buildPartTemplateIndex, Vector3Int gridPos, BuildPart.Rotation rotation, bool transient, ref bool __result)
            {
                if (!__result || transient) return;
                BuildPart part;
                if (!TryGetPart(buildPartTemplateIndex, out part)) return;
                Parts.Add(new Key(part, gridPos, rotation));
            }
        }

        [HarmonyPatch(typeof(BuildPartManager), nameof(BuildPartManager.RemoveBuildPart))]
        internal static class PartRemovedPatch
        {
            private static void Prefix(int buildPartTemplateIndex, Vector3Int gridPos, BuildPart.Rotation rotation)
            {
                BuildPart part;
                if (!TryGetPart(buildPartTemplateIndex, out part)) return;
                Key key = new Key(part, gridPos, rotation);
                Parts.Remove(key);
                if (Loading) return; // 读档会整段重建，偏移必须留给新建的同键零件
                Offsets.Remove(key);
            }
        }

        [HarmonyPatch(typeof(BuildPlanManager), nameof(BuildPlanManager.CreateBuildPlan))]
        internal static class PlanCreatedPatch
        {
            private static void Postfix(int templateIndex, Vector3Int gridPosition, BuildPart.Rotation rotation, ref bool __result)
            {
                if (!__result) return;
                BuildPart part;
                if (!TryGetPart(templateIndex, out part)) return;
                Plans.Add(new Key(part, gridPosition, rotation));
            }
        }

        [HarmonyPatch(typeof(BuildPlanManager), nameof(BuildPlanManager.RemoveBuildPlan))]
        internal static class PlanRemovedPatch
        {
            private static void Prefix(BuildPlanManager.BuildPlan buildPlan)
            {
                if (buildPlan == null) return;
                BuildPart part;
                if (!TryGetPart(buildPlan.templateIndex, out part)) return;
                Plans.Remove(new Key(part, buildPlan.gridPosition, buildPlan.rotation));
            }
        }
    }
}
