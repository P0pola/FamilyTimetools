using System.Collections.Generic;
using GAT;
using UnityEngine;

namespace FamilyTimeTools
{
    // 实体生成用的是游戏 SDK 自带接口：
    //   GAT.GAT.CreateInstance / CreateInstances(Matrix4x4[], templateIndex, onCreated)
    // 游戏内部 AnimalHusbandry.SpawnOffspringForFemales 与
    // GATUtilities.CreateTemplateSpawnGroups 就是这么刷实体的，这里照抄同样用法。
    internal static class Spawner
    {
        internal static readonly List<Template> Templates = new List<Template>();
        internal static readonly List<string> Names = new List<string>();

        // 模板索引只在下世界后有效，每次进入世界后需要重新读取。
        internal static bool RefreshTemplates()
        {
            Templates.Clear();
            Names.Clear();
            var gat = World.gat;
            if (gat == null) return false;
            IReadOnlyList<GATTemplateEntry> entries = gat.GetTemplateEntries();
            if (entries == null) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                GATTemplateEntry e = entries[i];
                Template tpl = e == null ? null : e.template;
                if (tpl == null) continue;
                if (string.IsNullOrEmpty(tpl.name)) continue;
                Templates.Add(tpl);
                Names.Add(tpl.name);
            }
            return Templates.Count > 0;
        }

        // 在当前角色位置生成 count 个指定模板的实体，随机散开一点并随机朝向。
        internal static int Spawn(int templateListIndex, int count, float radius)
        {
            var gat = World.gat;
            if (gat == null) return 0;
            if (templateListIndex < 0 || templateListIndex >= Templates.Count) return 0;
            Template tpl = Templates[templateListIndex];
            int templateIndex = gat.GetTemplateIndex(tpl);
            if (templateIndex < 0) return 0;

            Vector3 origin = PlayerPosition();
            if (count < 1) count = 1;
            var matrices = new Matrix4x4[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = count == 1 ? Vector2.zero : Random.insideUnitCircle * radius;
                Vector3 pos = origin + new Vector3(offset.x, 0f, offset.y);
                Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                matrices[i] = Matrix4x4.TRS(pos, rot, Vector3.one);
            }
            gat.CreateInstances(matrices, templateIndex, null);
            return count;
        }

        // 优先取玩家所拥有实体的矩阵位置，其次取缓存的实例位置，最后退回摄像机。
        internal static Vector3 PlayerPosition()
        {
            var gat = World.gat;
            var pc = World.playerController;
            if (gat != null && pc != null)
            {
                Handle h = pc.PossessedHandle;
                if (!h.isNull)
                {
                    Matrix4x4? m = gat.GetInstanceMatrixImmediate(h);
                    if (m.HasValue) return m.Value.GetPosition();
                }
                if (pc.CachedInstancePosition != Vector3.zero) return pc.CachedInstancePosition;
            }
            var cam = Camera.main;
            return cam == null ? Vector3.zero : cam.transform.position;
        }
    }
}
