// ============================================================
//  TrueInputSystem Tutorials — Tay cầm · Aim assist (AimAssistProcess + TISTargetScanner)
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Tay cầm · Aim assist")]
    public class TISTutGp_AimAssist : TISTutLesson
    {
        public override string Title => "Aim assist: hỗ trợ ngắm";

        public override string Summary =>
            "Ngắm bằng cần analog khó hơn chuột. AimAssistProcess kéo nhẹ tâm ngắm về phía mục tiêu khi người chơi đang xoay gần nó — không tự ngắm hộ. TISTargetScanner trên camera tìm mục tiêu gần tâm màn hình nhất để đưa vào.";

        public override string[] SetupSteps => new[]
        {
            "Thêm <b>TISTargetScanner</b> vào camera: Tag / Layer của mục tiêu, góc quét, khoảng cách, có cần nhìn thấy không.",
            "Mỗi frame: tính độ xoay từ LookDelta → <b>tis.AimAssistProcess(settings, cam, scanner.CurrentTarget, độXoay)</b> → xoay camera bằng kết quả.",
            "<b>AimAssistSettings</b>: SnapRadius (độ), AssistSpeed, AssistWithoutInput, EnabledPlatforms (thường chỉ bật cho Gamepad).",
        };

        public override string Code =>
@"var settings = new AimAssistSettings(
    snapRadius: 8f, assistSpeed: 45f,
    enabledPlatforms: AimAssistPlatform.Gamepad);

Vector2 deg = tis.LookDelta * degPerSec
            * Time.deltaTime;

deg = tis.AimAssistProcess(settings,
    cam.transform, scanner.CurrentTarget,
    deg, intensity: 0.8f);

_yaw += deg.x; _pitch += deg.y;";

        public override string[] Objectives => new[]
        {
            "Đưa tâm ngắm vào 3 bia khác nhau (tâm đổi màu)",
            $"Bắn trúng một bia bằng {TISTutGlyphs.RT}",
            $"Nhấn {TISTutGlyphs.North} tắt aim assist, cảm nhận rồi bật lại",
        };

        public override string FooterHint => "Xoay chậm qua bia để cảm nhận lực hút · Y bật / tắt aim assist";

        // AssistSpeed tính theo độ / giây trên mỗi độ lệch — 45 để lực kéo rõ so với tốc độ xoay cần (~180°/s)
        readonly AimAssistSettings _settings = new AimAssistSettings(8f, 45f, false, AimAssistPlatform.All);
        readonly HashSet<Transform> _aimed = new HashSet<Transform>();
        readonly List<Transform> _targets = new List<Transform>();
        TISTargetScanner _scanner;
        TISTutShape _cross;
        bool _assist = true, _turnedOff;

        protected override void OnEnter()
        {
            PG.Toys.gameObject.SetActive(false);
            GamepadCamera();
            Player.CanMove = false;
            Cam.shoulder = new Vector2(1.1f, 0.5f);
            Cam.distance = 4.5f;
            Cam.pitch = 6f;
            _assist = true; _turnedOff = false;
            _aimed.Clear(); _targets.Clear();

            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.Lerp(-55f, 55f, i / 4f) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(a) * 11f, 1.6f + (i % 2) * 1.2f, Mathf.Cos(a) * 11f);
                var t = PG.Spawn(PrimitiveType.Cylinder, "Bia " + (i + 1), i % 2 == 0 ? PG.coral : PG.sun,
                                 pos, new Vector3(1.4f, 0.12f, 1.4f), Stage);
                t.transform.LookAt(new Vector3(0f, pos.y, 0f));
                t.transform.Rotate(90f, 0f, 0f);
                t.tag = "Finish";
                _targets.Add(t.transform);
            }

            _scanner = Cam.GetComponent<TISTargetScanner>();
            if (_scanner != null) _scanner.enabled = true;
            Cam.LookFilter = deg => _assist && _scanner != null
                ? TIS.AimAssistProcess(_settings, Cam.transform, _scanner.CurrentTarget, deg, 0.8f)
                : deg;

            // Tâm ngắm giữa sân chơi
            _cross = TISTutUI.Shape(Overlay, "Crosshair", Theme.card, 9f, 3f, Theme.ink);
            TISTutUI.At(_cross.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));

            GamepadWidget(new Vector2(0f, 0f), new Vector2(20f, 20f), 0.7f);

            Watch("Aim assist", () => _assist ? "BẬT" : "TẮT");
            Watch("scanner.CurrentTarget (vùng hút)", () => _scanner != null && _scanner.CurrentTarget != null ? _scanner.CurrentTarget.name : "—");
            Watch("Tâm đang trên bia", () => AimedTarget() != null ? AimedTarget().name : "—");
            Watch("scanner.CurrentAngle", () => _scanner != null && _scanner.CurrentAngle >= 0f ? _scanner.CurrentAngle.ToString("0.0") + "°" : "—");
            Watch("SnapRadius", () => _settings.SnapRadius.ToString("0") + "°");
            Watch("Bia đã ngắm", () => _aimed.Count + " / 3", () => _aimed.Count / 3f);
        }

        protected override void OnTick()
        {
            var tis = TIS;
            // Tâm chỉ đổi màu khi tia từ giữa màn hình chạm đúng bia
            // (scanner quét cả vùng 12° quanh tâm — dùng cho lực hút, không dùng để báo "trúng").
            Transform target = AimedTarget();
            bool onTarget = target != null;
            _cross.color = onTarget ? Theme.AccentStrong() : Theme.card;

            if (onTarget)
            {
                _aimed.Add(target);
                if (_aimed.Count >= 3) Complete(0);
            }

            if (tis.IsSkillDown("Attack"))
            {
                if (onTarget)
                {
                    target.localScale = new Vector3(1.9f, 0.12f, 1.9f);
                    Toast("Trúng " + target.name + "!");
                    Complete(1);
                }
                else Toast("Trượt");
            }
            foreach (var t in _targets)
                t.localScale = Vector3.Lerp(t.localScale, new Vector3(1.4f, 0.12f, 1.4f), 1f - Mathf.Exp(-6f * Time.deltaTime));

            if (tis.IsSkillDown("Toggle"))
            {
                _assist = !_assist;
                Toast(_assist ? "Aim assist: BẬT" : "Aim assist: TẮT");
                if (!_assist) _turnedOff = true;
                else if (_turnedOff) Complete(2);
            }
        }

        Transform AimedTarget()
        {
            var cam = Cam.GetComponent<Camera>();
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out var hit, 60f) && _targets.Contains(hit.transform)) return hit.transform;
            return null;
        }

        protected override void OnExit()
        {
            if (_scanner != null) { _scanner.ClearTarget(); _scanner.enabled = false; }
        }
    }
}
