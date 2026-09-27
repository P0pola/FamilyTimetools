using System.Collections.Generic;
using System.IO;
using System.Text;
using GAT;
using UnityEngine;

namespace FamilyTimeESP
{
    // 游戏 SDK 自带权威的实体模板表：
    //   GAT.GAT.GetTemplateEntries() -> IReadOnlyList<GAT.GATTemplateEntry>
    //   entry.template.name / entry.template.faction / entry.count
    // 这里直接把它导出来，不再靠猜名字。
    internal static class TemplateDump
    {
        internal static string Export()
        {
            var gat = World.gat;
            var sb = new StringBuilder();
            if (gat == null)
            {
                sb.AppendLine("gat == null（尚未进入游戏世界）");
                return Write(sb.ToString());
            }

            IReadOnlyList<GATTemplateEntry> entries = gat.GetTemplateEntries();
            sb.AppendLine("模板总数: " + (entries == null ? 0 : entries.Count));
            sb.AppendLine("活动实例总数: " + gat.instanceCount);
            sb.AppendLine();
            sb.AppendLine("索引  实时数  范围数  阵营      模板名");
            sb.AppendLine("----  ------  ------  --------  ------");

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    GATTemplateEntry e = entries[i];
                    Template tpl = e == null ? null : e.template;
                    string name = tpl == null ? "(空模板)" : tpl.name;
                    string faction = tpl == null ? "?" : tpl.faction.ToString();
                    int live = gat.GetAllHandlesByTemplate(i).Length;
                    int range = e == null ? 0 : e.count;
                    sb.AppendLine(string.Format("{0,4}  {1,6}  {2,6}  {3,-8}  {4}", i, live, range, faction, name));
                }
            }
            return Write(sb.ToString());
        }

        private static string Write(string content)
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "UserData");
            string file = Path.Combine(dir, "FamilyTimeESP_templates.txt");
            File.WriteAllText(file, content, new UTF8Encoding(false));
            return file;
        }
    }
}
