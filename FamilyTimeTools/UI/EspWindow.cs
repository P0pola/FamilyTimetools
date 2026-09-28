using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Panels;
using UniverseLib.UI.Widgets;

namespace FamilyTimeTools.UI
{
    internal sealed class EspWindow : PanelBase
    {
        private List<GameObject> _pages;
        private List<ButtonRef> _nav;
        private List<Action> _refresh;
        private List<Text> _targetRows;
        private List<ButtonRef> _styles;
        private Text _status, _counts, _targetSummary;
        private int _page;
        private float _nextRefresh;
        private EspMod Mod => EspMod.Instance;

        public override string Name => "FamilyTime Tools  /  控制中心";
        public override int MinWidth => 660;
        public override int MinHeight => 460;
        public override Vector2 DefaultAnchorMin => new Vector2(0.08f, 0.12f);
        public override Vector2 DefaultAnchorMax => new Vector2(0.72f, 0.88f);
        public override bool CanDragAndResize => true;

        internal EspWindow(UIBase owner) : base(owner)
        {
            foreach (Text text in UIRoot.GetComponentsInChildren<Text>(true)) text.font = Mod.UiFont;
        }

        protected override void OnClosePanelClicked()
        {
            Mod.SetWindowVisible(false);
        }

        protected override void ConstructPanelContent()
        {
            // PanelBase invokes this override from its constructor, before derived field initializers run.
            _pages = new List<GameObject>();
            _nav = new List<ButtonRef>();
            _refresh = new List<Action>();
            _targetRows = new List<Text>();
            _styles = new List<ButtonRef>();
            ContentRoot.GetComponent<Image>().color = UiControls.Background;
            TitleBar.GetComponent<Image>().color = UiControls.Surface;
            var header = UiControls.Horizontal(ContentRoot, "Header", 42);
            _status = UiControls.Label(header, "", 30);
            UiControls.Button(header, "保存配置", Mod.PersistSettings, 92);

            var body = UIFactory.CreateHorizontalGroup(ContentRoot, "Body", false, true, true, true,
                8, new Vector4(4, 4, 4, 4), UiControls.Background);
            UIFactory.SetLayoutElement(body, flexibleWidth: 1, flexibleHeight: 1, minHeight: 250);
            var sidebar = UiControls.Vertical(body, "Navigation", 8, 6);
            UIFactory.SetLayoutElement(sidebar, minWidth: 124, preferredWidth: 124, flexibleWidth: 0, flexibleHeight: 1);
            var pageHost = UiControls.Vertical(body, "Pages", 0, 0);
            UIFactory.SetLayoutElement(pageHost, minWidth: 420, flexibleWidth: 1, flexibleHeight: 1);

            string[] titles = { "总览", "部件开关", "目标筛选", "绘制样式", "目标列表", "作弊功能", "生成实体", "关于 / 配置" };
            for (int i = 0; i < titles.Length; i++)
            {
                int index = i;
                _nav.Add(UiControls.Button(sidebar, titles[i], () => SelectPage(index)));
                GameObject content;
                AutoSliderScrollbar scrollbar;
                var page = UIFactory.CreateScrollView(pageHost, "Page_" + i, out content, out scrollbar, UiControls.Background);
                UIFactory.SetLayoutElement(page, minHeight: 180, flexibleHeight: 1, flexibleWidth: 1);
                UIFactory.SetLayoutGroup<VerticalLayoutGroup>(content, forceWidth: true, forceHeight: false,
                    childControlWidth: true, childControlHeight: true, spacing: 8,
                    padTop: 12, padBottom: 12, padLeft: 12, padRight: 12);
                _pages.Add(page);
                switch (i)
                {
                    case 0: BuildOverview(content); break;
                    case 1: BuildParts(content); break;
                    case 2: BuildFilters(content); break;
                    case 3: BuildStyle(content); break;
                    case 4: BuildTargets(content); break;
                    case 5: BuildCheats(content); break;
                    case 6: BuildSpawn(content); break;
                    case 7: BuildAbout(content); break;
                }
            }
            UiControls.Label(ContentRoot, "F2：窗口开关    标题栏拖动 / 窗口边缘缩放", 24, 12, true);
            SelectPage(0);
        }

