// ============================================================
//  TrueInputSystem — TISMobileInputSetup.cs
//
//  P3 FIX #21 — Naming Convention:
//  Method nội bộ đang dùng tiếng Việt (KhoiTaoMobileUI, TaoCanvas,
//  DamBaoEventSystem, v.v.) — giữ nguyên để tránh breaking change.
//  TODO P3: Alias sang tiếng Anh trong sprint tiếp theo:
//    KhoiTaoMobileUI    → InitialiseMobileUI
//    DamBaoEventSystem  → EnsureEventSystem
//    TaoCanvas          → CreateCanvas
//    DongBoVungManHinh  → SyncZoneLayout
// ============================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GaMo.TrueInputSystem
{
    [AddComponentMenu("TIS/Demo/TIS Mobile Setup")]
    [DefaultExecutionOrder(-50)]
    public class TISMobileInputSetup : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        //  EDITOR HOOKS  (static delegates — zero cost when null at runtime)
        //
        //  Editor assembly đăng ký vào các hook này qua [InitializeOnLoadMethod].
        //  Khi build DLL (không có Editor assembly), tất cả đều null
        //  → các lệnh gọi bên dưới là no-op hoàn toàn.
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Hook: Editor có thể ghi đè isMobile dựa vào SimulateMobileInEditor().
        /// Signature: (TISMobileInputSetup setup, bool currentValue) → bool overriddenValue
        /// </summary>
        public static System.Func<TISMobileInputSetup, bool, bool> EditorHook_CheckSimulateMobile;

        /// <summary>
        /// Hook: Editor thực hiện tìm TISInputBridge và hiển thị dialog.
        /// Signature: (TISMobileInputSetup setup) → void
        /// </summary>
        public static System.Action<TISMobileInputSetup> EditorHook_TimBridgeTuDong;

        /// <summary>
        /// Hook: Editor truyền knob size ratio debug xuống joystick.
        /// Signature: (TISVirtualJoystick joystick, float knobSizeRatio) → void
        /// </summary>
        public static System.Action<TISVirtualJoystick, float> EditorHook_SetKnobSizeRatioDebug;

        // ══════════════════════════════════════════════════════════════════════
        //  PREVIEW INSPECTOR
        // ══════════════════════════════════════════════════════════════════════
        
        [SerializeField] public bool _previewDoc = false;

        // ══════════════════════════════════════════════════════════════════════
        //  TAB: TỔNG QUAN
        // ══════════════════════════════════════════════════════════════════════

        [Tooltip("Để trống → tự tìm TISInputBridge.Instance khi Awake.")]
        [SerializeField] public TISInputBridge _inputBridge;

        [Tooltip("Sorting order của Canvas mobile UI. Giá trị cao hơn = hiển thị trên cùng.")]
        [SerializeField] public int _canvasSortOrder = -1;

        [Tooltip("Độ phân giải tham chiếu cho CanvasScaler. Nên khớp với thiết kế UI.")]
        [SerializeField] public Vector2 _referenceResolution = new Vector2(1920f, 1080f);

        [Range(0f, 1f)]
        [Tooltip("Cân bằng chiều ngang / dọc cho CanvasScaler.\n0 = theo Width · 1 = theo Height · 0.5 = cân bằng.")]
        [SerializeField] public float _matchWidthOrHeight = 0.5f;

        [Tooltip("Prefab có sẵn TISVirtualJoystick component.\nĐể trống → tự tạo bằng code với cài đặt ở tab Joystick.")]
        [SerializeField] public GameObject _joystickPrefab;

        [Tooltip("Prefab có sẵn TISTouchCameraZone component.\nĐể trống → tự tạo bằng code với cài đặt ở tab Camera.")]
        [SerializeField] public GameObject _cameraZonePrefab;

        // ══════════════════════════════════════════════════════════════════════
        //  TAB: JOYSTICK
        // ══════════════════════════════════════════════════════════════════════

        [Range(80f, 320f)]
        [Tooltip("Đường kính (pixel) của vùng nền joystick.\nGiá trị càng lớn, vùng chạm càng rộng.")]
        [SerializeField] public float _joystickBGDiameter = 200f;

        [Range(0.2f, 0.85f)]
        [Tooltip("Tỉ lệ kích thước của knob so với nền.\n0.5 = knob bằng một nửa đường kính nền.")]
        [SerializeField] public float _knobSizeRatio = 0.5f;

        [Tooltip("Sprite cho vùng nền joystick.\nĐể trống → tự dùng màu khối đơn giản.")]
        [SerializeField] public Sprite _spriteNen;

        [Tooltip("Sprite cho núm điều khiển (knob).\nĐể trống → tự dùng màu khối đơn giản.")]
        [SerializeField] public Sprite _spriteKnob;

        [Tooltip("Màu của vùng nền joystick khi hiển thị.")]
        [SerializeField] public Color _mauNen  = new Color(1f, 1f, 1f, 0.25f);

        [Tooltip("Màu của núm điều khiển (knob).")]
        [SerializeField] public Color _mauKnobHienThi = new Color(1f, 1f, 1f, 0.60f);

        // ══════════════════════════════════════════════════════════════════════
        //  TAB: JOYSTICK — NÂNG CAO (Override)
        //
        //  Các thông số này ghi đè lên TISVirtualJoystick / TISMobileSettings
        //  (profile). Bật _tuyChinhJoystickNangCao để áp dụng; tắt → dùng giá
        //  trị mặc định từ prefab / profile như trước.
        // ══════════════════════════════════════════════════════════════════════

        [Tooltip("Bật → ghi đè các thông số bên dưới lên TISVirtualJoystick / TISMobileSettings.\n" +
                 "Tắt → dùng giá trị mặc định từ prefab hoặc từ profile hiện tại.")]
        [SerializeField] public bool _tuyChinhJoystickNangCao = false;

        [Tooltip("Vị trí neo (anchoredPosition) mặc định của joystick, tính từ tâm Canvas.\n" +
                 "Dùng cho vị trí ban đầu (Dynamic mode) hoặc vị trí cố định (Fixed mode).")]
        [SerializeField] public Vector2 _joystickAnchoredOrigin = new Vector2(-800f, -300f);

        [Range(0f, 1f)]
        [Tooltip("Opacity của joystick khi đang được chạm/kéo (active).")]
        [SerializeField] public float _joystickActiveOpacity = 0.85f;

        [Tooltip("Ghi đè dynamicJoystick của TISMobileSettings (profile).\n" +
                 "true = joystick hiện tại điểm chạm (PUBG/Liên Quân style) · false = cố định vị trí.")]
        [SerializeField] public bool _dynamicJoystickOverride = true;

        [Range(0f, 1f)]
        [Tooltip("Ghi đè idleOpacity của TISMobileSettings (profile) — opacity khi joystick không được chạm.")]
        [SerializeField] public float _idleOpacityOverride = 0.4f;

        // ══════════════════════════════════════════════════════════════════════
        //  TAB: PHÂN VÙNG
        // ══════════════════════════════════════════════════════════════════════

        [Tooltip("Bật → hiển thị Virtual Joystick ở phần bên trái màn hình.\nTắt → toàn bộ màn hình dành cho Camera (100%).")]
        [SerializeField] public bool _batJoystick = true;

        [Tooltip("Bật → kéo vùng bên phải màn hình để điều khiển Camera.\nTắt → toàn bộ màn hình dành cho Joystick (100%).")]
        [SerializeField] public bool _batCamera = true;

        [Range(0.25f, 0.75f)]
        [Tooltip("Tỉ lệ chiều rộng màn hình dành cho vùng JOYSTICK (bên trái).\n" +
                 "Phần còn lại tự động là vùng CAMERA (bên phải).\n\n" +
                 "Ví dụ: 0.5 = mỗi bên chiếm đúng nửa màn hình.")]
        [SerializeField] public float _tyLeJoystick = 0.5f;

        // ══════════════════════════════════════════════════════════════════════
        //  TAB: CAMERA
        // ══════════════════════════════════════════════════════════════════════

        [Tooltip("Bật → kéo ngón tay bên phải màn hình để xoay camera.\nTắt → TISTouchCameraZone bị vô hiệu hoá hoàn toàn.")]
        [SerializeField] public bool _batXoayCamera = true;

        [Range(1f, 60f)]
        [Tooltip("Tốc độ làm mượt khi xoay camera. Cao = phản hồi nhanh · Thấp = chuyển động mượt, cinematic.")]
        [SerializeField] public float _tocDoMuotXoay = 15f;

        [Range(0f, 20f)]
        [Tooltip("Quán tính sau khi nhả ngón tay. 0 = dừng ngay · Cao = trôi dài.")]
        [SerializeField] public float _quanTinhXoay = 10f;

        [Tooltip("Bật → dùng 2 ngón tay để zoom camera.\nPinch chỉ nhận khi cả 2 ngón ở vùng Camera — không bao giờ xung đột với joystick.")]
        [SerializeField] public bool _batZoom = true;

        [Range(0.001f, 0.05f)]
        [Tooltip("Tốc độ zoom khi pinch. Khuyến nghị: 0.005 – 0.02.\nGiá trị nhỏ = phải kéo dài hơn mới zoom được.")]
        [SerializeField] public float _tocDoZoom = 0.01f;

        [Range(0f, 30f)]
        [Tooltip("Khoảng cách pixel tối thiểu giữa 2 ngón mới bắt đầu zoom.\nTránh zoom nhạy khi cố giữ 2 ngón yên.")]
        [SerializeField] public float _nguongPinchPx = 5f;

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        public bool _joystickPrefabAssigned => _joystickPrefab != null;

        /// <summary>
        /// Tỉ lệ hiệu dụng tính đến trạng thái bật/tắt Joystick và Camera.
        /// · Joystick off → 0f (camera 100%)
        /// · Camera off  → 1f (joystick 100%)
        /// · Cả 2 bật    → _tyLeJoystick
        /// </summary>
        public float _tyLeJoystickHienThi
        {
            get
            {
                if (!_batJoystick) return 0f;
                if (!_batCamera)   return 1f;
                return _tyLeJoystick;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  EDITOR CALLBACKS (called from custom editor)
        // ══════════════════════════════════════════════════════════════════════

        public void _onToggleJoystickCamera()
        {
            if (!Application.isPlaying) return;
            ApDungToggleJoystickCamera();
        }

        public void _onSizeChanged()
        {
            if (Application.isPlaying && _joystick != null)
                _joystick.SetRadius(_joystickBGDiameter * 0.5f);
        }

        public void _onRatioChanged()
        {
            if (Application.isPlaying) DongBoVungManHinh();
        }

        public void _onCameraChanged() { if (Application.isPlaying) ApDungCaiDatCamera(); }

        public void _onJoystickAdvancedChanged()
        {
            if (Application.isPlaying) ApDungJoystickNangCao();
        }

        public void TimBridgeTuDong()
        {
            EditorHook_TimBridgeTuDong?.Invoke(this);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  RUNTIME REFERENCES
        // ══════════════════════════════════════════════════════════════════════

        private Canvas                       _mobileCanvas;
        private TISVirtualJoystick           _joystick;
        private TISTouchCameraZone           _cameraZone;
        private TISFullscreenTouchReceiver   _touchReceiver;
        private TISMultiFingerGestureDetector _gestureDetector;

        public TISVirtualJoystick            Joystick         => _joystick;
        public TISTouchCameraZone            CameraZone       => _cameraZone;
        public TISFullscreenTouchReceiver    TouchReceiver    => _touchReceiver;
        public TISMultiFingerGestureDetector GestureDetector  => _gestureDetector;

        // P3 FIX #17: Lifecycle rõ ràng ─────────────────────────────────────────

        /// <summary>
        /// true sau khi KhoiTaoMobileUI() hoàn thành mà không có lỗi.
        /// Game script có thể poll hoặc subscribe SetupCompleted event thay vì
        /// đoán rằng mobile UI đã sẵn sàng sau Awake().
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// Fire sau khi toàn bộ mobile UI đã khởi tạo thành công.
        /// Param: instance TISMobileInputSetup vừa hoàn thành setup.
        /// </summary>
        public event System.Action<TISMobileInputSetup> SetupCompleted;

        // ══════════════════════════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (_inputBridge == null)
                _inputBridge = TISInputBridge.Instance;

            bool isMobile = Application.isMobilePlatform;
            isMobile = EditorHook_CheckSimulateMobile?.Invoke(this, isMobile) ?? isMobile;
            if (!isMobile)
            {
                gameObject.SetActive(false);
                return;
            }

            DamBaoEventSystem();

            // P3 FIX #17: Bọc setup trong try-catch để graceful degradation.
            // Nếu một bước fail (ví dụ: prefab thiếu component), log rõ lỗi
            // thay vì throw exception làm tắt toàn bộ mobile input.
            try
            {
                KhoiTaoMobileUI();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TISMobileInputSetup] ✗ Setup thất bại: {ex.Message}\n{ex.StackTrace}");
                IsReady = false;
                // Không return — vẫn đăng ký OnProfileSwapped để có thể retry sau.
            }

            if (_inputBridge != null)
                _inputBridge.OnProfileSwapped += KhiDoiProfile;
        }

        private void Start()
        {
            DongBoVungManHinh();
            ApDungToggleJoystickCamera();
        }

        private void OnDestroy()
        {
            if (_inputBridge != null)
                _inputBridge.OnProfileSwapped -= KhiDoiProfile;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SETUP
        // ══════════════════════════════════════════════════════════════════════

        private void KhoiTaoMobileUI()
        {
            _mobileCanvas = TaoCanvas();

            if (_joystickPrefab != null)
            {
                var go = Instantiate(_joystickPrefab, _mobileCanvas.transform);
                _joystick = go.GetComponent<TISVirtualJoystick>();
            }
            else
            {
                _joystick = TaoJoystickMacDinh(_mobileCanvas);
            }

            _touchReceiver = TaoFullscreenReceiver(_mobileCanvas);
            _touchReceiver.InjectJoystick(_joystick);

            if (_cameraZonePrefab != null)
            {
                var go = Instantiate(_cameraZonePrefab, transform);
                _cameraZone = go.GetComponent<TISTouchCameraZone>();
            }
            else
            {
                _cameraZone = TaoCameraZoneMacDinh();
            }

            _joystick?.InjectDependencies(_inputBridge);
            ApDungJoystickNangCao();
            _cameraZone?.InjectDependencies(_inputBridge, _joystick);

            _gestureDetector = TaoGestureDetectorMacDinh();
            _gestureDetector.InjectDependencies(_inputBridge, _cameraZone, _joystick);
            _gestureDetector.SetZoneRatio(_tyLeJoystickHienThi, this);

            DongBoVungManHinh();
            ApDungCaiDatCamera();
            ApDungToggleJoystickCamera();
            LamMoiSkillButton();

            // P3 FIX #17: Đánh dấu setup hoàn thành — IsReady để poll, event để subscribe.
            IsReady = true;
            SetupCompleted?.Invoke(this);
        }

        private void DamBaoEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<StandaloneInputModule>();
                Debug.Log("[TISMobileInputSetup] ✓ Đã tạo EventSystem tự động.");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  FACTORY
        // ══════════════════════════════════════════════════════════════════════

        private Canvas TaoCanvas()
        {
            var go = new GameObject("MobileInputCanvas");
            go.transform.SetParent(transform);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _canvasSortOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _referenceResolution;
            scaler.matchWidthOrHeight  = _matchWidthOrHeight;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private TISFullscreenTouchReceiver TaoFullscreenReceiver(Canvas canvas)
        {
            var go = new GameObject("TISFullscreenTouchReceiver");
            go.transform.SetParent(canvas.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color         = Color.clear;
            img.raycastTarget = true;

            return go.AddComponent<TISFullscreenTouchReceiver>();
        }

        private TISVirtualJoystick TaoJoystickMacDinh(Canvas canvas)
        {
            float diameter = _joystickBGDiameter;
            float knobSize = diameter * _knobSizeRatio;
            float radius   = diameter * 0.5f;

            var bgGO = new GameObject("VirtualJoystick_BG");
            bgGO.transform.SetParent(canvas.transform, false);

            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin        = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax        = new Vector2(0.5f, 0.5f);
            bgRect.pivot            = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta        = new Vector2(diameter, diameter);

            var canvasRect = canvas.GetComponent<RectTransform>();
            bgRect.anchoredPosition = new Vector2(
                -canvasRect.rect.width  * 0.5f + radius + 20f,
                -canvasRect.rect.height * 0.5f + radius + 20f
            );

            var bgImg = bgGO.AddComponent<Image>();
            bgImg.sprite = _spriteNen;
            bgImg.color  = _mauNen;
            bgImg.type   = _spriteNen != null ? Image.Type.Sliced : Image.Type.Simple;

            var knobGO = new GameObject("Knob");
            knobGO.transform.SetParent(bgGO.transform, false);

            var knobRect = knobGO.AddComponent<RectTransform>();
            knobRect.anchorMin        = new Vector2(0.5f, 0.5f);
            knobRect.anchorMax        = new Vector2(0.5f, 0.5f);
            knobRect.pivot            = new Vector2(0.5f, 0.5f);
            knobRect.sizeDelta        = new Vector2(knobSize, knobSize);
            knobRect.anchoredPosition = Vector2.zero;

            var knobImg = knobGO.AddComponent<Image>();
            knobImg.sprite = _spriteKnob;
            knobImg.color  = _mauKnobHienThi;
            knobImg.type   = _spriteKnob != null ? Image.Type.Sliced : Image.Type.Simple;

            var joystick = bgGO.AddComponent<TISVirtualJoystick>();
            joystick.InjectKnob(knobRect);
            joystick.SetRadius(radius);

            EditorHook_SetKnobSizeRatioDebug?.Invoke(joystick, _knobSizeRatio);

            return joystick;
        }

        private TISTouchCameraZone TaoCameraZoneMacDinh()
        {
            var go = new GameObject("TISTouchCameraZone");
            go.transform.SetParent(transform, false);

            var zone = go.AddComponent<TISTouchCameraZone>();
            // P2 FIX #12: Dùng public setter thay vì DatField reflection.
            zone.SetSmoothSpeed(_tocDoMuotXoay);
            zone.SetInertiaDecay(_quanTinhXoay);
            zone.SetEnablePinchZoom(_batZoom);
            zone.SetPinchZoomSpeed(_tocDoZoom);
            zone.SetPinchDeadzone(_nguongPinchPx);
            return zone;
        }

        private TISMultiFingerGestureDetector TaoGestureDetectorMacDinh()
        {
            var go = new GameObject("TISMultiFingerGestureDetector");
            go.transform.SetParent(transform, false);
            return go.AddComponent<TISMultiFingerGestureDetector>();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SYNC RUNTIME
        // ══════════════════════════════════════════════════════════════════════

        public void ApDungToggleJoystickCamera()
        {
            float tyLeHienTai = _tyLeJoystickHienThi;

            if (_joystick != null)
                _joystick.gameObject.SetActive(_batJoystick);

            // P2 FIX #12: Dùng public setter thay vì DatField reflection.
            _joystick?.SetActivationZoneWidthRatio(tyLeHienTai);
            _cameraZone?.SetZoneStartRatio(tyLeHienTai);

            _inputBridge?.GestureSyncZone(tyLeHienTai);

            ApDungCaiDatCamera();
        }

        public void DongBoVungManHinh()
        {
            float ty = _tyLeJoystickHienThi;

            // P2 FIX #12: Dùng public setter thay vì DatField reflection.
            _joystick?.SetActivationZoneWidthRatio(ty);
            _cameraZone?.SetZoneStartRatio(ty);

            _inputBridge?.GestureSyncZone(ty);
            _inputBridge?.GestureSyncZone();
        }

        public void ApDungCaiDatCamera()
        {
            if (_cameraZone == null) return;

            _cameraZone.enabled = _batXoayCamera;

            // P2 FIX #12: Dùng public setter thay vì DatField reflection.
            _cameraZone.SetEnablePinchZoom(_batZoom && _batXoayCamera);
            _cameraZone.SetPinchZoomSpeed(_tocDoZoom);
            _cameraZone.SetPinchDeadzone(_nguongPinchPx);
            _cameraZone.SetSmoothSpeed(_tocDoMuotXoay);
            _cameraZone.SetInertiaDecay(_quanTinhXoay);
        }

        /// <summary>
        /// Ghi đè activeOpacity/anchoredOrigin (TISVirtualJoystick) và
        /// dynamicJoystick/idleOpacity (TISMobileSettings) lên joystick hiện tại,
        /// nếu _tuyChinhJoystickNangCao được bật. Gọi sau khi joystick được tạo/inject
        /// và sau mỗi lần đổi profile để override không bị mất.
        /// </summary>
        public void ApDungJoystickNangCao()
        {
            if (_joystick == null || !_tuyChinhJoystickNangCao) return;

            _joystick.SetAnchoredOrigin(_joystickAnchoredOrigin);
            _joystick.SetActiveOpacity(_joystickActiveOpacity);
            _joystick.SetDynamicMode(_dynamicJoystickOverride);
            _joystick.SetIdleOpacity(_idleOpacityOverride);
        }

        private void LamMoiSkillButton()
        {
            var settings = _inputBridge?.TISMobileSettings;
            if (settings == null || _mobileCanvas == null) return;

            var buttons = _mobileCanvas.GetComponentsInChildren<TISMobileSkillButton>(true);
            foreach (var btn in buttons)
                btn.gameObject.SetActive(settings.showSkillButtons);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        public void BatTatXoayCamera()
        {
            _batXoayCamera = !_batXoayCamera;
            ApDungCaiDatCamera();
        }

        public void BatTatZoom()
        {
            _batZoom = !_batZoom;
            ApDungCaiDatCamera();
        }

        public void BatTatJoystick()
        {
            _batJoystick = !_batJoystick;
            ApDungToggleJoystickCamera();
        }

        public void BatTatCameraZone()
        {
            _batCamera = !_batCamera;
            ApDungToggleJoystickCamera();
        }

        public void DongBoTyLe() => DongBoVungManHinh();

        public void DatXoayCamera(bool batLen)
        {
            _batXoayCamera = batLen;
            ApDungCaiDatCamera();
        }

        public void DatZoom(bool batLen)
        {
            _batZoom = batLen;
            ApDungCaiDatCamera();
        }

        public void DatJoystick(bool batLen)
        {
            _batJoystick = batLen;
            ApDungToggleJoystickCamera();
        }

        public void DatCameraZone(bool batLen)
        {
            _batCamera = batLen;
            ApDungToggleJoystickCamera();
        }

        public void DatTyLeVung(float tyLeJoystick)
        {
            _tyLeJoystick = Mathf.Clamp(tyLeJoystick, 0.25f, 0.75f);
            DongBoVungManHinh();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SỰ KIỆN
        // ══════════════════════════════════════════════════════════════════════

        private void KhiDoiProfile(TISInputProfileConfig profileMoi)
        {
            _joystick?.ApplyProfileSettings();
            ApDungJoystickNangCao();
            _cameraZone?.SyncProfileSettings();
            _gestureDetector?.SyncZone();
            ApDungCaiDatCamera();
            LamMoiSkillButton();
            Debug.Log($"[TISMobileInputSetup] ✓ Profile mới → \"{profileMoi.profileName}\"");
        }

    }
}