using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UniverseLib;

[assembly: MelonInfo(typeof(FamilyTimeTools.EspMod), "FamilyTime Tools", "2.0.0", "P0pola")]
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
            LoggerInstance.Msg("FamilyTimeTools 2.0.0：F1 开关 ESP，F2 开关 UniverseLib 窗口。");
        }

        private void CreateUI()
        {
            UiFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Noto Sans SC", "Noto Sans CJK SC", "Microsoft YaHei", "SimHei", "Arial" }, 18);
            string fontPath = System.IO.Path.Combine(Application.dataPath, "Fonts", "NotoSansCJKsc-Regular.otf");
            EspFont = System.IO.File.Exists(fontPath)
                ? new Font(fontPath)
                : Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC" }, 14);
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
            if (keyboard != null)
            {
                if (keyboard.f1Key.wasPressedThisFrame)
                {
                    _enabled.Value = !_enabled.Value;
                    SettingsChanged();
                }
                if (keyboard.f2Key.wasPressedThisFrame) SetWindowVisible(!_windowRequested);
            }
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
