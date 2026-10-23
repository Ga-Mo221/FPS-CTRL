// ============================================================
//  TrueInputSystem Tutorials — Mobile · Kỹ năng định hướng (TISDirectionalSkillButton)
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Mobile · Kỹ năng định hướng")]
    public class TISTutMb_Directional : TISTutLesson
    {
        public override string Title => "Kỹ năng định hướng (MOBA)";

        public override string Summary =>
            "TISDirectionalSkillButton kiểu Liên Quân / Tốc Chiến: giữ và kéo để chọn hướng, thả để tung chiêu, chạm nhanh để tự nhắm, kéo vào ✕ rồi thả để huỷ. Hướng đọc qua GetVector2Action như một action Vector2 bình thường.";

        public override string[] SetupSteps => new[]
        {
            "<b>Skill Name</b> = action Button (<b>Fireball</b>) · <b>Direction Action Name</b> = action Vector2 (<b>Fireball_Dir</b>).",
            "Gán <b>Cancel Zone</b> (RectTransform + Image) — nút tự bật vùng huỷ khi đang kéo.",
            "Inspector: <b>Cast Mode</b>, <b>Max Drag Radius</b>, <b>Allow Quick Tap</b>, kiểu mũi tên chỉ hướng.",
            "Mũi tên trên mặt đất quanh nhân vật do game tự vẽ: đọc <b>Direction</b> / <b>IsDragging</b> / <b>IsInCancelZone</b> mỗi frame (xem code mẫu).",
        };

        public override string Code =>
@"if (tis.IsSkillDown(""Fireball""))
{
    Vector2 d = tis.GetVector2Action(
        ""Fireball_Dir"");
    if (d == Vector2.zero)      // chạm nhanh
        d = AutoAim();
    Shoot(d);
}

// Mũi tên chỉ hướng quanh nhân vật khi đang kéo
arrow.SetActive(fireBtn.IsDragging);
Vector2 d2 = fireBtn.Direction;
arrow.forward = cam.right * d2.x
              + camForwardFlat * d2.y;

fireBtn.OnCancelled.AddListener(
    () => Debug.Log(""Huỷ chiêu""));";

        public override string[] Objectives => new[]
        {
            "Kéo nút CẦU LỬA về phía bia rồi thả để bắn trúng",
            "Chạm nhanh nút để tự nhắm bia gần nhất",
            "Kéo vào ✕ rồi thả để huỷ chiêu",
        };

        public override string FooterHint => "Giữ CẦU LỬA + kéo để nhắm (mũi tên hiện quanh nhân vật) · thả để bắn · kéo lên ✕ HUỶ ở góc trên-phải để huỷ";

        readonly List<Transform> _targets = new List<Transform>();
        readonly List<(Transform t, Vector3 v, float life)> _shots = new List<(Transform, Vector3, float)>();
        Vector2 _lastDir;
        bool _autoAim;
        Transform _arrow;
        Renderer[] _arrowParts;

        protected override void OnEnter()
        {
            PG.Toys.gameObject.SetActive(false);
            Hud.Show(showFireball: true);
            Cam.LookEnabled = true;
            Cam.RequireRightMouse = false;
            _targets.Clear(); _shots.Clear();

            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector3(-6f + i * 6f, 1f, 7f + (i == 1 ? 2f : 0f));
                var t = PG.Spawn(PrimitiveType.Capsule, "Bia " + (i + 1), PG.lilac, pos, Vector3.one * 1.1f, Stage);
                _targets.Add(t.transform);
            }

            Hud.fireball.OnCancelled.AddListener(OnCancelled);

            // Mũi tên chỉ hướng trên mặt đất quanh nhân vật (thân + đầu)
            _arrow = new GameObject("Aim Arrow").transform;
            _arrow.SetParent(Stage, false);
            var shaft = PG.Spawn(PrimitiveType.Cube, "Shaft", PG.coral, Vector3.zero, new Vector3(0.35f, 0.05f, 4f), _arrow, collider: false);
            shaft.transform.localPosition = new Vector3(0f, 0f, 2.6f);
            var head = PG.Spawn(PrimitiveType.Cube, "Head", PG.coral, Vector3.zero, new Vector3(1f, 0.05f, 1f), _arrow, collider: false);
            head.transform.localPosition = new Vector3(0f, 0f, 4.8f);
            head.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            var ring = PG.Spawn(PrimitiveType.Cylinder, "Ring", PG.coral, Vector3.zero, new Vector3(2.2f, 0.02f, 2.2f), _arrow, collider: false);
            _arrowParts = _arrow.GetComponentsInChildren<Renderer>();
            _arrow.gameObject.SetActive(false);

            Watch("GetVector2Action(\"Fireball_Dir\")", () => F(TIS.GetVector2Action("Fireball_Dir")));
            Watch("fireball.IsDragging", () => F(Hud.fireball.IsDragging));
            Watch("fireball.Direction", () => F(Hud.fireball.Direction), () => Hud.fireball.Direction.magnitude);
            Watch("fireball.IsInCancelZone", () => F(Hud.fireball.IsInCancelZone));
            Watch("Hướng lần bắn trước", () => F(_lastDir));
        }

        void OnCancelled()
        {
            Toast("Đã huỷ chiêu");
            Complete(2);
        }

        protected override void OnTick()
        {
            var tis = TIS;

            // Mũi tên hướng: hiện khi đang kéo, chuyển xám khi ở vùng huỷ
            var fb = Hud.fireball;
            bool aiming = fb.IsDragging && fb.Direction.sqrMagnitude > 0.01f;
            _arrow.gameObject.SetActive(aiming);
            if (aiming)
            {
                _arrow.position = new Vector3(Player.transform.position.x, 0.06f, Player.transform.position.z);
                _arrow.rotation = Quaternion.LookRotation(ToWorld(fb.Direction), Vector3.up);
                var mat = fb.IsInCancelZone ? PG.ink : PG.coral;
                foreach (var r in _arrowParts) r.sharedMaterial = mat;
            }

            if (tis.IsSkillDown("Fireball"))
            {
                Vector2 d = tis.GetVector2Action("Fireball_Dir");
                if (d == Vector2.zero) d = Hud.fireball.CastDirection;
                _autoAim = d.sqrMagnitude < 0.01f;
                Vector3 world = _autoAim ? AutoAim() : ToWorld(d);
                _lastDir = d;
                Shoot(world);
                if (_autoAim) { Toast("Tự nhắm!"); Complete(1); }
            }

            // Cầu lửa bay + va chạm bia
            float dt = Time.deltaTime;
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                var s = _shots[i];
                if (s.t == null) { _shots.RemoveAt(i); continue; }
                s.t.position += s.v * dt;
                s.life -= dt;
                bool hit = false;
                foreach (var tg in _targets)
                    if (tg.gameObject.activeSelf && (tg.position - s.t.position).sqrMagnitude < 2.6f)
                    {
                        hit = true;
                        tg.localScale = Vector3.one * 1.5f;
                        Toast("Trúng " + tg.name + "!");
                        if (!_autoAim) Complete(0);
                    }
                if (hit || s.life <= 0f) { Destroy(s.t.gameObject); _shots.RemoveAt(i); }
                else _shots[i] = s;
            }
            foreach (var tg in _targets)
                tg.localScale = Vector3.Lerp(tg.localScale, Vector3.one * 1.1f, 1f - Mathf.Exp(-6f * dt));
        }

        /// <summary>Hướng kéo trên màn hình (x phải, y lên) → hướng trong thế giới theo camera.</summary>
        Vector3 ToWorld(Vector2 d)
        {
            Transform c = Cam.transform;
            Vector3 f = c.forward; f.y = 0f; f.Normalize();
            Vector3 r = c.right;   r.y = 0f; r.Normalize();
            return (r * d.x + f * d.y).normalized;
        }

        Vector3 AutoAim()
        {
            Transform best = null; float bd = float.MaxValue;
            foreach (var t in _targets)
            {
                float d = (t.position - Player.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = t; }
            }
            if (best == null) return Player.transform.forward;
            Vector3 v = best.position - Player.transform.position; v.y = 0f;
            return v.normalized;
        }

        void Shoot(Vector3 dir)
        {
            var p = Player.transform.position + Vector3.up * 0.1f + dir * 0.8f;
            var ball = PG.Spawn(PrimitiveType.Sphere, "Cầu lửa", PG.coral, p, Vector3.one * 0.6f, Stage, collider: false);
            _shots.Add((ball.transform, dir * 16f, 1.6f));
        }

        protected override void OnExit()
        {
            if (Hud != null && Hud.fireball != null) Hud.fireball.OnCancelled.RemoveListener(OnCancelled);
        }
    }
}
