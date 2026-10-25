// ============================================================
//  TrueInputSystem — TIS_MotionDebugGUI.cs
//
//  Bảng debug gyro / touchpad trên màn hình (OnGUI) — dùng khi test.
//  KHÔNG cần add component: bật "Show Motion Debug GUI" trong Inspector
//  của TISInputBridge (mục Motion), bridge tự gọi Draw() trong OnGUI.
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem
{
    public static class TISMotionDebugGUI
    {
        private static readonly Rect Area = new Rect(10, 10, 420, 290);
        private static GUIStyle _title;

        public static void Draw(TISInputBridge bridge)
        {
            if (bridge == null) return;

            var m   = bridge.Motion;
            var p   = m.GyroProcessor;
            var src = m.Source;

            _title ??= new GUIStyle(GUI.skin.label) { richText = true };

            GUILayout.BeginArea(Area, GUI.skin.box);
            GUILayout.Label($"<b>TIS Motion — {(src != null ? src.Name : "không có source")}</b>", _title);
            GUILayout.Label($"Gyro={(m.GyroEnabled ? "ON" : "OFF")}  Touchpad={(m.TouchpadEnabled ? "ON" : "OFF")}  " +
                            $"Sources={m.Sources.Count}  ActiveDevice={bridge.ActiveDevice}");

            if (src != null)
            {
                GUILayout.Label($"Raw gyro : {src.Gyro:F2} deg/s");
                GUILayout.Label($"Accel    : {src.Accel:F2} g");
            }

            GUILayout.Label($"Offset   : {p.Offset:F2}  calibrated={p.IsCalibrated}  still={p.IsStill}");
            GUILayout.Label($"Rate     : {m.GyroRate:F2} deg/s");
            GUILayout.Label($"GyroAim  : {m.GyroAim:F2} deg/s  active={m.IsGyroAimActive}  " +
                            $"device={(bridge.MotionDevice != null ? "ON" : "off")}");
            GUILayout.Label($"LookDelta: {bridge.LookDelta:F3}");
            GUILayout.Label($"Touch    : {(m.IsTouching ? m.TouchpadPosition.ToString("F2") : "-")}  " +
                            $"delta={m.TouchpadDelta:F3}  pressed={m.TouchpadPressed}");

            GUILayout.Label($"Touch v  : {m.TouchpadVelocity:F2} /s");
            if (GUILayout.Button(p.IsManualCalibrating ? "End Calibration (đặt tay cầm yên)" : "Begin Calibration"))
            {
                if (p.IsManualCalibrating) m.EndGyroCalibration();
                else                       m.BeginGyroCalibration();
            }
            GUILayout.EndArea();
        }
    }
}