        private void SelectPage(int index)
        {
            _page = index;
            for (int i = 0; i < _pages.Count; i++)
            {
                _pages[i].SetActive(i == index);
                UiControls.Paint(_nav[i], i == index ? UiControls.Accent : UiControls.Surface);
            }
            _nextRefresh = 0f;
        }

        private void Heading(GameObject parent, string title, string detail)
        {
            UiControls.Label(parent, title, 32, 20);
            UiControls.Label(parent, detail, 40, 12, true);
        }

        private void Toggle(GameObject parent, string title, string detail, MelonPreferences_Entry<bool> entry)
        {
            UiControls.Toggle(parent, title, detail, () => entry.Value, value => entry.Value = value, _refresh);
        }

        private void BuildOverview(GameObject content)
        {
            Heading(content, "世界目标总览", "配置即时生效；修改后自动保存。窗口关闭不会关闭 ESP。");
            Toggle(content, "ESP 总开关", "控制扫描与全部目标绘制", Mod._enabled);
            _counts = UiControls.Label(content, "", 116);
            UiControls.Slider(content, "最大距离（米）", 20, 500, () => Mod._maxDistance.Value,
                value => Mod._maxDistance.Value = value, "F0", false, _refresh);
            UiControls.Slider(content, "最多目标", 10, 800, () => Mod._maxTargets.Value,
                value => Mod._maxTargets.Value = (int)value, "F0", true, _refresh);
            UiControls.Slider(content, "扫描间隔（秒）", 0.05f, 1f, () => Mod._scanInterval.Value,
                value => Mod._scanInterval.Value = value, "F2", false, _refresh);
            UiControls.Button(content, "立即重新扫描", () => Mod.SettingsChanged());
        }

        private void BuildParts(GameObject content)
        {
            Heading(content, "绘制部件", "各部件独立启停；只影响显示，不改变游戏实体。");
            Toggle(content, "框体", "按绘制样式标记目标轮廓", Mod._showBoxes);
            Toggle(content, "名称标签", "显示目标类别或实体名称", Mod._showName);
            Toggle(content, "距离标签", "显示目标与主摄像机的距离", Mod._showDistance);
            Toggle(content, "目标连线", "从屏幕底部中心连接到目标", Mod._showTracers);
            Toggle(content, "中心点", "在目标位置绘制小标记", Mod._showCenter);
            Toggle(content, "屏外指示", "在屏幕边缘标记视野外目标", Mod._offscreen);
        }

        private void BuildFilters(GameObject content)
        {
            Heading(content, "目标类别筛选", "“其他”与“物品”各自独立控制，互不影响。");
            var actions = UiControls.Horizontal(content, "FilterActions", 44);
            UiControls.Button(actions, "全部启用", () => Mod.SetAllCategories(true));
            UiControls.Button(actions, "全部禁用", () => Mod.SetAllCategories(false));
            foreach (EntityKind kind in EspMod.Kinds)
                Toggle(content, kind.Name, kind.IsItem ? "可搬运物品" : "GAT 实体模板", Mod._kindFilter[kind]);
        }

        private void BuildStyle(GameObject content)
        {
            Heading(content, "绘制样式", "类别配色或距离渐变；框体、连线、标签互不依赖。");
            var row = UiControls.Horizontal(content, "BoxStyles", 44);
            string[] names = { "方框", "四角", "填充", "框 + 角" };
            for (int i = 0; i < names.Length; i++)
            {
                int style = i;
                _styles.Add(UiControls.Button(row, names[i], () =>
                {
                    Mod._style.Value = style;
                    Mod.SettingsChanged();
                }));
            }
            Toggle(content, "距离渐变", "近处绿色，远处红色", Mod._rainbow);
            Toggle(content, "仅屏幕内目标", "开启时强制不绘制屏外指示", Mod._onlyOnScreen);
            UiControls.Slider(content, "框体缩放", 0.3f, 3f, () => Mod._scale.Value,
                value => Mod._scale.Value = value, "F2", false, _refresh);
            UiControls.Slider(content, "线宽（像素）", 1f, 4f, () => Mod._thickness.Value,
                value => Mod._thickness.Value = value, "F1", false, _refresh);
        }

