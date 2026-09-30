using System;
using GAT;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FamilyTimeTools
{
    // 准星注水：按住热键，从准星命中的地形格持续喷出一股细水柱。
    //
    // 游戏自带公开入口，水桶倒水与闪电洪水都走它：
    //   WaterManager.SpawnWaterAtCell(Vector2Int worldCell, float waterLevel)   // 加水
    //   WaterManager.RemoveWaterAtCell(Vector2Int worldCell, float waterLevel)  // 抽水
    // waterLevel 是 0~1 的归一化水量（内部 Clamp01），写在 R16_UNorm 的水账本上，
    // 1.0 对应 HeightfieldMaxY（约 127.5）的水深。
    //
    // 关键量纲：WaterWellLogicManager.waterBucketCapacity 默认 250，也就是
    //   一桶水 = 250 / 65535 ≈ 0.0038
    // 所以参数一律按“桶”标定。默认每次每格只倒 1/4 桶、每秒 20 次（约 5 桶/秒），
    // 落在单格里才会像一道细水柱；之前直接写 0.25 相当于一次灌 65 桶，必然泄洪。
    //
    // 两个硬限制：
    //   1. 水只在模拟窗口（256×256 格）内生效，窗口跟着玩家走。
    //      超出窗口 SpawnWaterAtCell 直接返回 false，所以注水点必须在玩家附近。
    //   2. 窗口原点只在玩家跨格时刷新，瞄准远处可能整条射线都落在窗口外。
    internal static class WaterTool
    {
        // 一桶水对应的归一化水量。
        private const float BucketWater = 250f / 65535f;

        // 准星取点用的最大距离，和铲子一致（2 格）太短，这里放到 24 格方便远处注水。
        private const float MaxReach = 24f;

        // 喷射脉冲间隔，20Hz：比水自身的 0.1s 模拟步进更快，喷出来才连成一条水柱。
        private const float RepeatInterval = 0.05f;

        private static float _nextApply;

        internal static bool Enabled => EspMod.Instance != null && EspMod.Instance._waterEnabled.Value;

        // 每次脉冲、每格注入的水量，单位是游戏水桶。
        internal static float BucketsPerPulse => EspMod.Instance == null ? 0.25f : EspMod.Instance._waterAmount.Value;

        // 水柱半径（格），0 = 单格细水柱。
        internal static int Radius => EspMod.Instance == null ? 0 : EspMod.Instance._waterRadius.Value;

        internal static void Reset()
        {
            _nextApply = 0f;
        }

        // 每帧由 EspMod.OnUpdate 调用。
        internal static void Update()
        {
            if (!Enabled) return;
            if (UI.UiControls.IsCapturingHotkey) return;
            if (!IsKeyHeld()) return;
            if (Time.realtimeSinceStartup < _nextApply) return;
            _nextApply = Time.realtimeSinceStartup + RepeatInterval;
            ApplyAtCrosshair();
        }

        // ── 热键解析：支持 Keyboard 与 Mouse 的简单名字 ────────────────────────────────
        // 配置里存 "G" / "F5" / "mouse0" / "mouse1" / "leftShift" 这类写法。
        internal static bool IsKeyHeld()
        {
            string name = EspMod.Instance == null ? null : EspMod.Instance._waterKey.Value;
            if (string.IsNullOrEmpty(name)) return false;
            name = name.Trim();

            if (name.StartsWith("mouse", StringComparison.OrdinalIgnoreCase))
            {
                var m = Mouse.current;
                if (m == null) return false;
                string which = name.Substring(5).Trim();
                if (which == "0") return m.leftButton.isPressed;
                if (which == "1") return m.rightButton.isPressed;
                if (which == "2") return m.middleButton.isPressed;
                return false;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return false;

            // 配置存的是 Key 枚举名（G / LeftShift / F5 / Space），
            // 直接解析后查表即可，对大小写不敏感。
            Key parsed;
            if (Enum.TryParse(name, true, out parsed))
                return keyboard[parsed].isPressed;

            // 兼容单字母写法（g -> G）
            if (name.Length == 1 && char.IsLetter(name[0]))
            {
                if (Enum.TryParse(name.ToUpperInvariant(), out parsed))
                    return keyboard[parsed].isPressed;
            }
            return false;
        }

        // ── 准星取点：返回命中的地形格 ──────────────────────────────────────────────────
        internal static bool TryGetCrosshairCell(out Vector2Int cell, out Vector3 point)
        {
            cell = default;
            point = Vector3.zero;

            var camera = Camera.main;
            if (camera == null) return false;

            Vector3 origin, direction;
            if (Cursor.lockState == CursorLockMode.Locked || Mouse.current == null)
            {
                // 锁定鼠标时用屏幕中心（游戏自身的准星）
                var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                origin = ray.origin;
                direction = ray.direction;
            }
            else
            {
                Vector2 screen = Mouse.current.position.ReadValue();
                var ray = camera.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
                origin = ray.origin;
                direction = ray.direction;
            }

            World.HitInfo hit;
            if (!World.Raycast(origin, direction, MaxReach, out hit)) return false;

            // 打中建筑/实体就不注水，只认地形
            if (hit.blockId.HasValue || hit.rigidId.HasValue || hit.boxId.HasValue) return false;
            if (hit.interactivityData != null) return false;

            point = hit.point;
            cell = new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.z));
            return true;
        }

        // ── 实际注水 ────────────────────────────────────────────────────────────────────
        internal static int ApplyAtCrosshair()
        {
            var manager = World.waterManager;
            if (manager == null) return 0;

            Vector2Int center;
            Vector3 point;
            if (!TryGetCrosshairCell(out center, out point)) return 0;

            // 半径 0 就是单格；给个上限只是防止配置被改成天文数字。
            int radius = Mathf.Clamp(Radius, 0, 4);
            float amount = Mathf.Clamp(BucketsPerPulse, 0.02f, 8f) * BucketWater;
            int count = 0;

            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var cell = new Vector2Int(center.x + dx, center.y + dz);
                    if (manager.SpawnWaterAtCell(cell, amount)) count++;
                }
            }
            return count;
        }

        // 抽干是一次性操作，直接把命中范围的水位清空（RemoveWaterAtCell 内部 Clamp01）。
        internal static int DrainAtCrosshair()
        {
            var manager = World.waterManager;
            if (manager == null) return 0;

            Vector2Int center;
            Vector3 point;
            if (!TryGetCrosshairCell(out center, out point)) return 0;

            int radius = Mathf.Clamp(Radius, 0, 4);
            int count = 0;
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var cell = new Vector2Int(center.x + dx, center.y + dz);
                    if (manager.RemoveWaterAtCell(cell, 1f)) count++;
                }
            }
            return count;
        }
    }
}