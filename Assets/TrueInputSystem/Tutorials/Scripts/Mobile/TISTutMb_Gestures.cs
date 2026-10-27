// ============================================================
//  TrueInputSystem Tutorials — Mobile · Cử chỉ nhiều ngón (Gesture API)
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Mobile · Cử chỉ nhiều ngón")]
    public class TISTutMb_Gestures : TISTutLesson
    {
        public override string Title => "Cử chỉ nhiều ngón";

        public override string Summary =>
            "Gesture detector theo dõi nhiều ngón cùng lúc: số ngón, tâm các ngón, kéo 2 ngón (pan bản đồ kiểu game chiến thuật), vị trí từng ngón. Cần màn hình cảm ứng thật — chuột trong Editor chỉ là một ngón; thử bằng Unity Remote 5 hoặc bản build.";

        public override string[] SetupSteps => new[]
        {
            "TISMobileInputSetup tự tạo <b>TISMultiFingerGestureDetector</b> và đăng ký với bridge.",
            "Đọc qua bridge: <b>GestureActiveFingers</b>, <b>GestureIsTwoFingerPanning</b>, <b>GestureTwoFingerPanDelta</b>, <b>GestureCentroidPosition</b>, <b>GestureGetFingerPosition(i)</b>.",
            "Giới hạn vùng nhận cử chỉ: <b>GestureSyncZone(start, end)</b> (tỉ lệ bề ngang màn hình).",
        };

        public override string Code =>
@"int n = tis.GestureActiveFingers;

// Kéo 2 ngón → di chuyển bản đồ
if (tis.GestureIsTwoFingerPanning)
    mapCam.position -= (Vector3)
        tis.GestureTwoFingerPanDelta * k;

Vector2 c = tis.GestureCentroidPosition;
Vector2 f0 = tis.GestureGetFingerPosition(0);";

        public override string[] Objectives => new[]
        {
            "Đặt 1 ngón lên sân chơi",
            "Đặt 2 ngón cùng lúc",
            "Kéo 2 ngón để di chuyển bản đồ",
        };

        public override string FooterHint => "Bài này cần màn hình cảm ứng thật (Unity Remote 5 / bản build)";

        readonly List<RectTransform> _dots = new List<RectTransform>();
        Transform _map;
        float _panned;

        protected override void OnEnter()
        {
            PG.Toys.gameObject.SetActive(false);
            Player.CanMove = false;
            SetMobileJoystick(false);      // cử chỉ nhiều ngón dùng cả sân chơi
            _panned = 0f;
            _dots.Clear();

            // "Bản đồ" = lưới ô màu trên mặt đất, kéo 2 ngón để trượt
            _map = new GameObject("Map").transform;
            _map.SetParent(Stage);
            Material[] mats = { PG.sun, PG.mint, PG.lilac, PG.coral };
            for (int x = -2; x <= 2; x++)
                for (int z = 0; z <= 3; z++)
                    PG.Spawn(PrimitiveType.Cube, "Tile", mats[(x + z + 8) % 4], new Vector3(x * 3f, 0.1f, z * 3f + 2f),
                             new Vector3(2.6f, 0.2f, 2.6f), _map, collider: false);

            for (int i = 0; i < 5; i++)
            {
                var d = TISTutUI.Shape(Overlay, "Finger " + i, Theme.AccentStrong(), 30f, 3f, Theme.ink);
                var rt = d.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(60f, 60f);
                d.raycastTarget = false;
                d.gameObject.SetActive(false);
                _dots.Add(rt);
            }

            Watch("GestureActiveFingers", () => TIS.GestureActiveFingers.ToString(), () => TIS.GestureActiveFingers / 2f);
            Watch("GestureIsTwoFingerPanning", () => F(TIS.GestureIsTwoFingerPanning));
            Watch("GestureTwoFingerPanDelta", () => F(TIS.GestureTwoFingerPanDelta));
            Watch("GestureCentroidPosition", () => F(TIS.GestureCentroidPosition));
            Watch("Touchscreen", () => UnityEngine.InputSystem.Touchscreen.current != null ? "có" : "không (Editor chỉ có chuột)");
        }

        protected override void OnTick()
        {
            var tis = TIS;
            int n = tis.GestureActiveFingers;
            if (n >= 1) Complete(0);
            if (n >= 2) Complete(1);

            var overlay = Overlay;
            for (int i = 0; i < _dots.Count; i++)
            {
                bool on = i < n;
                _dots[i].gameObject.SetActive(on);
                if (!on) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, tis.GestureGetFingerPosition(i), null, out var local);
                _dots[i].anchoredPosition = local + overlay.rect.size * 0.5f;
            }

            if (tis.GestureIsTwoFingerPanning)
            {
                Vector2 d = tis.GestureTwoFingerPanDelta;
                _map.position += new Vector3(d.x, 0f, d.y) * 0.02f;
                _panned += d.magnitude;
                if (_panned > 200f) Complete(2);
            }
        }
    }
}