        private void BuildTargets(GameObject content)
        {
            Heading(content, "当前目标列表", "显示已筛选、距离内的目标，按距离由近到远排序（最多 800 条）。");
            _targetSummary = UiControls.Label(content, "", 28, 13, true);
            // Rows are created on demand and reused; no allocation per scan or per frame.
            UiControls.Button(content, "刷新扫描", () => Mod.SettingsChanged());
            _targetContent = content;
        }

        private GameObject _targetContent;

        private void BuildCheats(GameObject content)
        {
            Heading(content, "建造作弊", "作用于锤子建造与村民自动施工；只影响材料校验与放置校验，不改动建筑本身。");
            Toggle(content, "免费建造", "建造不消耗材料，点击蓝图即可建成", Mod._freeBuild);
            UiControls.Label(content, "开启后放下蓝图，用锤子对准蓝图点击一次即可完成建造。", 44, 13, true);
            Toggle(content, "地板堆叠", "地板可以贴着下一块地板往上摸，按实际厚度紧贴", Mod._stackFloor);
            UiControls.Label(content, "只影响地板：瞄准已建成地板的顶面时，新地板底面直接贴住它；X / Z 仍然按最小格子对齐，其余建筑不受影响。", 44, 13, true);
            UiControls.Button(content, "导出实体模板列表", () => Mod.LoggerInstance.Msg("模板列表已导出：" + TemplateDump.Export()));
        }

        private int _spawnTemplate = -1;
        private Text _spawnInfo;

        private void BuildSpawn(GameObject content)
        {
            Heading(content, "生成实体", "调用游戏自身的 GAT.GAT.CreateInstances，在当前角色位置生成实体。");
            _spawnInfo = UiControls.Label(content, "模板列表：未加载（进入世界后点“刷新模板列表”）", 44, 13, true);
            UiControls.Button(content, "刷新模板列表", RefreshTemplateList);
            _spawnDropdown = content;
        }

        private GameObject _spawnDropdown;
        private int _spawnCount = 1;
        private float _spawnRadius = 2f;

        private void RefreshTemplateList()
        {
            if (!Spawner.RefreshTemplates())
            {
                _spawnInfo.text = "模板列表：读取失败（需要先进入游戏世界）";
                return;
            }
            _spawnTemplate = 0;
            _spawnInfo.text = "模板列表：" + Spawner.Templates.Count + " 个实体模板";
            BuildSpawnControls();
        }

        private void BuildSpawnControls()
        {
            if (_spawnPanel != null) UnityEngine.Object.Destroy(_spawnPanel);
            foreach (Action stale in _spawnRefresh) _refresh.Remove(stale);
            _spawnRefresh.Clear();
            _spawnPanel = UiControls.Vertical(_spawnDropdown, "SpawnControls", 6, 6);
            UiControls.Dropdown(_spawnPanel, "实体模板", Spawner.Names.ToArray(), _spawnTemplate,
                index => _spawnTemplate = index, _spawnRefresh, () => _spawnTemplate);
            UiControls.Input(_spawnPanel, "生成数量", "1", _spawnCount.ToString(), value =>
            {
                int parsed;
                if (int.TryParse(value, out parsed) && parsed > 0) _spawnCount = Mathf.Clamp(parsed, 1, 200);
            });
            UiControls.Input(_spawnPanel, "散布半径（米）", "2", _spawnRadius.ToString("F1"), value =>
            {
                float parsed;
                if (float.TryParse(value, out parsed)) _spawnRadius = Mathf.Clamp(parsed, 0f, 50f);
            });
            _refresh.AddRange(_spawnRefresh);
            UiControls.Button(_spawnPanel, "在当前角色位置生成", DoSpawn);
        }

