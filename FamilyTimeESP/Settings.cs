using System;
using System.Collections.Generic;
using MelonLoader;

namespace FamilyTimeESP
{
    public sealed partial class EspMod
    {
        internal MelonPreferences_Category _cat;
        internal MelonPreferences_Entry<bool> _enabled;
        internal MelonPreferences_Entry<float> _maxDistance;
        internal MelonPreferences_Entry<bool> _showDistance;
        internal MelonPreferences_Entry<bool> _showName;
        internal MelonPreferences_Entry<bool> _onlyOnScreen;
        internal MelonPreferences_Entry<bool> _offscreen;
        internal MelonPreferences_Entry<bool> _rainbow;
        internal MelonPreferences_Entry<float> _scale;
        internal MelonPreferences_Entry<float> _thickness;
        internal MelonPreferences_Entry<int> _style;
        internal MelonPreferences_Entry<int> _maxTargets;
        internal readonly Dictionary<Cat, MelonPreferences_Entry<bool>> _catFilter
            = new Dictionary<Cat, MelonPreferences_Entry<bool>>();


        internal MelonPreferences_Entry<bool> _showBoxes, _showTracers, _showCenter;
        internal MelonPreferences_Entry<float> _scanInterval;
        private bool _settingsDirty;
        private float _saveAt;

        private void CreateSettings()
        {
            _cat = MelonPreferences.CreateCategory("FamilyTimeESP", "FamilyTime ESP");
            _enabled = _cat.CreateEntry<bool>("Enabled", true, "启用 ESP", "F1 开关");
            _maxDistance = _cat.CreateEntry<float>("MaxDistance", 150f, "最大距离", "米");
            _showDistance = _cat.CreateEntry<bool>("ShowDistance", true, "显示距离", "");
            _showName = _cat.CreateEntry<bool>("ShowName", true, "显示名称", "");
            _onlyOnScreen = _cat.CreateEntry<bool>("OnlyOnScreen", false, "仅屏幕内", "");
            _offscreen = _cat.CreateEntry<bool>("Offscreen", true, "屏外指示", "屏幕边缘标记");
            _rainbow = _cat.CreateEntry<bool>("Rainbow", false, "按距离渐变", "");
            _scale = _cat.CreateEntry<float>("Scale", 1f, "框体缩放", "0.3 - 3.0");
            _thickness = _cat.CreateEntry<float>("Thickness", 1.5f, "线宽", "1 - 4");
            _style = _cat.CreateEntry<int>("Style", 0, "框体样式", "0方框 1四角 2填充 3方框+角");
            _maxTargets = _cat.CreateEntry<int>("MaxTargets", 200, "最多目标", "10 - 800");

            foreach (Cat c in AllCats)
            {
                bool def = c != Cat.Other && c != Cat.Item;
                _catFilter[c] = _cat.CreateEntry<bool>("Cat_" + c, def, CatName(c), "");
            }


            _showBoxes = _cat.CreateEntry("ShowBoxes", true, "绘制框体");
            _showTracers = _cat.CreateEntry("ShowTracers", false, "绘制连线");
            _showCenter = _cat.CreateEntry("ShowCenter", false, "绘制中心点");
            _scanInterval = _cat.CreateEntry("ScanInterval", 0.15f, "扫描间隔");
        }

        internal void SettingsChanged()
        {
            _scanTimer = 0f;
            _settingsDirty = true;
            _saveAt = UnityEngine.Time.realtimeSinceStartup + 0.5f;
        }

        internal void PersistSettings()
        {
            _cat.SaveToFile(false);
            _settingsDirty = false;
        }

        internal void SetAllCategories(bool value)
        {
            foreach (var entry in _catFilter.Values) entry.Value = value;
            SettingsChanged();
        }

        internal void ResetSettings()
        {
            _enabled.Value = true;
            _maxDistance.Value = 150f;
            _maxTargets.Value = 200;
            _showDistance.Value = _showName.Value = true;
            _onlyOnScreen.Value = false;
            _offscreen.Value = true;
            _rainbow.Value = false;
            _scale.Value = 1f;
            _thickness.Value = 1.5f;
            _style.Value = 0;
            _showBoxes.Value = true;
            _showTracers.Value = _showCenter.Value = false;
            _scanInterval.Value = 0.15f;
            foreach (Cat category in AllCats)
                _catFilter[category].Value = category != Cat.Other && category != Cat.Item;
            SettingsChanged();
        }
    }
}
