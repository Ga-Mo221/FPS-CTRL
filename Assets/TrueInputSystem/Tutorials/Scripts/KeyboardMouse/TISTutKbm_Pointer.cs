// ============================================================
//  TrueInputSystem Tutorials — Chuột + Bàn phím · Con trỏ & Object Tracker
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/KBM · Con trỏ & chọn vật")]
    public class TISTutKbm_Pointer : TISTutLesson
    {
        public override string Title => "Con trỏ chuột & chọn vật";

        public override string Summary =>
            "Bật Object Tracker để TIS raycast theo con trỏ: biết vật nào vừa được click, được double-click, vị trí con trỏ trên màn hình và điểm chạm trong thế giới. Cùng bộ API này nhận chạm tay trên mobile.";

        public override string[] SetupSteps => new[]
        {
            "TISInputBridge › bật <b>Enable Object Tracker</b> (hoặc gọi <b>SetObjectTrackerEnabled(true)</b>) và gán <b>Tracker Camera</b>.",
            "Vật muốn chọn phải có <b>Collider</b>. Lọc bằng <b>Tracker Layer Mask</b>.",
            "Độ nhạy double-click: <b>Double Tap Interval</b> (giây) và <b>Double Tap Distance</b> (pixel) trên bridge.",
        };

        public override string Code =>
@"tis.SetObjectTrackerEnabled(true);

// Vật vừa được click frame này
GameObject hit = tis.ToucherGetSingleObject();
if (hit != null) Select(hit);

// Double-click đúng vật
int id = ball.GetInstanceID();
if (tis.ToucherIsDoubleTapOnObject(id))
    Kick(ball);

Vector2 sp  = tis.GetPointerScreenPosition();
Vector3 pos = tis.ToucherWorldPosition();
bool held   = tis.IsPointerHeld;";

        public override string[] Objectives => new[]
        {
            "Click chọn 3 vật khác nhau",
            "Double-click quả bóng tím để đá nó",
            "Giữ chuột trái và kéo — xem IsPointerHeld",
        };

        public override string FooterHint => "Click / double-click vào đồ vật trên sân chơi";

        readonly HashSet<int> _picked = new HashSet<int>();
        readonly List<Transform> _items = new List<Transform>();
        GameObject _ball;
        Transform _marker;
        float _dragDist;
        Vector3 _ballVel;
        readonly Pulse _dbl = new Pulse();

        protected override void OnEnter()
        {
            PG.Toys.gameObject.SetActive(false);
            _picked.Clear(); _items.Clear(); _dragDist = 0f; _ballVel = Vector3.zero;
            TIS.SetObjectTrackerEnabled(true);
            TIS.ToucherClearAll();

            Add(PG.Spawn(PrimitiveType.Cube,     "Hộp vàng",  PG.sun,   new Vector3(-4f, 0.75f, 3f),  Vector3.one * 1.5f, Stage));
            Add(PG.Spawn(PrimitiveType.Cylinder, "Trụ xanh",  PG.mint,  new Vector3(-1.5f, 1f, 5.5f), new Vector3(1.2f, 1f, 1.2f), Stage));
            Add(PG.Spawn(PrimitiveType.Cube,     "Hộp cam",   PG.coral, new Vector3(2f, 0.6f, 4.5f),  Vector3.one * 1.2f, Stage));
            _ball = PG.Spawn(PrimitiveType.Sphere, "Quả bóng tím", PG.lilac, new Vector3(4.5f, 0.9f, 2f), Vector3.one * 1.8f, Stage);
            Add(_ball);

            _marker = PG.Spawn(PrimitiveType.Cylinder, "Marker", PG.ink, Vector3.zero, new Vector3(0.5f, 0.02f, 0.5f), Stage, collider: false).transform;
            _marker.gameObject.SetActive(false);

            KeyRow(new Vector2(0f, 0f), new Vector2(20f, 20f), ("CHUỘT TRÁI", "<Mouse>/leftButton", 170f));

            Watch("GetPointerScreenPosition()", () => F(TIS.GetPointerScreenPosition()));
            Watch("GetPointerScreenDelta()",    () => F(TIS.GetPointerScreenDelta()), () => Mathf.Clamp01(TIS.GetPointerScreenDelta().magnitude / 30f));
            Watch("IsPointerHeld",              () => F(TIS.IsPointerHeld));
            Watch("ToucherGetLastName()",       () => string.IsNullOrEmpty(TIS.ToucherGetLastName()) ? "—" : TIS.ToucherGetLastName());
            Watch("ToucherCount",               () => TIS.ToucherCount.ToString());
            Watch("Double-click bóng",          () => _dbl.Text);
        }

        void Add(GameObject go) => _items.Add(go.transform);

        protected override void OnTick()
        {
            var tis = TIS;

            var hit = tis.ToucherGetSingleObject();
            if (hit != null && _items.Contains(hit.transform))
            {
                _picked.Add(hit.GetInstanceID());
                hit.transform.localScale *= 1.12f;          // nảy nhẹ khi chọn
                Toast("Đã chọn: " + hit.name);
                if (_picked.Count >= 3) Complete(0);
            }

            if (tis.ToucherIsDoubleTapOnObject(_ball.GetInstanceID()))
            {
                _dbl.Hit();
                Vector3 away = _ball.transform.position - Cam.transform.position;
                away.y = 0f;
                _ballVel = away.normalized * 9f + Vector3.up * 6f;
                Toast("Sút!");
                Complete(1);
            }

            if (tis.IsPointerHeld)
            {
                _dragDist += tis.GetPointerScreenDelta().magnitude;
                if (_dragDist > 300f) Complete(2);
            }

            // Điểm chạm gần nhất trong thế giới
            Vector3 wp = tis.ToucherWorldPosition();
            if (wp != Vector3.zero)
            {
                _marker.gameObject.SetActive(true);
                _marker.position = new Vector3(wp.x, 0.03f, wp.z);
            }

            // Trả kích thước + quả bóng bay
            float dt = Time.deltaTime;
            foreach (var t in _items)
                t.localScale = Vector3.Lerp(t.localScale, BaseScale(t), 1f - Mathf.Exp(-8f * dt));
            if (_ballVel != Vector3.zero)
            {
                _ballVel.y -= 20f * dt;
                var p = _ball.transform.position + _ballVel * dt;
                if (p.y < 0.9f) { p.y = 0.9f; _ballVel.y = -_ballVel.y * 0.4f; _ballVel.x *= 0.85f; _ballVel.z *= 0.85f; }
                if (_ballVel.sqrMagnitude < 0.05f) _ballVel = Vector3.zero;
                _ball.transform.position = p;
            }
        }

        static Vector3 BaseScale(Transform t) => t.name switch
        {
            "Hộp vàng"     => Vector3.one * 1.5f,
            "Trụ xanh"     => new Vector3(1.2f, 1f, 1.2f),
            "Hộp cam"      => Vector3.one * 1.2f,
            _              => Vector3.one * 1.8f,
        };

        protected override void OnExit()
        {
            TIS.SetObjectTrackerEnabled(false);
            TIS.ToucherClearAll();
        }
    }
}
