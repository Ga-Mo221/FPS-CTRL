// ============================================================
//  TrueInputSystem Tutorials — Tay cầm · Cần analog (MoveDirection / LookDelta / deadzone)
// ============================================================

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Tay cầm · Cần analog")]
    public class TISTutGp_Sticks : TISTutLesson
    {
        public override string Title => "Cần analog: đi & xoay";

        public override string Summary =>
            "Cần trái → MoveDirection, cần phải → LookDelta — cùng API với WASD / chuột nên code nhân vật không phải sửa. Profile lo deadzone (bỏ rung nhẹ khi không chạm cần) và đường cong độ nhạy (nhắm chính xác khi đẩy nhẹ, xoay nhanh khi đẩy hết). Chấm đen = giá trị thô, chấm màu = giá trị TIS trả về.";

        public override string[] SetupSteps => new[]
        {
            "Move = <b><Gamepad>/leftStick</b> · Look = <b><Gamepad>/rightStick</b>.",
            "Gamepad Settings › <b>Left / Right Stick Deadzone</b> và <b>Stick Curve</b> (đường cong chỉ áp cho cần phải).",
            "Tốc độ xoay: Camera Settings › <b>Gamepad Sensitivity</b>. Tự đọc giá trị thô thì lọc bằng <b>ApplyDeadzone(raw, bán kính)</b>.",
        };

        public override string Code =>
@"Vector2 move = tis.MoveDirection; // cần trái
Vector2 look = tis.LookDelta;     // cần phải

// LookDelta tay cầm là tốc độ
// → nhân deltaTime
_yaw   += look.x * degPerSec * Time.deltaTime;
_pitch -= look.y * degPerSec * Time.deltaTime;

// Lọc deadzone cho giá trị tự đọc
Vector2 v = tis.ApplyDeadzone(raw, 0.2f);";

        public override string[] Objectives => new[]
        {
            "Đi tới vòng tròn xanh bằng cần trái",
            "Xoay camera một vòng bằng cần phải",
            "Đẩy cần trái hết cỡ (magnitude ≈ 1)",
        };

        public override string FooterHint => "Cần trái đi · cần phải xoay camera · thả tay ra xem deadzone giữ về 0";

        TISTutZone _zone;
        RectTransform _lRaw, _lTis, _rRaw, _rTis;

        protected override void OnEnter()
        {
            PG.Toys.gameObject.SetActive(false);
            GamepadCamera();
            _zone = PG.Zone("Zone Xanh", PG.mint, new Vector3(-5f, 0f, 5f), 1.3f, Stage);

            GamepadWidget(new Vector2(0f, 0f), new Vector2(20f, 20f), 0.75f);
            StickCard("CẦN TRÁI",  new Vector2(-196f, -18f), out _lRaw, out _lTis);
            StickCard("CẦN PHẢI",  new Vector2(-18f, -18f),  out _rRaw, out _rTis);

            var gs = TIS.TISGamepadSettings;
            Watch("MoveDirection", () => F(TIS.MoveDirection), () => Mag(TIS.MoveDirection));
            Watch("LookDelta", () => F(TIS.LookDelta), () => Mathf.Clamp01(TIS.LookDelta.magnitude / 0.25f));
            Watch("leftStick (thô)", () => F(Raw(true)), () => Raw(true).magnitude);
            Watch("leftStickDeadzone", () => gs != null ? F(gs.leftStickDeadzone) : "—");
            Watch("stickCurve", () => gs != null ? F(gs.stickCurve) : "—");
            Watch("Camera đã xoay", () => Mathf.Min(360f, Cam.TotalYaw).ToString("0") + "°", () => Cam.TotalYaw / 360f);
        }

        void StickCard(string title, Vector2 pos, out RectTransform raw, out RectTransform tis)
        {
            var card = TISTutUI.Card(Overlay, title, new Vector2(1f, 1f), pos, new Vector2(166f, 200f));
            var t = TISTutUI.Text(card.transform, "Title", title, Theme.monoBold, 14, Theme.inkSoft, TextAlignmentOptions.Center);
            TISTutUI.Place(t.rectTransform, 8f, 10f, 8f, -1f, height: 22f);
            var ring = TISTutUI.Shape(card.transform, "Ring", Theme.paper, 60f, 2f, Theme.line);
            TISTutUI.At(ring.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(120f, 120f));
            ring.rectTransform.pivot = new Vector2(0.5f, 0f);
            raw = Dot(ring.transform, Theme.ink, 16f);
            tis = Dot(ring.transform, Theme.AccentStrong(), 22f);
        }

        RectTransform Dot(Transform parent, Color c, float size)
        {
            var d = TISTutUI.Shape(parent, "Dot", c, size * 0.5f);
            var rt = d.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            return rt;
        }

        static Vector2 Raw(bool left)
        {
            var gp = Gamepad.current;
            if (gp == null) return Vector2.zero;
            return left ? gp.leftStick.ReadValue() : gp.rightStick.ReadValue();
        }

        protected override void OnTick()
        {
            const float r = 52f;
            _lRaw.anchoredPosition = Raw(true) * r;
            _rRaw.anchoredPosition = Raw(false) * r;
            _lTis.anchoredPosition = TIS.MoveDirection * r;
            _rTis.anchoredPosition = Vector2.ClampMagnitude(TIS.LookDelta / 0.25f, 1f) * r;

            if (_zone.Reached) Complete(0);
            if (Cam.TotalYaw >= 360f) Complete(1);
            if (TIS.MoveDirection.magnitude >= 0.97f && TIS.IsGamepad) Complete(2);
        }
    }
}