        private GameObject _spawnPanel;
        private readonly List<Action> _spawnRefresh = new List<Action>();

        private void DoSpawn()
        {
            if (Spawner.Templates.Count == 0)
            {
                _spawnInfo.text = "模板列表：未加载，请先点“刷新模板列表”";
                return;
            }
            int n = Spawner.Spawn(_spawnTemplate, _spawnCount, _spawnRadius);
            _spawnInfo.text = n > 0
                ? "已生成 " + n + " 个：" + Spawner.Names[_spawnTemplate]
                : "生成失败（模板索引无效或未进入世界）";
        }

        private void BuildAbout(GameObject content)
        {
            Heading(content, "FamilyTime Tools 2.1.0", "UniverseLib.Mono 1.6.2 / MelonLoader / Unity Mono");
            UiControls.Label(content,
                "窗口：UniverseLib PanelBase + UIFactory + UGUI\n" +
                "实体：GAT.World.gat / fakeGATManager\n" +
                "物品：rigidTransformManager\n" +
                "目标叠加：IMGUI（仅绘制，无自绘菜单）\n" +
                "配置：沿用 FamilyTimeESP 分类及原配置键", 128);
            UiControls.Button(content, "打开配置目录", () => Process.Start(new ProcessStartInfo
            {
                FileName = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "UserData"),
                UseShellExecute = true
            }));
            UiControls.Button(content, "立即保存配置", Mod.PersistSettings);
            UiControls.Button(content, "恢复默认设置", () => { Mod.ResetSettings(); _nextRefresh = 0f; });
            UiControls.Button(content, "重置窗口位置与大小", SetDefaultSizeAndPosition);
            UiControls.Button(content, "关闭窗口（ESP 继续运行）", () => Mod.SetWindowVisible(false));
        }

        internal void Tick()
        {
            if (Time.realtimeSinceStartup < _nextRefresh) return;
            _nextRefresh = Time.realtimeSinceStartup + 0.15f;
            foreach (Action action in _refresh) action();
            _status.text = "ESP " + (Mod._enabled.Value ? "开启" : "关闭") + "  |  当前目标 " + Mod._targets.Count;
            for (int i = 0; i < _styles.Count; i++)
                UiControls.Paint(_styles[i], Mod._style.Value == i ? UiControls.Accent : UiControls.Surface);
            if (_page == 0)
            {
                int[] counts = new int[EspMod.Kinds.Length];
                foreach (EspTarget target in Mod._targets)
                    counts[System.Array.IndexOf(EspMod.Kinds, target.Kind)]++;
                var builder = new StringBuilder("当前目标：" + Mod._targets.Count + "\n");
                for (int i = 0; i < counts.Length; i++)
                {
                    builder.Append(EspMod.Kinds[i].Name).Append("  ").Append(counts[i]).Append("     ");
                    if (i % 3 == 2) builder.AppendLine();
                }
                _counts.text = builder.ToString();
            }
            if (_page == 4)
            {
                int count = Mod._targets.Count;
                _targetSummary.text = "已显示 " + count + " 个目标 / 上限 " + Mod._maxTargets.Value;
                while (_targetRows.Count < count)
                    _targetRows.Add(UiControls.Label(_targetContent, "", 28));
                for (int i = 0; i < _targetRows.Count; i++)
                {
                    bool active = i < count;
                    _targetRows[i].gameObject.SetActive(active);
                    if (!active) continue;
                    EspTarget target = Mod._targets[i];
                    _targetRows[i].text = (i + 1).ToString("D3") + "   " + target.Label + "   " + target.Distance.ToString("F1") + " m";
                    _targetRows[i].color = target.Kind.Color;
                }
            }
        }
    }
}
