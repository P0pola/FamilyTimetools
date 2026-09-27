using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Models;

namespace FamilyTimeESP.UI
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
