using System;
using GAT;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Models;

namespace FamilyTimeTools.UI
{
    internal static class UiControls
    {
        internal static readonly Color Background = new Color(0.065f, 0.085f, 0.12f);
        internal static readonly Color Surface = new Color(0.10f, 0.13f, 0.18f);
        internal static readonly Color Accent = new Color(0.18f, 0.62f, 0.85f);
        internal static readonly Color TextColor = new Color(0.91f, 0.94f, 0.98f);
        internal static readonly Color Muted = new Color(0.60f, 0.68f, 0.78f);
        internal static readonly Color OnColor = new Color(0.12f, 0.48f, 0.38f);

        internal static GameObject Vertical(GameObject parent, string name, int spacing = 8, int padding = 10)
        {
            return UIFactory.CreateVerticalGroup(parent, name, true, false, true, true,
                spacing, new Vector4(padding, padding, padding, padding), Background);
        }

        internal static GameObject Horizontal(GameObject parent, string name, int height)
        {
            var row = UIFactory.CreateHorizontalGroup(parent, name, false, false, true, true,
                8, new Vector4(8, 8, 5, 5), Surface);
            UIFactory.SetLayoutElement(row, minHeight: height, preferredHeight: height, flexibleWidth: 1, flexibleHeight: 0);
            return row;
        }

        internal static Text Label(GameObject parent, string value, int height = 26, int size = 14, bool muted = false)
        {
            var label = UIFactory.CreateLabel(parent, "Label", value, TextAnchor.MiddleLeft,
                muted ? Muted : TextColor, false, size);
            label.font = EspMod.Instance.UiFont;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            UIFactory.SetLayoutElement(label.gameObject, minHeight: height, preferredHeight: height, flexibleWidth: 1, flexibleHeight: 0);
            return label;
        }

        internal static ButtonRef Button(GameObject parent, string caption, Action action, int width = 0)
        {
            var button = UIFactory.CreateButton(parent, "Action", caption, Surface);
            button.ButtonText.font = EspMod.Instance.UiFont;
            button.ButtonText.fontSize = 14;
            button.ButtonText.color = TextColor;
            UIFactory.SetLayoutElement(button.GameObject, minWidth: width, minHeight: 32,
                preferredWidth: width > 0 ? (int?)width : null, preferredHeight: 32,
                flexibleWidth: width > 0 ? 0 : 1, flexibleHeight: 0);
            button.OnClick += action;
            Paint(button, Surface);
            return button;
        }

        internal static void Paint(ButtonRef button, Color color)
        {
            ColorBlock colors = button.Component.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
            colors.selectedColor = color;
            colors.disabledColor = Background;
            colors.colorMultiplier = 1f;
            button.Component.colors = colors;
        }

        internal static void Toggle(GameObject parent, string caption, string description,
            Func<bool> get, Action<bool> set, List<Action> refresh)
        {
            var row = Horizontal(parent, "Switch", 52);
            Label(row, caption + "\n" + description, 42);
            ButtonRef button = Button(row, "", () =>
            {
                set(!get());
                EspMod.Instance.SettingsChanged();
            }, 86);
            Action sync = () =>
            {
                bool enabled = get();
                button.ButtonText.text = enabled ? "开启  ●" : "关闭  ○";
                Paint(button, enabled ? OnColor : Surface);
            };
            refresh.Add(sync);
            sync();
        }

        internal static void Dropdown(GameObject parent, string caption, string[] options, int defaultIndex,
            System.Action<int> onChanged, List<System.Action> refresh, System.Func<int> current)
        {
            var box = Vertical(parent, "DropdownSetting", 4, 8);
            UIFactory.SetLayoutElement(box, minHeight: 62, preferredHeight: 62, flexibleWidth: 1, flexibleHeight: 0);
            Label(box, caption, 22);
            UnityEngine.UI.Dropdown dropdown;
            GameObject root = UIFactory.CreateDropdown(box, "Value", out dropdown, "选择...", 14, onChanged, options);
            UIFactory.SetLayoutElement(root, minHeight: 30, preferredHeight: 30, flexibleWidth: 1, flexibleHeight: 0);
            dropdown.captionText.font = EspMod.Instance.UiFont;
            dropdown.itemText.font = EspMod.Instance.UiFont;
            dropdown.value = defaultIndex;
            dropdown.RefreshShownValue();
            refresh.Add(() =>
            {
                int want = current();
                if (want >= 0 && want < options.Length && dropdown.value != want)
                {
                    dropdown.SetValueWithoutNotify(want);
                    dropdown.RefreshShownValue();
                }
            });
        }

