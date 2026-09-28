using System.Collections.Generic;
using GAT;
using UnityEngine;

namespace FamilyTimeTools
{
    // 实体类别只在这里定义一次：名字、匹配关键字、颜色、默认开关都在同一行。
    // 新增一类实体 = 加一行记录，匹配/命名/配色/筛选 UI 全部自动生效。
    internal sealed class EntityKind
    {
        public readonly string Key;         // 配置键后缀，保持与旧版 Cat_Xxx 一致
        public readonly string Name;        // 界面与标签上显示的中文名
        public readonly string[] Match;     // 模板名包含的关键字（全部小写）
        public readonly Color Color;
        public readonly bool DefaultEnabled;
        public readonly bool IsItem;        // 走 rigidTransformManager 而不是 GAT

        public EntityKind(string key, string name, Color color, bool defaultEnabled = true, bool isItem = false, params string[] match)
        {
            Key = key;
            Name = name;
            Color = color;
            DefaultEnabled = defaultEnabled;
            IsItem = isItem;
            Match = match ?? new string[0];
        }
    }

    internal sealed class EspTarget
    {
        public Vector3 World;
        public string Label;
        public Color Color;
        public float Distance;
        public EntityKind Kind;
    }

    public sealed partial class EspMod
    {
        internal readonly List<EspTarget> _targets = new List<EspTarget>();

        // 匹配按数组顺序取第一个命中的关键字，所以更具体的关键字（小鸡/狼女…）要排在更宽泛的（鸡/狼）前面。
        // Key 写入配置时拼成旧版的 "Cat_Xxx"，老配置文件继续有效。
        internal static readonly EntityKind[] Kinds =
        {
            new EntityKind("Other", "其他",   new Color(0.40f, 1.00f, 0.60f), false),
            new EntityKind("Item", "物品",   new Color(0.50f, 0.85f, 1.00f), false, true),
            new EntityKind("Chick", "小鸡",   new Color(1.00f, 1.00f, 0.65f), match: "baby_chick"),
            new EntityKind("Rooster", "公鸡",   new Color(1.00f, 0.45f, 0.35f), match: "rooster"),
            new EntityKind("Hen", "母鸡",   new Color(1.00f, 0.82f, 0.45f), match: "hen"),
            new EntityKind("Chicken", "鸡",     new Color(1.00f, 0.95f, 0.40f), match: "chicken"),
            new EntityKind("Piglet", "猪崽",   new Color(1.00f, 0.78f, 0.85f), match: "piglet"),
            new EntityKind("Pig", "猪",     new Color(1.00f, 0.65f, 0.75f), match: "pig"),
            new EntityKind("Boar", "野猪",   new Color(1.00f, 0.55f, 0.20f), match: "boar"),
            new EntityKind("WolfGirl", "狼女",   new Color(0.75f, 0.55f, 1.00f), match: new string[] { "wolf_girl", "wolfgirl" }),
            new EntityKind("WolfMama", "狼妈妈", new Color(0.90f, 0.35f, 0.85f), match: new string[] { "wolf_mama", "wolfmama" }),
            new EntityKind("Wolf", "狼",     new Color(0.95f, 0.30f, 0.30f), match:new string[] { "wolf" }),
            new EntityKind("Player", "玩家",   new Color(0.40f, 1.00f, 0.55f), match: new string[] { "fake_player", "player" }),
            //new EntityKind("Deer", "鹿",     new Color(0.90f, 0.75f, 0.50f), match: "deer"),
            //new EntityKind("Rabbit", "兔子",   new Color(0.95f, 0.95f, 0.95f), match: "rabbit"),
        };

        internal static readonly EntityKind OtherKind = Kinds[0];
        internal static readonly EntityKind ItemKind = Kinds[1];

        private float _scanTimer;

