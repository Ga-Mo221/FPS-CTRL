// CopyTransformPieMenu.cs
// Đặt file này vào: Assets/Editor/CopyTransformPieMenu.cs
//
// ── Pie menu copy Transform nhanh (kiểu Blender "Copy Attributes") ──────────
//
//  Mặc định phím tắt: đổi được ở phần Settings bên dưới, tương tự cơ chế của
//  CreateObjectPieMenu.cs.
//
//  ★ PHÍM TẮT DÙNG CHUNG VỚI PasteTransformPieMenu ★
//  Copy và Paste giờ chia sẻ CHUNG 1 tổ hợp phím (cấu hình ở tab "Copy
//  Transform Pie" trong Settings — tab "Paste Transform Pie" chỉ còn hiển thị/
//  chỉnh lại cùng 1 giá trị này để tiện tra cứu).
//
//  Cách dùng: GIỮ modifier (VD Shift) + gõ phím chính (VD X) rồi THẢ NHANH
//  phím chính (vẫn giữ modifier) → pie Copy hiện ra và Ở LẠI màn hình (Toggle
//  Mode). Vẫn đang giữ modifier, gõ phím chính THÊM LẦN NỮA → menu CHUYỂN
//  NGAY sang Paste tại đúng chỗ đó — không tắt đi bật lại, không chớp/flicker.
//  Gõ thêm lần nữa lại quay về Copy — cứ so le mỗi lần gõ, miễn menu đang ở
//  Toggle Mode. Xem SwitchInAsToggle() và nhánh "s_Mode == MenuMode.Toggle"
//  trong HandleKeyboard() bên dưới (và trong PasteTransformPieMenu.cs).
//
//  [HOLD MODE]   Nhấn GIỮ phím tắt (lần đầu tiên, từ trạng thái đóng hẳn)
//                → menu hiện. Lia chuột về phía nút muốn copy → nút đó
//                highlight. THẢ phím → copy giá trị đang highlight & đóng
//                menu. Thả mà không lia chuột (dead-zone) = không làm gì
//                (menu chuyển sang Toggle Mode, xem bên dưới).
//
//  [TOGGLE MODE] Nhấn rồi THẢ NHANH (< HoldThreshold giây, chưa lia chuột)
//                → menu mở cố định, click để chọn.
//                Gõ lại phím CHÍNH (modifier vẫn giữ) → chuyển sang Paste.
//                Escape hoặc chuột phải → đóng hẳn.
//
//  Phím 1–8 chọn nhanh khi menu đang mở.
//
//  ┌─────────────────────────── Bố cục 8 nút ───────────────────────────┐
//  │                         [TRÊN]  Local Transform (cả 3)              │
//  │                                                                     │
//  │   [TRÁI-TRÊN] Local Position          Local ↔ World                │
//  │   [TRÁI-GIỮA] Local Rotation                World Position [PHẢI-TRÊN]│
//  │   [TRÁI-DƯỚI] Local Scale                   World Rotation [PHẢI-GIỮA]│
//  │                                              World Scale   [PHẢI-DƯỚI]│
//  │                         [DƯỚI]   World Transform (cả 3)             │
//  └─────────────────────────────────────────────────────────────────────┘
//
//  Giá trị được copy vào Clipboard hệ thống (EditorGUIUtility.systemCopyBuffer)
//  dưới dạng text người đọc được, đồng thời lưu lại trong các field static
//  (LastLocalPosition, LastWorldRotation, v.v.) để các tool khác (VD một Paste
//  Pie Menu trong tương lai) có thể tái sử dụng trực tiếp mà không cần parse text.
//
//  Cấu hình phím tắt tại vùng "Settings" bên dưới trong file này.
// ─────────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    [InitializeOnLoad]
    public static class CopyTransformPieMenu
    {
        // ════════════════════════════════════════════════════════════════════════
        //  Hằng số animation
        // ════════════════════════════════════════════════════════════════════════
        private const float ANIM_SPEED = 10f;

        // Ở Toggle Mode (đã thả phím tắt nhưng menu vẫn mở), nếu người dùng kéo chuột ra
        // xa quá bán kính này (tính theo bội số của BackdropR) thì coi như đã "chốt" hướng
        // muốn chọn — tự động copy & đóng menu, không bắt buộc phải click chuột trái nữa.
        private const float TOGGLE_AUTO_CONFIRM_MULT = 1.5f;

        // ── Shorthand đọc Settings (dùng chung khung hiển thị Pie Menu) ─────────
        private static float MenuRadius   => BlenderEditorPreferences.PieMenuRadius;
        private static float ButtonSize   => BlenderEditorPreferences.PieMenuButtonSize;
        private static float HoldThresh   => BlenderEditorPreferences.PieMenuHoldThreshold;
        private static float DeadZone     => BlenderEditorPreferences.PieMenuDeadZone;
        private static float BackdropR    => BlenderEditorPreferences.PieMenuBackdropRadius;
        private static float CenterR      => BlenderEditorPreferences.PieMenuCenterRadius;
        private static int   CenterFont   => BlenderEditorPreferences.PieMenuCenterFontSize;
        private static int   BtnLabelFont => BlenderEditorPreferences.PieMenuBtnLabelFontSize;
        private static int   BtnSubFont   => BlenderEditorPreferences.PieMenuBtnSubFontSize;
        private static int   TooltipFont  => BlenderEditorPreferences.PieMenuTooltipFontSize;
        private static int   HintFont     => BlenderEditorPreferences.PieMenuHintFontSize;
        private static float HoverScale   => BlenderEditorPreferences.PieMenuHoverScale;
        private static float BeamArrow    => BlenderEditorPreferences.PieMenuBeamArrowSize;

        // ════════════════════════════════════════════════════════════════════════
        //  EditorPrefs Keys cho phím tắt riêng của menu này
        // ════════════════════════════════════════════════════════════════════════
        private const string K_CTPM_ENABLED  = "CTPM_Enabled";
        private const string K_CTPM_KEY_CODE = "CTPM_KeyCode";
        private const string K_CTPM_SHIFT    = "CTPM_UseShift";
        private const string K_CTPM_ALT      = "CTPM_UseAlt";
        private const string K_CTPM_CTRL     = "CTPM_UseCtrl";

        // Defaults: Ctrl + Alt + C
        public static bool    Enabled  { get => EditorPrefs.GetBool  (K_CTPM_ENABLED,  false);                 set => EditorPrefs.SetBool  (K_CTPM_ENABLED,  value); }
        public static KeyCode HotKey   { get => (KeyCode)EditorPrefs.GetInt(K_CTPM_KEY_CODE, (int)KeyCode.C); set => EditorPrefs.SetInt   (K_CTPM_KEY_CODE, (int)value); }
        public static bool    UseShift { get => EditorPrefs.GetBool  (K_CTPM_SHIFT,    false);                set => EditorPrefs.SetBool  (K_CTPM_SHIFT,    value); }
        public static bool    UseAlt   { get => EditorPrefs.GetBool  (K_CTPM_ALT,      true);                 set => EditorPrefs.SetBool  (K_CTPM_ALT,      value); }
        public static bool    UseCtrl  { get => EditorPrefs.GetBool  (K_CTPM_CTRL,     true);                 set => EditorPrefs.SetBool  (K_CTPM_CTRL,     value); }

        public static bool MatchesHotKey(Event e)
        {
            if (e == null) return false;
            bool modOk = (e.shift   == UseShift) &&
                         (e.alt     == UseAlt)   &&
                         (e.control == UseCtrl || e.command == UseCtrl);
            return modOk && e.keyCode == HotKey;
        }

        public static string HotKeyDisplayString()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (UseCtrl)  parts.Add("Ctrl");
            if (UseShift) parts.Add("Shift");
            if (UseAlt)   parts.Add("Alt");
            parts.Add(HotKey.ToString());
            return string.Join(" + ", parts);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Phím tắt DÙNG CHUNG với PasteTransformPieMenu (gộp Copy + Paste vào 1
        //  tổ hợp phím vì 2 pie phục vụ chung 1 tính năng "copy/paste transform").
        //  PasteTransformPieMenu.Enabled/HotKey/UseShift/UseAlt/UseCtrl/MatchesHotKey/
        //  HotKeyDisplayString giờ trỏ thẳng về các field bên trên — chỉnh ở tab nào
        //  trong Settings cũng ra cùng 1 kết quả.
        //
        //  Cách dùng: giữ modifier (VD Shift) + gõ phím chính (VD X) rồi THẢ ra
        //  → Copy mở ở dạng Toggle (ở lại màn hình). Vẫn giữ modifier, gõ phím
        //  chính THÊM LẦN NỮA → CHUYỂN NGAY sang Paste tại đúng vị trí đó (không
        //  đóng-rồi-mở-lại, không chớp/flicker). Gõ thêm lần nữa lại chuyển về
        //  Copy — cứ thế luân phiên mỗi lần gõ, miễn menu đang ở Toggle Mode.
        //  Xem SwitchInAsToggle() và nhánh "s_Mode == MenuMode.Toggle" trong
        //  HandleKeyboard() của cả 2 file.
        // ════════════════════════════════════════════════════════════════════════

        // ════════════════════════════════════════════════════════════════════════
        //  Giá trị copy cuối cùng — public để tool khác (VD Paste Pie Menu tương
        //  lai) có thể đọc trực tiếp mà không cần parse lại Clipboard text.
        // ════════════════════════════════════════════════════════════════════════
        public static Vector3?    LastLocalPosition { get; private set; }
        public static Quaternion? LastLocalRotation { get; private set; }
        public static Vector3?    LastLocalScale    { get; private set; }
        public static Vector3?    LastWorldPosition { get; private set; }
        public static Quaternion? LastWorldRotation { get; private set; }
        public static Vector3?    LastWorldScale    { get; private set; }

        // ════════════════════════════════════════════════════════════════════════
        //  Định nghĩa 8 slot copy
        // ════════════════════════════════════════════════════════════════════════
        private enum CopyKind
        {
            LocalPosition, LocalRotation, LocalScale, LocalAll,
            WorldPosition, WorldRotation, WorldScale, WorldAll,
        }

        private struct CopyEntry
        {
            public string   Label;
            public string   SubLabel;
            public Color    IconColor;
            public float    AngleDeg;
            public CopyKind Kind;
            public bool     IsCombo; // true = nút "cả 3" (vẽ diamond thay vì tròn)
        }

        // Góc 0° = phải, tăng thuận chiều kim đồng hồ trong tọa độ GUI (y xuống).
        // Cụm trái (150°/180°/210°) = Local Position/Rotation/Scale, đọc từ trên xuống.
        // Cụm phải (-30°/0°/30°)    = World Position/Rotation/Scale, đọc từ trên xuống.
        // Trên (-90°) = Local Transform (cả 3). Dưới (90°) = World Transform (cả 3).
        private static readonly Color s_LocalColor = new Color(0.30f, 0.69f, 0.91f); // xanh dương – Local
        private static readonly Color s_WorldColor = new Color(0.91f, 0.60f, 0.26f); // cam – World

        private static readonly CopyEntry[] s_Entries =
        {
            // 0 – Trái trên (-135°): Local Position
            new CopyEntry { Label="Position", SubLabel="LOCAL", IconColor=s_LocalColor, AngleDeg=-135f, Kind=CopyKind.LocalPosition },
            // 1 – Trái giữa (180°): Local Rotation
            new CopyEntry { Label="Rotation", SubLabel="LOCAL", IconColor=s_LocalColor, AngleDeg=180f, Kind=CopyKind.LocalRotation },
            // 2 – Trái dưới (135°): Local Scale
            new CopyEntry { Label="Scale",    SubLabel="LOCAL", IconColor=s_LocalColor, AngleDeg=135f, Kind=CopyKind.LocalScale },
            // 3 – Trên (-90°): Local Transform (cả 3)
            new CopyEntry { Label="Local",    SubLabel="ALL 3", IconColor=s_LocalColor, AngleDeg=-90f, Kind=CopyKind.LocalAll, IsCombo=true },
            // 4 – Phải trên (-45°): World Position
            new CopyEntry { Label="Position", SubLabel="WORLD", IconColor=s_WorldColor, AngleDeg=-45f, Kind=CopyKind.WorldPosition },
            // 5 – Phải giữa (0°): World Rotation
            new CopyEntry { Label="Rotation", SubLabel="WORLD", IconColor=s_WorldColor, AngleDeg=0f,   Kind=CopyKind.WorldRotation },
            // 6 – Phải dưới (45°): World Scale
            new CopyEntry { Label="Scale",    SubLabel="WORLD", IconColor=s_WorldColor, AngleDeg=45f,  Kind=CopyKind.WorldScale },
            // 7 – Dưới (90°): World Transform (cả 3)
            new CopyEntry { Label="World",    SubLabel="ALL 3", IconColor=s_WorldColor, AngleDeg=90f,  Kind=CopyKind.WorldAll, IsCombo=true },
        };

        private static readonly string[] s_FullNames =
        {
            "Local Position  [1]",
            "Local Rotation  [2]",
            "Local Scale  [3]",
            "Local Transform (Pos + Rot + Scale)  [4]",
            "World Position  [5]",
            "World Rotation  [6]",
            "World Scale  [7]",
            "World Transform (Pos + Rot + Scale)  [8]",
        };

        // ════════════════════════════════════════════════════════════════════════
        //  Trạng thái nội bộ
        // ════════════════════════════════════════════════════════════════════════
        private enum MenuMode { Closed, Hold, Toggle }

        private static MenuMode s_Mode        = MenuMode.Closed;
        private static bool     s_KeyHeld     = false;
        private static double   s_KeyDownTime = 0.0;

        private static float    s_AnimT       = 0f;
        private static Vector2  s_Center;
        private static int      s_HoveredIdx  = -1;
        private static Vector2  s_MouseDelta;
        private static float    s_DeltaMag;

        // ════════════════════════════════════════════════════════════════════════
        //  Bootstrap
        // ════════════════════════════════════════════════════════════════════════
        /// True khi menu đang hiện (Hold hoặc Toggle). Dùng để báo cáo cho
        /// BlenderInputPriority và để 2 pie menu còn lại tự tránh mở song song.
        public static bool IsOpen => s_Mode != MenuMode.Closed;

        static CopyTransformPieMenu()
        {
            SceneView.duringSceneGui += OnSceneGUI;

            // Gộp trạng thái "đang mở" của CẢ 3 pie menu thành 1 nguồn sự thật duy nhất
            // cho trọng tài ưu tiên trung tâm. Idempotent — CreateObjectPieMenu.cs và
            // Viewaxispiemenu.cs cũng gán cùng nội dung lambda này, chỉ ai load sau cùng
            // thắng nhưng kết quả như nhau. Xem BlenderInputPriority.cs.
            BlenderInputPriority.IsPieMenuOpen = () =>
                CreateObjectPieMenu.IsOpen || ViewAxisPieMenu.IsOpen ||
                CopyTransformPieMenu.IsOpen || PasteTransformPieMenu.IsOpen;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Entry point
        // ════════════════════════════════════════════════════════════════════════
        private static void OnSceneGUI(SceneView sv)
        {
            if (!Enabled) return;

            Event e = Event.current;
            HandleKeyboard(e, sv);

            bool menuVisible = s_Mode != MenuMode.Closed;

            if (!menuVisible && s_AnimT < 0.001f) return;

            float animTarget = menuVisible ? 1f : 0f;
            s_AnimT = Mathf.Lerp(s_AnimT, animTarget, 1f - Mathf.Exp(-ANIM_SPEED * 0.016f));
            if (!menuVisible && s_AnimT < 0.002f) s_AnimT = 0f;

            if (e != null) UpdateDirection(e);

            // Toggle Mode: kéo chuột ra đủ xa = tự chọn luôn, không cần click.
            CheckToggleAutoConfirm();

            DrawMenu(sv);

            if (s_Mode == MenuMode.Toggle && e != null)
                HandleToggleClick(e, sv);

            if (s_AnimT > 0.001f || menuVisible) sv.Repaint();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Keyboard
        // ════════════════════════════════════════════════════════════════════════
        private static void HandleKeyboard(Event e, SceneView sv)
        {
            if (e == null) return;

            bool isHotKey = MatchesHotKey(e);

            // ── KeyDown ──────────────────────────────────────────────────────────
            if (e.type == EventType.KeyDown && isHotKey && !s_KeyHeld)
            {
                // Nếu Copy đang mở dạng Toggle (đã thả phím 1 lần trước đó, modifier vẫn
                // giữ) → gõ phím chính thêm lần nữa nghĩa là "chuyển sang Paste", KHÔNG
                // phải đóng menu. Xử lý riêng, không cần các guard clause bên dưới (vì
                // không mở mới từ đầu, chỉ chuyển pie đang hiển thị).
                if (s_Mode == MenuMode.Toggle)
                {
                    if (BlenderInputPriority.IsTransformSessionActive() ||
                        CreateObjectPieMenu.IsOpen || ViewAxisPieMenu.IsOpen)
                        return;

                    e.Use();
                    s_KeyHeld     = true;
                    s_KeyDownTime = EditorApplication.timeSinceStartup;
                    Vector2 center = s_Center;
                    ForceClose();
                    PasteTransformPieMenu.SwitchInAsToggle(center);
                    return;
                }

                // Không cho mở nếu: đang trong phiên G/R/S, hoặc 2 pie menu còn lại đang mở.
                // Xem ghi chú tương ứng trong CreateObjectPieMenu.cs / Viewaxispiemenu.cs.
                if (BlenderInputPriority.IsTransformSessionActive() ||
                    CreateObjectPieMenu.IsOpen || ViewAxisPieMenu.IsOpen || PasteTransformPieMenu.IsOpen)
                    return;

                // Không có object nào được chọn thì không có gì để copy.
                if (Selection.activeTransform == null)
                    return;

                e.Use();
                s_KeyHeld     = true;
                s_KeyDownTime = EditorApplication.timeSinceStartup;

                OpenMenu(e, sv);
                s_Mode = MenuMode.Hold;
                return;
            }

            // ── KeyUp ─────────────────────────────────────────────────────────────
            if (e.type == EventType.KeyUp && e.keyCode == HotKey && s_KeyHeld)
            {
                e.Use();
                s_KeyHeld = false;

                if (s_Mode == MenuMode.Hold)
                {
                    double held = EditorApplication.timeSinceStartup - s_KeyDownTime;
                    // FIX: chỉ coi là "nhấn nhanh → Toggle Mode" khi thời gian giữ ngắn
                    // VÀ chuột chưa lia ra khỏi dead-zone (chưa hover slot nào). Trước đây
                    // chỉ xét thời gian, nên giữ-kéo-thả thật nhanh (đủ chọn trúng 1 slot
                    // nhưng tổng thời gian vẫn < HoldThresh) bị hiểu nhầm là "nhấn nhanh",
                    // pie chuyển sang Toggle thay vì xác nhận chọn, kẹt lại phải bấm Esc.
                    if (held < HoldThresh && s_HoveredIdx < 0)
                        s_Mode = MenuMode.Toggle;   // nhấn nhanh, chưa chọn slot nào → Toggle Mode
                    else
                    {
                        if (s_HoveredIdx >= 0)
                            CopyEntryAt(s_HoveredIdx);
                        ForceClose();
                    }
                }
                return;
            }

            // ── Escape ────────────────────────────────────────────────────────────
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && s_Mode != MenuMode.Closed)
            {
                e.Use();
                ForceClose();
                return;
            }

            // ── Phím số 1–8 ──────────────────────────────────────────────────────
            if (s_Mode != MenuMode.Closed && e.type == EventType.KeyDown)
            {
                int idx = e.keyCode switch
                {
                    KeyCode.Alpha1 => 0, KeyCode.Alpha2 => 1,
                    KeyCode.Alpha3 => 2, KeyCode.Alpha4 => 3,
                    KeyCode.Alpha5 => 4, KeyCode.Alpha6 => 5,
                    KeyCode.Alpha7 => 6, KeyCode.Alpha8 => 7,
                    _ => -1
                };
                if (idx >= 0) { e.Use(); CopyEntryAt(idx); ForceClose(); }
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Mở / đóng
        // ════════════════════════════════════════════════════════════════════════
        private static void OpenMenu(Event e, SceneView sv)
        {
            s_Center = (e != null)
                ? e.mousePosition
                : new Vector2(sv.position.width * 0.5f, sv.position.height * 0.5f);
            s_HoveredIdx = -1;
            s_MouseDelta = Vector2.zero;
            s_DeltaMag   = 0f;
            sv.Repaint();
        }

        private static void ForceClose()
        {
            s_Mode       = MenuMode.Closed;
            s_KeyHeld    = false;
            s_HoveredIdx = -1;
        }

        /// Được PasteTransformPieMenu gọi khi người dùng gõ phím tắt thêm 1 lần nữa
        /// trong lúc Paste đang mở dạng Toggle (modifier vẫn giữ) — chuyển thẳng
        /// sang Copy tại đúng vị trí đó, KHÔNG đóng-rồi-mở-lại (không chớp/flicker).
        internal static void SwitchInAsToggle(Vector2 center)
        {
            if (Selection.activeTransform == null) return; // không có gì để copy thì thôi, coi như đóng hẳn

            s_Center     = center;
            s_HoveredIdx = -1;
            s_MouseDelta = Vector2.zero;
            s_DeltaMag   = 0f;
            s_Mode       = MenuMode.Toggle;
            s_KeyHeld    = false;
            if (s_AnimT < 1f) s_AnimT = 1f; // pie kia vừa full hiện xong, giữ nguyên độ hiện để không bị chớp tắt/mở lại
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Direction
        // ════════════════════════════════════════════════════════════════════════
        private static void UpdateDirection(Event e)
        {
            if (s_Mode == MenuMode.Closed) return;
            s_MouseDelta = e.mousePosition - s_Center;
            s_DeltaMag   = s_MouseDelta.magnitude;
            s_HoveredIdx = GetIndexByAngle(s_MouseDelta, s_DeltaMag);
        }

        private static int GetIndexByAngle(Vector2 delta, float mag)
        {
            if (mag < DeadZone) return -1;
            float angleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            int   best     = -1;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < s_Entries.Length; i++)
            {
                float diff = Mathf.Abs(NormalizeAngle(s_Entries[i].AngleDeg - angleDeg));
                if (diff < bestDiff) { bestDiff = diff; best = i; }
            }
            return best;
        }

        private static float NormalizeAngle(float a) => ((a + 540f) % 360f) - 180f;

        // ════════════════════════════════════════════════════════════════════════
        //  Toggle: tự xác nhận khi kéo chuột ra xa (không cần click)
        // ════════════════════════════════════════════════════════════════════════
        private static void CheckToggleAutoConfirm()
        {
            if (s_Mode != MenuMode.Toggle) return;
            if (s_HoveredIdx < 0) return;

            if (s_DeltaMag > BackdropR * TOGGLE_AUTO_CONFIRM_MULT)
            {
                CopyEntryAt(s_HoveredIdx);
                ForceClose();
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Toggle click
        // ════════════════════════════════════════════════════════════════════════
        private static void HandleToggleClick(Event e, SceneView sv)
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (s_HoveredIdx >= 0)
                { e.Use(); CopyEntryAt(s_HoveredIdx); ForceClose(); }
                else if (s_DeltaMag > BackdropR)
                { e.Use(); ForceClose(); }
            }
            if (e.type == EventType.MouseDown && e.button == 1)
            { e.Use(); ForceClose(); }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Copy logic
        // ════════════════════════════════════════════════════════════════════════
        private static void CopyEntryAt(int index)
        {
            Transform t = Selection.activeTransform;
            if (t == null)
            {
                Notify("Không có object nào được chọn");
                return;
            }

            CopyEntry entry = s_Entries[index];
            string    text;

            switch (entry.Kind)
            {
                case CopyKind.LocalPosition:
                    LastLocalPosition = t.localPosition;
                    text = FormatVec3(t.localPosition);
                    break;

                case CopyKind.LocalRotation:
                    LastLocalRotation = t.localRotation;
                    text = FormatVec3(t.localEulerAngles);
                    break;

                case CopyKind.LocalScale:
                    LastLocalScale = t.localScale;
                    text = FormatVec3(t.localScale);
                    break;

                case CopyKind.LocalAll:
                    LastLocalPosition = t.localPosition;
                    LastLocalRotation = t.localRotation;
                    LastLocalScale    = t.localScale;
                    text = FormatTransformBlock("Local Transform", t.localPosition, t.localEulerAngles, t.localScale);
                    break;

                case CopyKind.WorldPosition:
                    LastWorldPosition = t.position;
                    text = FormatVec3(t.position);
                    break;

                case CopyKind.WorldRotation:
                    LastWorldRotation = t.rotation;
                    text = FormatVec3(t.eulerAngles);
                    break;

                case CopyKind.WorldScale:
                    LastWorldScale = t.lossyScale;
                    text = FormatVec3(t.lossyScale);
                    break;

                case CopyKind.WorldAll:
                    LastWorldPosition = t.position;
                    LastWorldRotation = t.rotation;
                    LastWorldScale    = t.lossyScale;
                    text = FormatTransformBlock("World Transform", t.position, t.eulerAngles, t.lossyScale);
                    break;

                default:
                    return;
            }

            EditorGUIUtility.systemCopyBuffer = text;
            Notify($"Đã copy {entry.SubLabel} {entry.Label} của \"{t.name}\"");
        }

        private static string FormatVec3(Vector3 v) =>
            $"({v.x.ToString("0.####", CultureInfo.InvariantCulture)}, " +
            $"{v.y.ToString("0.####", CultureInfo.InvariantCulture)}, " +
            $"{v.z.ToString("0.####", CultureInfo.InvariantCulture)})";

        private static string FormatTransformBlock(string title, Vector3 pos, Vector3 eulerRot, Vector3 scale)
        {
            var sb = new StringBuilder();
            sb.Append(title).Append('\n');
            sb.Append("Position: ").Append(FormatVec3(pos)).Append('\n');
            sb.Append("Rotation: ").Append(FormatVec3(eulerRot)).Append('\n');
            sb.Append("Scale: ").Append(FormatVec3(scale));
            return sb.ToString();
        }

        private static void Notify(string msg)
        {
            //Debug.Log($"[CopyTransformPieMenu] {msg}");
            // if (SceneView.lastActiveSceneView != null)
            //     SceneView.lastActiveSceneView.ShowNotification(new GUIContent(msg));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Vẽ menu
        // ════════════════════════════════════════════════════════════════════════
        private static void DrawMenu(SceneView sv)
        {
            float t = s_AnimT;
            if (t < 0.002f) return;

            Handles.BeginGUI();

            // Backdrop
            DrawCircle(s_Center, BackdropR * t,
                new Color(0.10f, 0.10f, 0.10f, 0.62f * t),
                new Color(0.55f, 0.55f, 0.55f, 0.20f * t), 1f);

            // Direction beam (Hold mode)
            if (s_Mode == MenuMode.Hold && s_DeltaMag > DeadZone && s_HoveredIdx >= 0)
                DrawDirectionBeam(t);

            // Spoke lines
            for (int i = 0; i < s_Entries.Length; i++)
            {
                float   rad = s_Entries[i].AngleDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                Vector2 end = s_Center + dir * (MenuRadius - ButtonSize * 0.5f - 4f) * t;
                Color   lc  = s_Entries[i].IconColor;
                lc.a        = (i == s_HoveredIdx ? 0.65f : 0.22f) * t;
                Handles.color = lc;
                Handles.DrawLine(s_Center, end);
            }

            // Nút copy
            for (int i = 0; i < s_Entries.Length; i++)
                DrawCopyButton(i, t);

            DrawCenterDot(t);

            if (s_HoveredIdx >= 0 && t > 0.4f)
                DrawTooltip(s_HoveredIdx, t);

            DrawModeHint(t);

            Handles.EndGUI();
        }

        private static void DrawCopyButton(int index, float t)
        {
            CopyEntry entry  = s_Entries[index];
            float     rad    = entry.AngleDeg * Mathf.Deg2Rad;
            Vector2   pos    = s_Center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * MenuRadius * t;

            bool  hovered = s_HoveredIdx == index;
            float scale   = hovered ? HoverScale : 1f;
            float r       = ButtonSize * 0.5f * t * scale;

            Color bg = hovered
                ? new Color(entry.IconColor.r, entry.IconColor.g, entry.IconColor.b, 0.26f * t)
                : new Color(0.16f, 0.16f, 0.16f, 0.93f * t);

            Color border = hovered
                ? new Color(entry.IconColor.r, entry.IconColor.g, entry.IconColor.b, 1.00f * t)
                : new Color(0.44f, 0.44f, 0.44f, 0.50f * t);

            // Nút "cả 3" (Local/World Transform) vẽ diamond để phân biệt với nút component đơn.
            if (entry.IsCombo)
                DrawDiamond(pos, r, bg, border, hovered ? 2.0f : 0.75f);
            else
                DrawCircle(pos, r, bg, border, hovered ? 2.0f : 0.75f);

            if (hovered)
            {
                if (entry.IsCombo)
                    DrawDiamond(pos, r + 5f * t,
                        new Color(0, 0, 0, 0),
                        new Color(entry.IconColor.r, entry.IconColor.g, entry.IconColor.b, 0.28f * t), 4f);
                else
                    DrawCircle(pos, r + 5f * t,
                        new Color(0, 0, 0, 0),
                        new Color(entry.IconColor.r, entry.IconColor.g, entry.IconColor.b, 0.28f * t), 4f);
            }

            // Label chính
            GUIStyle lbl = new GUIStyle(GUI.skin.label)
            {
                fontSize  = Mathf.RoundToInt(BtnLabelFont * t * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(entry.IconColor.r, entry.IconColor.g, entry.IconColor.b, t) }
            };
            GUI.Label(new Rect(pos.x - r, pos.y - r * 0.50f, r * 2f, r * 0.95f), entry.Label, lbl);

            // Sub-label
            GUIStyle sub = new GUIStyle(GUI.skin.label)
            {
                fontSize  = Mathf.RoundToInt(BtnSubFont * t * scale),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(0.72f, 0.72f, 0.72f, t * 0.85f) }
            };
            GUI.Label(new Rect(pos.x - r, pos.y + r * 0.08f, r * 2f, r * 0.55f), entry.SubLabel, sub);
        }

        private static void DrawCenterDot(float t)
        {
            float cr = CenterR * t;
            DrawCircle(s_Center, cr,
                new Color(0.13f, 0.16f, 0.20f, 0.95f * t),
                new Color(0.55f, 0.55f, 0.55f, 0.40f * t), 0.75f);

            GUIStyle cs = new GUIStyle(GUI.skin.label)
            {
                fontSize  = Mathf.RoundToInt(CenterFont * t),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(0.75f, 0.75f, 0.75f, t) }
            };
            float labelW = cr * 2.2f;
            GUI.Label(new Rect(s_Center.x - labelW * 0.5f, s_Center.y - cr * 0.5f, labelW, cr),
                "COPY", cs);
        }

        private static void DrawTooltip(int index, float t)
        {
            CopyEntry entry = s_Entries[index];
            string    txt   = s_FullNames[index];

            string objInfo = Selection.activeGameObject != null
                ? $"→ {Selection.activeGameObject.name}"
                : "→ (no selection)";

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize  = TooltipFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(entry.IconColor.r, entry.IconColor.g, entry.IconColor.b, t) }
            };

            GUIStyle objStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = Mathf.Max(8, TooltipFont - 2),
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(0.65f, 0.65f, 0.65f, t * 0.85f) }
            };

            Vector2 sz1 = style.CalcSize(new GUIContent(txt));
            Vector2 sz2 = objStyle.CalcSize(new GUIContent(objInfo));
            float   pw  = Mathf.Max(sz1.x, sz2.x) + 24f;
            float   ph  = sz1.y + sz2.y + 14f;

            Rect bg = new Rect(s_Center.x - pw * 0.5f,
                               s_Center.y + BackdropR * 0.58f * t, pw, ph);

            EditorGUI.DrawRect(bg, new Color(0.09f, 0.09f, 0.09f, 0.90f * t));
            DrawRectBorder(bg, new Color(entry.IconColor.r, entry.IconColor.g, entry.IconColor.b, 0.50f * t), 0.75f);

            Rect r1 = new Rect(bg.x, bg.y + 5f, bg.width, sz1.y + 2f);
            Rect r2 = new Rect(bg.x, r1.yMax,   bg.width, sz2.y + 4f);
            GUI.Label(r1, txt,     style);
            GUI.Label(r2, objInfo, objStyle);
        }

        private static void DrawModeHint(float t)
        {
            if (t < 0.5f) return;
            float alpha = (t - 0.5f) * 2f;
            //string hotkey = HotKeyDisplayString();
            // string hint   = s_Mode == MenuMode.Hold
            //     ? $"[{hotkey}] Giữ & lia chuột để chọn  •  Thả để xác nhận"
            //     : $"[{hotkey}] Click để chọn  •  1–8  •  Esc / RMB để đóng";

            GUIStyle hs = new GUIStyle(GUI.skin.label)
            {
                fontSize  = HintFont,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(0.58f, 0.58f, 0.58f, alpha) }
            };
            float hintW = 480f;
            GUI.Label(
                new Rect(s_Center.x - hintW * 0.5f,
                         s_Center.y - BackdropR * 0.72f * t, hintW, HintFont + 6f),
                "", hs);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Direction beam
        // ════════════════════════════════════════════════════════════════════════
        private static void DrawDirectionBeam(float t)
        {
            int     hi      = s_HoveredIdx;
            Color   col     = s_Entries[hi].IconColor;
            float   len     = Mathf.Min(s_DeltaMag, BackdropR * 0.86f);
            Vector2 dir     = s_MouseDelta.normalized;
            Vector2 beamEnd = s_Center + dir * len * t;

            Handles.color = new Color(col.r, col.g, col.b, 0.18f * t);
            Handles.DrawAAPolyLine(len * 0.20f, s_Center, beamEnd);

            Handles.color = new Color(col.r, col.g, col.b, 0.72f * t);
            Handles.DrawAAPolyLine(1.5f, s_Center, beamEnd);

            float   arrowSize = BeamArrow * t;
            Vector2 perp      = new Vector2(-dir.y, dir.x);
            Vector3 tip       = new Vector3(beamEnd.x, beamEnd.y);
            Vector3 lw        = new Vector3(
                beamEnd.x - dir.x * arrowSize + perp.x * arrowSize * 0.45f,
                beamEnd.y - dir.y * arrowSize + perp.y * arrowSize * 0.45f);
            Vector3 rw        = new Vector3(
                beamEnd.x - dir.x * arrowSize - perp.x * arrowSize * 0.45f,
                beamEnd.y - dir.y * arrowSize - perp.y * arrowSize * 0.45f);

            Handles.color = new Color(col.r, col.g, col.b, 0.90f * t);
            Handles.DrawAAPolyLine(1.8f, lw, tip, rw);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Helpers vẽ hình học
        // ════════════════════════════════════════════════════════════════════════
        private static void DrawCircle(Vector2 center, float radius,
                                       Color fill, Color border, float borderW)
        {
            const int SEG = 48;
            if (radius < 0.5f) return;

            if (fill.a > 0.005f)
            {
                Handles.color = fill;
                var pts = new Vector3[SEG + 1];
                for (int i = 0; i <= SEG; i++)
                {
                    float a = i * 2f * Mathf.PI / SEG;
                    pts[i]  = new Vector3(center.x + Mathf.Cos(a) * radius,
                                          center.y + Mathf.Sin(a) * radius);
                }
                Handles.DrawAAConvexPolygon(pts);
            }
            if (border.a > 0.005f && borderW > 0f)
            {
                Handles.color = border;
                const int BSEG = 64;
                var bpts = new Vector3[BSEG + 1];
                for (int i = 0; i <= BSEG; i++)
                {
                    float a = i * 2f * Mathf.PI / BSEG;
                    bpts[i] = new Vector3(center.x + Mathf.Cos(a) * radius,
                                          center.y + Mathf.Sin(a) * radius);
                }
                Handles.DrawAAPolyLine(borderW, bpts);
            }
        }

        // Diamond cho các nút "cả 3" (Local Transform / World Transform)
        private static void DrawDiamond(Vector2 center, float radius,
                                        Color fill, Color border, float borderW)
        {
            if (radius < 0.5f) return;
            float r     = radius * 1.05f;
            Vector3 top   = new Vector3(center.x,     center.y - r);
            Vector3 right = new Vector3(center.x + r, center.y    );
            Vector3 bot   = new Vector3(center.x,     center.y + r);
            Vector3 left  = new Vector3(center.x - r, center.y    );

            if (fill.a > 0.005f)
            { Handles.color = fill; Handles.DrawAAConvexPolygon(top, right, bot, left); }
            if (border.a > 0.005f && borderW > 0f)
            { Handles.color = border; Handles.DrawAAPolyLine(borderW, top, right, bot, left, top); }
        }

        private static void DrawRectBorder(Rect r, Color color, float width)
        {
            Handles.color = color;
            Handles.DrawAAPolyLine(width,
                new Vector3(r.xMin, r.yMin), new Vector3(r.xMax, r.yMin),
                new Vector3(r.xMax, r.yMax), new Vector3(r.xMin, r.yMax),
                new Vector3(r.xMin, r.yMin));
        }
    }
}
#endif