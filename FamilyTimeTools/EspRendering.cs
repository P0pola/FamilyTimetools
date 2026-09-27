using UnityEngine;

namespace FamilyTimeTools
{
    public sealed partial class EspMod
    {
        private Texture2D _white;
        private GUIStyle _stText, _stSmall;
        private static readonly Color CText = new Color(0.90f, 0.92f, 0.95f);
        private static readonly Color CSub = new Color(0.65f, 0.70f, 0.78f);

        public override void OnGUI()
        {
            if (!_uiReady || Event.current.type != EventType.Repaint) return;
            if (_white == null)
            {
                _white = new Texture2D(1, 1);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
                _stText = new GUIStyle(GUI.skin.label) { font = EspFont, fontSize = 13, alignment = TextAnchor.MiddleCenter };
                _stSmall = new GUIStyle(_stText) { fontSize = 11 };
                _stText.normal.textColor = Color.white;
                _stSmall.normal.textColor = Color.white;
            }
            if (!_enabled.Value) return;
            var camera = Camera.main;
            if (camera == null) return;
            foreach (EspTarget target in _targets) DrawTarget(target, camera);
        }

        private void DrawLine(Vector2 start, Vector2 end, Color color)
        {
            Matrix4x4 matrix = GUI.matrix;
            Vector2 direction = end - start;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, start);
            Fill(new Rect(start.x, start.y, direction.magnitude, _thickness.Value), color);
            GUI.matrix = matrix;
        }

        private void DrawTarget(EspTarget t, Camera cam)
        {
            Vector3 sp = cam.WorldToScreenPoint(t.World);
            bool behind = sp.z <= 0f;

            float x = sp.x;
            float y = Screen.height - sp.y;

            bool onScreen = !behind && x >= 0f && x <= Screen.width && y >= 0f && y <= Screen.height;
            if (!onScreen)
            {
                if (_onlyOnScreen.Value || !_offscreen.Value) return;
                if (behind)
                {
                    x = Screen.width - x;
                    y = Screen.height - y;
                }
                x = Mathf.Clamp(x, 14f, Screen.width - 14f);
                y = Mathf.Clamp(y, 14f, Screen.height - 14f);
                Fill(new Rect(x - 5f, y - 5f, 10f, 10f), t.Color);
                Border(new Rect(x - 5f, y - 5f, 10f, 10f), CText);
                return;
            }


            float sc = _scale.Value;
            float h = Mathf.Clamp(1400f / Mathf.Max(t.Distance, 1f), 14f, 90f) * sc;
            float w = h * 0.5f;
            var box = new Rect(x - w * 0.5f, y - h, w, h);

            Color col = t.Color;
            if (_rainbow.Value)
            {
                float k = Mathf.Clamp01(t.Distance / Mathf.Max(_maxDistance.Value, 1f));
                col = Color.Lerp(new Color(0.3f, 1f, 0.4f), new Color(1f, 0.25f, 0.2f), k);
            }

            int style = _style.Value;
            if (_showTracers.Value) DrawLine(new Vector2(Screen.width * 0.5f, Screen.height - 12f), new Vector2(x, y), col);
            if (_showCenter.Value) Fill(new Rect(x - 2f, y - 2f, 4f, 4f), col);
            if (_showBoxes.Value) switch (style)
            {
                case 1: Corners(box, col); break;
                case 2: Fill(box, new Color(col.r, col.g, col.b, 0.18f)); Border(box, col); break;
                case 3: Box(box, col); Corners(box, col); break;
                default: Box(box, col); break;
            }

            if (_showName.Value)
            {
                var prev = GUI.color;
                GUI.color = col;
                GUI.Label(new Rect(x - 110f, y - h - 17f, 220f, 17f), t.Label, _stText);
                GUI.color = prev;
            }
            if (_showDistance.Value)
            {
                var prev = GUI.color;
                GUI.color = CSub;
                GUI.Label(new Rect(x - 110f, y + 2f, 220f, 17f), t.Distance.ToString("F0") + "m", _stSmall);
                GUI.color = prev;
            }
        }

        private void Box(Rect r, Color c)
        {
            float th = _thickness.Value;
            Fill(new Rect(r.x, r.y, r.width, th), c);
            Fill(new Rect(r.x, r.yMax - th, r.width, th), c);
            Fill(new Rect(r.x, r.y, th, r.height), c);
            Fill(new Rect(r.xMax - th, r.y, th, r.height), c);
        }

        private void Corners(Rect r, Color c)
        {
            float th = _thickness.Value;
            float len = Mathf.Min(r.width, r.height) * 0.32f;
            Fill(new Rect(r.x, r.y, len, th), c);
            Fill(new Rect(r.x, r.y, th, len), c);
            Fill(new Rect(r.xMax - len, r.y, len, th), c);
            Fill(new Rect(r.xMax - th, r.y, th, len), c);
            Fill(new Rect(r.x, r.yMax - th, len, th), c);
            Fill(new Rect(r.x, r.yMax - len, th, len), c);
            Fill(new Rect(r.xMax - len, r.yMax - th, len, th), c);
            Fill(new Rect(r.xMax - th, r.yMax - len, th, len), c);
        }

        private void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = prev;
        }

        private void Border(Rect r, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, 1f), c);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            Fill(new Rect(r.x, r.y, 1f, r.height), c);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }

    }
}