        internal static UnityEngine.UI.InputField Input(GameObject parent, string caption, string placeholder,
            string initial, System.Action<string> onChanged, int width = 0)
        {
            var box = Vertical(parent, "InputSetting", 4, 8);
            UIFactory.SetLayoutElement(box, minHeight: 62, preferredHeight: 62, flexibleWidth: width > 0 ? 0 : 1, flexibleHeight: 0, minWidth: width);
            Label(box, caption, 22);
            InputFieldRef field = UIFactory.CreateInputField(box, "Value", placeholder);
            UnityEngine.UI.InputField input = field.Component;
            input.textComponent.font = EspMod.Instance.UiFont;
            if (input.placeholder is UnityEngine.UI.Text ph) ph.font = EspMod.Instance.UiFont;
            input.text = initial;
            input.onValueChanged.AddListener(value => onChanged(value));
            UIFactory.SetLayoutElement(field.GameObject, minHeight: 30, preferredHeight: 30, flexibleWidth: 1, flexibleHeight: 0);
            return input;
        }

        // 热键捕获：点一下按钮进入录制，接下来按的键写回配置。
        // 实际采集由 PollHotkeyCapture 每帧轮询，避开 UGUI 的事件系统。
        private static bool _capturing;
        private static int _captureStartFrame = -1;

        internal static bool IsCapturingHotkey => _capturing;
        private static Action<string> _captureSetter;

        internal static void Hotkey(GameObject parent, string caption, string description,
            Func<string> get, Action<string> set, List<Action> refresh)
        {
            var row = Horizontal(parent, "Hotkey", 52);
            Label(row, caption + "\n" + description, 42);
            ButtonRef button = Button(row, "", () => StartCapture(set), 132);

            Action sync = () =>
            {
                button.ButtonText.text = _capturing ? "按下新热键…" : get();
                Paint(button, _capturing ? Accent : Surface);
            };
            refresh.Add(sync);
            sync();
        }

        private static readonly List<UnityEngine.InputSystem.InputActionMap> SuspendedMaps
            = new List<UnityEngine.InputSystem.InputActionMap>();

        private static void StartCapture(Action<string> setter)
        {
            _capturing = true;
            _captureStartFrame = UnityEngine.Time.frameCount;
            _captureSetter = setter;

            // 捕获期间禁用游戏输入，避免注册热键时误触发建造/交互。
            // 做法与游戏自带的 InputMenu 一致。
            SuspendMaps(World.playerController == null ? null : World.playerController.InputActionsAsset);
            SuspendMaps(World.commandInputActions);
        }

        private static void SuspendMaps(UnityEngine.InputSystem.InputActionAsset asset)
        {
            if (asset == null) return;
            for (int i = 0; i < asset.actionMaps.Count; i++)
            {
                var map = asset.actionMaps[i];
                if (!map.enabled) continue;
                SuspendedMaps.Add(map);
                map.Disable();
            }
        }

        private static void RestoreMaps()
        {
            for (int i = 0; i < SuspendedMaps.Count; i++)
                SuspendedMaps[i].Enable();
            SuspendedMaps.Clear();
        }

        internal static void CancelCapture()
        {
            _capturing = false;
            _captureSetter = null;
            RestoreMaps();
        }

        // 每帧轮询：捕到第一个按下的键就定下来。Esc 取消。
        internal static void PollHotkeyCapture()
        {
            if (!_capturing) return;
            // 跳过点击“录制”按钮那一帧，否则会把鼠标左键录成热键。
            if (UnityEngine.Time.frameCount <= _captureStartFrame) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;

            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) { Commit("mouse0"); return; }
                if (mouse.rightButton.wasPressedThisFrame) { Commit("mouse1"); return; }
                if (mouse.middleButton.wasPressedThisFrame) { Commit("mouse2"); return; }
            }

            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { CancelCapture(); return; }

                foreach (var control in keyboard.allKeys)
                {
                    if (control == null || !control.wasPressedThisFrame) continue;
                    // 存 Key 枚举名（G / LeftShift / F5），比 displayName 稳定且好解析。
                    Commit(control.keyCode.ToString());
                    return;
                }
            }
        }

        private static void Commit(string name)
        {
            if (_captureSetter != null) _captureSetter(name);
            CancelCapture();
            if (EspMod.Instance != null) EspMod.Instance.SettingsChanged();
        }

        internal static void Slider(GameObject parent, string caption, float min, float max,
            Func<float> get, Action<float> set, string format, bool whole, List<Action> refresh)
        {
            var box = Vertical(parent, "SliderSetting", 3, 8);
            UIFactory.SetLayoutElement(box, minHeight: 76, preferredHeight: 76, flexibleWidth: 1, flexibleHeight: 0);
            Text valueLabel = Label(box, "", 24);
            UnityEngine.UI.Slider slider;
            GameObject root = UIFactory.CreateSlider(box, "Value", out slider);
            UIFactory.SetLayoutElement(root, minHeight: 28, preferredHeight: 28, flexibleWidth: 1, flexibleHeight: 0);
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = whole;
            slider.SetValueWithoutNotify(get());
            slider.onValueChanged.AddListener(value =>
            {
                set(value);
                valueLabel.text = caption + "    " + value.ToString(format);
                EspMod.Instance.SettingsChanged();
            });
            Action sync = () =>
            {
                slider.SetValueWithoutNotify(get());
                valueLabel.text = caption + "    " + get().ToString(format);
            };
            refresh.Add(sync);
            sync();
        }
    }
}
