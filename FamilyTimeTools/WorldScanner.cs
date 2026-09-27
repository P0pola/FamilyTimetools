using System.Collections.Generic;
using GAT;
using UnityEngine;

namespace FamilyTimeTools
{
    internal enum Cat { Boar, Chicken, Hen, Rooster, Chick, Pig, Piglet, WolfGirl, WolfMama, Wolf, Player, Deer, Rabbit, Other, Item }

    internal sealed class EspTarget
    {
        public Vector3 World;
        public string Label;
        public Color Color;
        public float Distance;
        public Cat Cat;
    }

    public sealed partial class EspMod
    {
        internal readonly List<EspTarget> _targets = new List<EspTarget>();
        internal static readonly Cat[] AllCats = (Cat[])System.Enum.GetValues(typeof(Cat));
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
                Cat cat = DescribeHandle(gat, handle, out raw);
                if (!PassFilter(cat)) continue;

                _targets.Add(new EspTarget
                {
                    World = pos,
                    Label = raw,
                    Cat = cat,
                    Color = ColorFor(cat),
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

        private static Cat DescribeHandle(GAT.GAT gat, Handle handle, out string label)
        {
            label = "实体";
            int idx = gat.GetTemplateIndexFromHandle(handle);
            var tpl = gat.GetTemplate(idx);
            if (tpl != null)
            {
                string n = tpl.name;
                if (!string.IsNullOrEmpty(n))
                {
                    string low = n.ToLowerInvariant();
                    if (low.Contains("baby_chick")) { label = "小鸡"; return Cat.Chick; }
                    if (low.Contains("rooster")) { label = "公鸡"; return Cat.Rooster; }
                    if (low.Contains("hen")) { label = "母鸡"; return Cat.Hen; }
                    if (low.Contains("chicken")) { label = "鸡"; return Cat.Chicken; }
                    if (low.Contains("piglet")) { label = "猪崽"; return Cat.Piglet; }
                    if (low.Contains("pig")) { label = "猪"; return Cat.Pig; }
                    if (low.Contains("boar")) { label = "野猪"; return Cat.Boar; }
                    if (low.Contains("wolf_girl") || low.Contains("wolfgirl")) { label = "狼女"; return Cat.WolfGirl; }
                    if (low.Contains("wolf_mama") || low.Contains("wolfmama")) { label = "狼妈妈"; return Cat.WolfMama; }
                    if (low.Contains("wolf")) { label = "狼"; return Cat.Wolf; }
                    if (low.Contains("fake_player") || low.Contains("player")) { label = "玩家"; return Cat.Player; }
                    if (low.Contains("deer")) { label = "鹿"; return Cat.Deer; }
                    if (low.Contains("rabbit")) { label = "兔子"; return Cat.Rabbit; }
                    label = n;
                }
            }
            return Cat.Other;
        }

        private void ScanRigidTransforms(Vector3 eye)
        {
            if (!PassFilter(Cat.Item)) return;

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

                string label = "物品";
                var go = rigid.gameObject;
                if (go != null && !string.IsNullOrEmpty(go.name)) label = go.name;

                _targets.Add(new EspTarget
                {
                    World = pos,
                    Label = label,
                    Cat = Cat.Item,
                    Color = new Color(0.50f, 0.85f, 1f),
                    Distance = dist,
                });
            }
        }

        private bool PassFilter(Cat c)
        {
            return _catFilter[c].Value;
        }

        internal static string CatName(Cat c)
        {
            switch (c)
            {
                case Cat.Boar: return "野猪";
                case Cat.Chicken: return "鸡肉";
                case Cat.Hen: return "母鸡";
                case Cat.Rooster: return "公鸡";
                case Cat.Chick: return "小鸡";
                case Cat.Pig: return "猪";
                case Cat.Piglet: return "猪崽";
                case Cat.WolfGirl: return "狼女";
                case Cat.WolfMama: return "狼妈妈";
                case Cat.Wolf: return "狼";
                case Cat.Player: return "玩家";
                case Cat.Deer: return "鹿";
                case Cat.Rabbit: return "兔子";
                case Cat.Item: return "物品";
                default: return "其他";
            }
        }

        internal static Color ColorFor(Cat c)
        {
            switch (c)
            {
                case Cat.Boar: return new Color(1.00f, 0.55f, 0.20f);
                case Cat.Chicken: return new Color(1.00f, 0.95f, 0.40f);
                case Cat.Hen: return new Color(1.00f, 0.82f, 0.45f);
                case Cat.Rooster: return new Color(1.00f, 0.45f, 0.35f);
                case Cat.Chick: return new Color(1.00f, 1.00f, 0.65f);
                case Cat.Pig: return new Color(1.00f, 0.65f, 0.75f);
                case Cat.Piglet: return new Color(1.00f, 0.78f, 0.85f);
                case Cat.WolfGirl: return new Color(0.75f, 0.55f, 1.00f);
                case Cat.WolfMama: return new Color(0.90f, 0.35f, 0.85f);
                case Cat.Wolf: return new Color(0.95f, 0.30f, 0.30f);
                case Cat.Player: return new Color(0.40f, 1.00f, 0.55f);
                case Cat.Deer: return new Color(0.90f, 0.75f, 0.50f);
                case Cat.Rabbit: return new Color(0.95f, 0.95f, 0.95f);
                case Cat.Item: return new Color(0.50f, 0.85f, 1.00f);
                default: return new Color(0.40f, 1.00f, 0.60f);
            }
        }

    }
}
