// ============================================================
//  TrueInputSystem Tutorials — Mobile · Chạm chọn vật (Object Tracker)
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Mobile · Chạm chọn vật")]
    public class TISTutMb_Toucher : TISTutLesson
    {
        public override string Title => "Chạm chọn vật";

        public override string Summary =>
            "Object Tracker raycast theo ngón tay: biết vật nào vừa được chạm, chạm đúp vào vật, chạm đúp ra khoảng trống, điểm chạm trong thế giới. Dùng cho chọn đơn vị, nhặt đồ, chạm đúp để chạy tới — cùng API với click chuột trên PC.";

        public override string[] SetupSteps => new[]
        {
            "TISInputBridge › <b>Enable Object Tracker</b> (hoặc <b>SetObjectTrackerEnabled(true)</b>) · gán <b>Tracker Camera</b>.",
            "Vật phải có <b>Collider</b>. Lọc bằng <b>Tracker Layer Mask</b>; game 2D bật <b>Use 2D Physics</b>.",
            "Chạm đúp: <b>Double Tap Interval</b> (giây) và <b>Double Tap Distance</b> (pixel).",
        };

        public override string Code =>
@"GameObject hit = tis.ToucherGetSingleObject();
if (hit != null) Select(hit);

if (tis.ToucherIsDoubleTapOnObject(
        chest.GetInstanceID()))
    OpenChest();

// Chạm đúp mặt đất → chạy tới đó
var obj = tis.ToucherGetDoubleTappedObject();
if (tis.ToucherIsDoubleTapOnScreen()
    && (obj == null || obj == ground))
    MoveTo(tis.ToucherWorldPosition());";

        public override string[] Objectives => new[]
        {
            "Chạm chọn 3 vật khác nhau",
            "Chạm đúp vào rương vàng để mở",
            "Chạm đúp ra bãi cỏ trống — nhân vật chạy tới đó",
        };

        public override string FooterHint => "Chạm / chạm đúp vào đồ vật và mặt đất";

        readonly HashSet<int> _picked = new HashSet<int>();
        readonly List<GameObject> _items = new List<GameObject>();
        GameObject _chest, _lid;
        Vector3? _moveTo;
        Transform _marker;

        protected override void OnEnter()
        {
            PG.Toys.gameObject.SetActive(false);
            Cam.LookEnabled = true;
            Cam.RequireRightMouse = false;
            _picked.Clear(); _items.Clear(); _moveTo = null;
            TIS.SetObjectTrackerEnabled(true);
            TIS.ToucherClearAll();

            _items.Add(PG.Spawn(PrimitiveType.Sphere,   "Quả cầu tím", PG.lilac, new Vector3(-5f, 0.9f, 5f), Vector3.one * 1.6f, Stage));
            _items.Add(PG.Spawn(PrimitiveType.Cylinder, "Trụ xanh",    PG.mint,  new Vector3(5f, 1f, 6f),    new Vector3(1.2f, 1f, 1.2f), Stage));
            _items.Add(PG.Spawn(PrimitiveType.Cube,     "Hộp cam",     PG.coral, new Vector3(-2.5f, 0.6f, 8f), Vector3.one * 1.2f, Stage));
            _chest = PG.Spawn(PrimitiveType.Cube, "Rương vàng", PG.sun, new Vector3(2.5f, 0.6f, 4f), new Vector3(1.8f, 1.2f, 1.2f), Stage);
            _lid = PG.Spawn(PrimitiveType.Cube, "Nắp", PG.ink, new Vector3(2.5f, 1.3f, 4f), new Vector3(1.9f, 0.2f, 1.3f), Stage, collider: false);
            _items.Add(_chest);

            _marker = PG.Spawn(PrimitiveType.Cylinder, "Marker", PG.ink, Vector3.zero, new Vector3(0.6f, 0.02f, 0.6f), Stage, collider: false).transform;
            _marker.gameObject.SetActive(false);

            Watch("ToucherGetLastName()", () => string.IsNullOrEmpty(TIS.ToucherGetLastName()) ? "—" : TIS.ToucherGetLastName());
            Watch("ToucherIsSingleTouch()", () => F(TIS.ToucherIsSingleTouch()));
            Watch("ToucherWorldPosition()", () => F(TIS.ToucherWorldPosition()));
            Watch("ToucherCount", () => TIS.ToucherCount.ToString());
            Watch("Vật đã chọn", () => _picked.Count + " / 3", () => _picked.Count / 3f);
        }

        protected override void OnTick()
        {
            var tis = TIS;

            var hit = tis.ToucherGetSingleObject();
            if (hit != null && _items.Contains(hit))
            {
                _picked.Add(hit.GetInstanceID());
                Toast("Đã chọn: " + hit.name);
                if (_picked.Count >= 3) Complete(0);
            }

            if (tis.ToucherIsDoubleTapOnObject(_chest.GetInstanceID()))
            {
                _lid.transform.rotation = Quaternion.Euler(-70f, 0f, 0f);
                _lid.transform.position = _chest.transform.position + new Vector3(0f, 1.2f, -0.6f);
                Toast("Mở rương!");
                Complete(1);
            }

            // Mặt đất cũng có collider → chạm đúp bãi cỏ trả về Ground. Chỉ bỏ qua khi chạm đúp vào đồ vật.
            var dbl = tis.ToucherGetDoubleTappedObject();
            if (tis.ToucherIsDoubleTapOnScreen() && (dbl == null || !_items.Contains(dbl)))
            {
                Vector3 wp = tis.ToucherWorldPosition();
                if (wp != Vector3.zero)
                {
                    _moveTo = new Vector3(wp.x, 0f, wp.z);
                    _marker.gameObject.SetActive(true);
                    _marker.position = new Vector3(wp.x, 0.03f, wp.z);
                }
            }

            // Chạy tới điểm chạm đúp bằng override (giống bài tự chạy)
            if (_moveTo.HasValue)
            {
                Vector3 to = _moveTo.Value - Player.transform.position; to.y = 0f;
                if (to.magnitude < 0.8f)
                {
                    _moveTo = null;
                    tis.ClearMoveOverride();
                    _marker.gameObject.SetActive(false);
                    Complete(2);
                }
                else
                {
                    Transform c = Cam.transform;
                    Vector3 f = c.forward; f.y = 0f; Vector3 r = c.right; r.y = 0f;
                    tis.OverrideMoveInput(new Vector2(Vector3.Dot(to, r.normalized), Vector3.Dot(to, f.normalized)).normalized);
                }
            }
        }

        protected override void OnExit()
        {
            TIS.ClearMoveOverride();
            TIS.SetObjectTrackerEnabled(false);
            TIS.ToucherClearAll();
        }
    }
}
