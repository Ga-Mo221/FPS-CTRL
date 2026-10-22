// ============================================================
//  TrueInputSystem Tutorials — TISTutLesson.cs
//
//  Lớp nền cho mọi bài học. Mỗi bài là một MonoBehaviour con của
//  "Lessons" trong scene — thứ tự con = thứ tự bài.
//
//  Bài học khai báo nội dung bằng code (dễ bảo trì, dễ dịch):
//    Title      — tên bài (ngắn)
//    Summary    — bài này dạy gì, áp dụng vào game ra sao
//    SetupSteps — các bước setup (mỗi phần tử 1 bước)
//    Code       — code mẫu (hiện ở tab "Code mẫu", có nút Sao chép)
//    Objectives — nhiệm vụ "Thử ngay"; gọi Complete(i) khi người chơi làm được
//
//  Vòng đời: OnEnter → OnTick (mỗi frame) → OnExit.
//  Watch(...) đăng ký dòng hiển thị giá trị API theo thời gian thực.
// ============================================================

using System;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    public abstract class TISTutLesson : MonoBehaviour
    {
        [Tooltip("Đồ vật / UI riêng của bài — chỉ bật khi bài đang mở.")]
        [SerializeField] GameObject _playground;

        // ── Nội dung ──────────────────────────────────────────

        public abstract string   Title      { get; }
        public abstract string   Summary    { get; }
        public virtual  string[] SetupSteps => Array.Empty<string>();
        public virtual  string   Code       => "";
        public virtual  string[] Objectives => Array.Empty<string>();
        /// <summary>Gợi ý ở chân trang (phím tắt, mẹo).</summary>
        public virtual  string   FooterHint => "";

        // ── Truy cập ──────────────────────────────────────────

        protected TISInputBridge    TIS        => TISInputBridge.Instance;
        protected TISTutController  Controller { get; private set; }
        protected TISTutTheme       Theme      => TISTutTheme.Current;
        protected TISTutPlayground  PG         => TISTutPlayground.Instance;
        protected TISTutPlayer      Player     => PG != null ? PG.Player : null;
        protected TISTutOrbitCamera Cam        => PG != null ? PG.Camera : null;
        /// <summary>Bộ nút cảm ứng (chỉ có trong scene Mobile).</summary>
        protected TISTutMobileHud   Hud        => TISTutMobileHud.Instance;

        Transform     _stage;
        RectTransform _overlay;

        /// <summary>Gốc cho đồ vật 3D riêng của bài — tự xoá khi rời bài.</summary>
        protected Transform Stage
        {
            get
            {
                if (_stage == null) _stage = new GameObject($"Stage · {Title}").transform;
                return _stage;
            }
        }

        /// <summary>Gốc cho UI riêng của bài (đè lên sân chơi) — tự xoá khi rời bài.</summary>
        protected RectTransform Overlay
        {
            get
            {
                if (_overlay == null && Controller != null && Controller.OverlayRoot != null)
                {
                    _overlay = TISTutUI.Rect(Controller.OverlayRoot, $"Overlay · {Title}");
                    TISTutUI.Stretch(_overlay);
                }
                return _overlay;
            }
        }

        /// <summary>
        /// Cụm phím minh hoạ ở góc sân chơi. keys: (nhãn, controlPath, rộng).
        /// anchor (0,0) = dưới-trái, (1,0) = dưới-phải...
        /// </summary>
        protected RectTransform KeyRow(Vector2 anchor, Vector2 pos, params (string label, string path, float width)[] keys)
        {
            var row = TISTutUI.Row(Overlay, "Keys", 8f);
            row.anchorMin = row.anchorMax = row.pivot = anchor;
            row.anchoredPosition = pos;
            foreach (var k in keys)
                TISTutUI.KeyCap(row, k.label, k.path, k.width <= 0f ? 56f : k.width);
            return row;
        }

        public GameObject Playground { get => _playground; set => _playground = value; }

        bool[] _done = Array.Empty<bool>();
        public bool IsActive { get; private set; }

        public int  ObjectiveCount         => _done.Length;
        public bool IsObjectiveDone(int i) => i >= 0 && i < _done.Length && _done[i];

        public bool AllDone
        {
            get
            {
                for (int i = 0; i < _done.Length; i++) if (!_done[i]) return false;
                return true;
            }
        }

        /// <summary>Đánh dấu nhiệm vụ thứ i đã hoàn thành (gọi nhiều lần không sao).</summary>
        protected void Complete(int index)
        {
            if (!IsActive || index < 0 || index >= _done.Length || _done[index]) return;
            _done[index] = true;
            Controller.NotifyObjective(this, index);
        }

        /// <summary>Thêm một dòng vào bảng "API đang chạy".</summary>
        protected void Watch(string apiName, Func<string> value, Func<float> bar01 = null)
            => Controller.AddWatch(apiName, value, bar01);

        /// <summary>Hiện thông báo nhỏ trên sân chơi (vd. "Đã nhảy!").</summary>
        protected void Toast(string message) => Controller.Toast(message);

        // ── Vòng đời (controller gọi) ─────────────────────────

        internal void Enter(TISTutController controller)
        {
            Controller = controller;
            _done = new bool[Objectives.Length];
            IsActive = true;
            if (PG != null) PG.ResetForLesson();
            if (Hud != null) { Hud.HideAll(); Hud.SetVisible(true); SetMobileJoystick(true); }
            if (_playground != null) _playground.SetActive(true);
            OnEnter();
        }

        internal void Tick()
        {
            if (IsActive) OnTick();
        }

        internal void Exit()
        {
            if (!IsActive) return;
            // Thoát Play / đổi scene: bridge có thể đã bị huỷ trước → bỏ qua phần trả input
            if (TIS != null) OnExit();
            IsActive = false;
            if (_playground != null) _playground.SetActive(false);
            if (_stage != null)   { Destroy(_stage.gameObject);   _stage = null; }
            if (_overlay != null) { Destroy(_overlay.gameObject); _overlay = null; }
        }

        protected virtual void OnEnter() { }
        protected virtual void OnTick()  { }
        protected virtual void OnExit()  { }

        /// <summary>Hình tay cầm minh hoạ trực tiếp (nút sáng, cần di chuyển, cò có thanh mức).</summary>
        protected TISTutGamepadWidget GamepadWidget(Vector2 anchor, Vector2 pos, float scale = 0.85f)
            => TISTutGamepadWidget.Create(Overlay, anchor, pos, scale);

        /// <summary>
        /// Bật / tắt joystick ảo nửa trái (scene Mobile). Tắt → vùng vuốt camera phủ cả sân chơi.
        /// Mỗi lần vào bài, joystick được bật lại.
        /// </summary>
        protected void SetMobileJoystick(bool on)
        {
            var setup = FindAnyObjectByType<TISMobileInputSetup>();
            if (setup == null || !setup.IsReady) return;
            setup._batJoystick = on;
            setup.ApDungToggleJoystickCamera();          // bật / tắt joystick + trả vùng camera về tỉ lệ gốc
            if (!on)
            {
                setup.CameraZone?.SetZoneStartRatio(0f);  // camera nhận chạm cả nửa trái
                TIS?.GestureSyncZone(0f);
            }
        }

        /// <summary>Camera cho bài tay cầm: xoay không cần giữ chuột phải.</summary>
        protected void GamepadCamera(bool look = true, bool zoom = false)
        {
            Cam.LookEnabled = look;
            Cam.ZoomEnabled = zoom;
            Cam.RequireRightMouse = false;
        }

        /// <summary>Cụm W / A S D (2 hàng) ở góc sân chơi.</summary>
        protected RectTransform WasdCluster(Vector2 anchor, Vector2 pos, float key = 56f)
        {
            const float gap = 8f;
            var box = TISTutUI.Rect(Overlay, "WASD");
            TISTutUI.At(box, anchor, pos, new Vector2(key * 3f + gap * 2f, key * 2f + gap));
            void K(string l, string p, float x, float y)
            {
                var kc = TISTutUI.KeyCap(box, l, p, key, key);
                TISTutUI.At((RectTransform)kc.transform, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(key, key));
            }
            K("W", "<Keyboard>/w", key + gap, 0f);
            K("A", "<Keyboard>/a", 0f, key + gap);
            K("S", "<Keyboard>/s", key + gap, key + gap);
            K("D", "<Keyboard>/d", (key + gap) * 2f, key + gap);
            return box;
        }

        /// <summary>
        /// Giữ "true" thêm một chút cho giá trị chỉ đúng 1 frame (IsSkillDown, IsSkillReleased...)
        /// để mắt kịp thấy trên bảng API.
        /// </summary>
        protected sealed class Pulse
        {
            float _until = -1f;
            public int Count { get; private set; }
            public void Hit() { _until = Time.unscaledTime + 0.35f; Count++; }
            public bool On => Time.unscaledTime <= _until;
            public string Text => On ? "true" : "false";
        }

        // ── Định dạng giá trị cho bảng live ──────────────────

        // InvariantCulture: máy vi-VN dùng dấu phẩy thập phân → "(0,50, 1,00)" khó đọc
        static readonly IFormatProvider Inv = System.Globalization.CultureInfo.InvariantCulture;

        protected static string F(Vector2 v) => string.Format(Inv, "({0:0.00}, {1:0.00})", v.x, v.y);
        protected static string F(Vector3 v) => string.Format(Inv, "({0:0.0}, {1:0.0}, {2:0.0})", v.x, v.y, v.z);
        protected static string F(float v)   => v.ToString("0.00", Inv);
        protected static string F(bool v)    => v ? "true" : "false";
        protected static float  Mag(Vector2 v) => Mathf.Clamp01(v.magnitude);
    }
}