        private void ScanWorld()
        {
            _targets.Clear();
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 eye = cam.transform.position;

            ScanGatInstances(eye);
            ScanRigidTransforms(eye);

            if (_targets.Count > 1) _targets.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            int cap = _maxTargets.Value;
            if (_targets.Count > cap) _targets.RemoveRange(cap, _targets.Count - cap);
        }

        private void ScanGatInstances(Vector3 eye)
        {
            var gat = World.gat;
            if (gat == null) return;

            int count = gat.instanceCount;
            if (count <= 0) return;

            var fake = World.fakeGATManager;
            int limit = Mathf.Min(count, ushort.MaxValue + 1);
            float maxDist = _maxDistance.Value;

            for (int physical = 0; physical < limit; physical++)
            {
                Handle handle = gat.GetHandle((ushort)physical);
                if (handle.isNull || !gat.IsHandleValid(handle)) continue;

                Vector3 pos = Vector3.zero;
                bool hasPosition = fake != null && fake.TryGetPosition(handle, out pos);
                if (!hasPosition && !TryGetPositionFromMatrix(gat, handle, out pos))
                {
                    continue;
                }

                float dist = Vector3.Distance(pos, eye);
                if (dist > maxDist) continue;

                string raw;
                EntityKind kind = DescribeHandle(gat, handle, out raw);
                if (!PassFilter(kind)) continue;

                _targets.Add(new EspTarget
                {
                    World = pos,
                    Label = raw,
                    Kind = kind,
                    Color = kind.Color,
                    Distance = dist,
                });
            }
        }

        private static bool TryGetPositionFromMatrix(GAT.GAT gat, Handle handle, out Vector3 pos)
        {
            pos = Vector3.zero;
            var m = gat.GetInstanceMatrixImmediate(handle);
            if (!m.HasValue) return false;
            var mat = m.Value;
            pos = new Vector3(mat.m03, mat.m13, mat.m23);
            return true;
        }

        // 遍历整张表取第一个命中的关键字；命中就用类别名，未命中的有名字模板沿用原始名，其余归入“其他”。
        private static EntityKind DescribeHandle(GAT.GAT gat, Handle handle, out string label)
        {
            int idx = gat.GetTemplateIndexFromHandle(handle);
            var tpl = gat.GetTemplate(idx);
            if (tpl != null && !string.IsNullOrEmpty(tpl.name))
            {
                string name = tpl.name;
                string low = name.ToLowerInvariant();
                foreach (EntityKind kind in Kinds)
                {
                    if (Matches(kind, low))
                    {
                        label = kind.Name;
                        return kind;
                    }
                }
                label = name;
                return OtherKind;
            }
            label = OtherKind.Name;
            return OtherKind;
        }

        private static bool Matches(EntityKind kind, string lowerTemplateName)
        {
            for (int i = 0; i < kind.Match.Length; i++)
            {
                if (lowerTemplateName.Contains(kind.Match[i])) return true;
            }
            return false;
        }

        private void ScanRigidTransforms(Vector3 eye)
        {
            if (!PassFilter(ItemKind)) return;

            var mgr = World.rigidTransformManager;
            if (mgr == null) return;

            float maxDist = _maxDistance.Value;
            int n = mgr.Count;
            for (int i = 0; i < n; i++)
            {
                GAT.RigidTransform rigid;
                if (!mgr.TryGetManagedRigid(i, out rigid) || rigid == null) continue;

                Vector3 pos = rigid.position;

                float dist = Vector3.Distance(pos, eye);
                if (dist > maxDist) continue;

                string label = ItemKind.Name;
                var go = rigid.gameObject;
                if (go != null && !string.IsNullOrEmpty(go.name)) label = go.name;

                _targets.Add(new EspTarget
                {
                    World = pos,
                    Label = label,
                    Kind = ItemKind,
                    Color = ItemKind.Color,
                    Distance = dist,
                });
            }
        }

        private bool PassFilter(EntityKind kind)
        {
            return _kindFilter[kind].Value;
        }
    }
}
