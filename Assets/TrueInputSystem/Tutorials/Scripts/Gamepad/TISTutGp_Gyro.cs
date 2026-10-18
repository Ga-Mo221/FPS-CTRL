// ============================================================
//  TrueInputSystem Tutorials — Tay cầm · Gyro (Look + Gyro, Motion, bật/tắt, hiệu chỉnh)
// ============================================================

using TMPro;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Tay cầm · Gyro")]
    public class TISTutGp_Gyro : TISTutLesson
    {
        public override string Title => "Gyro: ngắm bằng cách xoay tay cầm";

        public override string Summary =>
            "Tay cầm có gyro (DualShock 4, DualSense, Switch Pro) cho phép tinh chỉnh tâm ngắm bằng cách xoay tay cầm — chính xác gần như chuột. Bật \"+ Gyro\" cho action Look là LookDelta đã cộng sẵn gyro. Không có tay cầm gyro: trong Editor dùng I J K L để giả lập.";

        public override string[] SetupSteps => new[]
        {
            "Setup Tool › action <b>Look</b> › Binding Gamepad = Right Stick › bật <b>+ Gyro</b>.",
            "Motion Settings: <b>Gyro Sensitivity</b>, <b>Activation</b> (luôn bật / chỉ khi giữ nút ngắm), lọc rung, tự hiệu chỉnh khi đặt yên.",
            "PC đọc gyro qua <b>SDL3</b> (Use SDL3 On PC). Tay cầm chỉ chạy XInput (Xbox, đầu thu 2.4G) không có gyro.",
            "Cho người chơi tắt gyro: <b>SetActionGyroEnabled(\"Look\", false, save: true)</b> — tự lưu và nạp lại.",
        };

        public override string Code =>
@"// Look đã gồm gyro — không cần code thêm
Vector2 look = tis.LookDelta;

var m = tis.Motion;
if (m.HasGyro)
    Debug.Log(m.GyroRate + "" °/s"");

// Menu Settings: tắt gyro cho camera
tis.SetActionGyroEnabled(""Look"", false,
                         save: true);

// Hiệu chỉnh: đặt tay cầm yên
m.BeginGyroCalibration();
bool ok = m.EndGyroCalibration();";

        public override string[] Objectives => new[]
        {
            "Xoay camera 90° chỉ bằng gyro (Editor: giữ J hoặc L)",
            $"Nhấn {TISTutGlyphs.North} tắt gyro cho Look, rồi nhấn lại để bật",
            $"Nhấn {TISTutGlyphs.West} và đặt tay cầm yên để hiệu chỉnh",
        };

        public override string FooterHint => "Xoay tay cầm để nhìn · Editor không có gyro: I J K L (Right Shift = nhanh)";

        float _gyroYaw;
        bool  _wasOff;
        float _calibUntil = -1f;
        RectTransform _dot;
        TMP_Text _status;

        protected override void OnEnter()
        {
            GamepadCamera();
            _gyroYaw = 0f; _wasOff = false; _calibUntil = -1f;
            TIS.SetActionGyroEnabled("Look", true);

            // Đồng hồ gyro: chấm lệch theo tốc độ xoay
            var card = TISTutUI.Card(Overlay, "Gyro", new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(240f, 250f));
            var t = TISTutUI.Text(card.transform, "Title", "GYRO RATE", Theme.monoBold, 14, Theme.inkSoft, TextAlignmentOptions.Center);
            TISTutUI.Place(t.rectTransform, 8f, 10f, 8f, -1f, height: 22f);
            var ring = TISTutUI.Shape(card.transform, "Ring", Theme.paper, 70f, 2f, Theme.line);
            TISTutUI.At(ring.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(140f, 140f));
            var d = TISTutUI.Shape(ring.transform, "Dot", Theme.AccentStrong(), 13f);
            _dot = d.rectTransform;
            _dot.anchorMin = _dot.anchorMax = _dot.pivot = new Vector2(0.5f, 0.5f);
            _dot.sizeDelta = new Vector2(26f, 26f);
            _status = TISTutUI.Text(card.transform, "Status", "", Theme.bodyBold, 15, Theme.ink, TextAlignmentOptions.Center, wrap: true);
            TISTutUI.Place(_status.rectTransform, 10f, -1f, 10f, 10f, height: 44f);

            GamepadWidget(new Vector2(0f, 0f), new Vector2(20f, 20f), 0.7f);

            var m = TIS.Motion;
            Watch("Motion.Source", () => m.Source != null ? m.Source.Name : "không có");
            Watch("Motion.HasGyro", () => F(m.HasGyro));
            Watch("Motion.GyroRate", () => F(m.GyroRate) + " °/s", () => Mathf.Clamp01(m.GyroRate.magnitude / 180f));
            Watch("IsActionGyroEnabled(\"Look\")", () => F(TIS.IsActionGyroEnabled("Look")));
            Watch("Gyro đã xoay", () => Mathf.Min(90f, _gyroYaw).ToString("0") + " / 90°", () => _gyroYaw / 90f);
        }

        protected override void OnTick()
        {
            var tis = TIS;
            var m = tis.Motion;
            float dt = Time.deltaTime;

            _dot.anchoredPosition = Vector2.ClampMagnitude(m.GyroRate / 180f, 1f) * 56f;

            // Xoay bằng gyro: chỉ tính khi cần phải đứng yên
            var gp = UnityEngine.InputSystem.Gamepad.current;
            bool stickIdle = gp == null || gp.rightStick.ReadValue().sqrMagnitude < 0.02f;
            if (m.HasGyro && tis.IsActionGyroEnabled("Look") && stickIdle)
                _gyroYaw += Mathf.Abs(m.GyroAim.x) * dt * 1.5f;
            if (_gyroYaw >= 90f) Complete(0);

            if (tis.IsSkillDown("Toggle"))
            {
                bool on = !tis.IsActionGyroEnabled("Look");
                tis.SetActionGyroEnabled("Look", on);
                Toast(on ? "Gyro cho camera: BẬT" : "Gyro cho camera: TẮT");
                if (!on) _wasOff = true;
                else if (_wasOff) Complete(1);
            }

            if (_calibUntil < 0f && tis.IsSkillDown("Interact"))
            {
                m.BeginGyroCalibration();
                _calibUntil = Time.unscaledTime + 2f;
            }
            if (_calibUntil >= 0f)
            {
                float left = _calibUntil - Time.unscaledTime;
                _status.text = $"Đang hiệu chỉnh… giữ yên {Mathf.CeilToInt(left)}";
                if (left <= 0f)
                {
                    bool ok = m.EndGyroCalibration();
                    _calibUntil = -1f;
                    Toast(ok ? "Hiệu chỉnh gyro xong ✓" : "Tay cầm chưa đủ yên — thử lại");
                    if (ok) Complete(2);
                }
            }
            else
            {
                _status.text = m.HasGyro ? (tis.IsActionGyroEnabled("Look") ? "Gyro đang điều khiển camera" : "Gyro cho camera đang TẮT")
                                         : "Không có nguồn gyro";
            }
        }

        protected override void OnExit()
        {
            if (_calibUntil >= 0f) TIS.Motion.EndGyroCalibration();
            TIS.SetActionGyroEnabled("Look", true);
        }
    }
}
