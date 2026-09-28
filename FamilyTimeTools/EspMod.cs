using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UniverseLib;

[assembly: MelonInfo(typeof(FamilyTimeTools.EspMod), "FamilyTime Tools", "2.1.0", "P0pola")]
[assembly: MelonGame("sgthale", "Family Time")]

namespace FamilyTimeTools
{
    public sealed partial class EspMod : MelonMod
    {
        internal static EspMod Instance { get; private set; }
        internal UI.EspWindow Window { get; private set; }
        private bool _windowRequested;
        private bool _uiReady;
        internal Font UiFont { get; private set; }
        internal Font EspFont { get; private set; }
        internal bool FreeBuildEnabled => _freeBuild.Value;

        public override void OnInitializeMelon()
        {
            Instance = this;
            CreateSettings();
            Universe.Init(1f, CreateUI, LogUniverse, default(UniverseLib.Config.UniverseLibConfig));
            LoggerInstance.Msg("FamilyTimeTools 2.1.0：F2 开关设置窗口。");
        }

        // 中文界面字体：游戏是 Windows 游戏，微软雅黑必然存在，直接优先用系统字体。
        // 后面几个是保险——万一系统没有微软雅黑（精简版 Windows / Wine），
        // 还能落到别的中文字体上，不会退化成 Arial 把中文画成方框。
        private static readonly string[] CjkFontNames =
        {
            "Microsoft YaHei",
            "Microsoft YaHei UI",
            "SimHei",
            "SimSun",
            "Noto Sans CJK SC",
            "Noto Sans SC",
            "PingFang SC",
            "WenQuanYi Micro Hei",
        };

        private static Font CreateCjkFont(int size)
        {
            return Font.CreateDynamicFontFromOSFont(CjkFontNames, size);
        }

        private void CreateUI()
        {
            UiFont = CreateCjkFont(18);
            EspFont = CreateCjkFont(14);
            var owner = UniverseLib.UI.UniversalUI.RegisterUI("local.FamilyTimeTools", UpdateUI);
            Window = new UI.EspWindow(owner);
            LoggerInstance.Msg("FamilyTimeTools UI ready, uiFont = " + UiFont.name + ", espFont = " + EspFont.name);
            _uiReady = true;
            SetWindowVisible(_windowRequested);
        }

        private void LogUniverse(string message, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) LoggerInstance.Error(message);
            else if (type == LogType.Warning) LoggerInstance.Warning(message);
            else LoggerInstance.Msg(message);
        }

        internal void SetWindowVisible(bool visible)
        {
            _windowRequested = visible;
            if (!_uiReady) return; // F2 can be pressed before UniverseLib finishes initialization.
            Window.SetActive(visible);
            Window.Owner.Enabled = visible;
            if (visible) Window.Owner.SetOnTop();
        }

        private void UpdateUI()
        {
            if (_uiReady && Window.Owner.Enabled) Window.Tick();
        }

        public override void OnUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f2Key.wasPressedThisFrame)
                SetWindowVisible(!_windowRequested);
            if (_settingsDirty && Time.realtimeSinceStartup >= _saveAt) PersistSettings();
            if (!_enabled.Value)
            {
                _targets.Clear();
                return;
            }
            _scanTimer -= Time.unscaledDeltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = _scanInterval.Value;
                ScanWorld();
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            _targets.Clear();
            _scanTimer = 0f;
            FloorStacking.Reset();
        }

        public override void OnDeinitializeMelon()
        {
            PersistSettings();
            if (_uiReady)
            {
                SetWindowVisible(false);
                var owner = Window.Owner;
                Window.Destroy();
                Object.Destroy(owner.RootObject);
                Object.Destroy(UiFont);
                Object.Destroy(EspFont);
            }
            if (_white != null) Object.Destroy(_white);
            Instance = null;
        }
    }
}
