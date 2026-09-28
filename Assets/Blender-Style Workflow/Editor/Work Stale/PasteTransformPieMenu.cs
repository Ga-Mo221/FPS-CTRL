// PasteTransformPieMenu.cs
// Đặt file này vào: Assets/Editor/PasteTransformPieMenu.cs
//
// ── Pie menu dán (paste) lại Transform đã copy bằng CopyTransformPieMenu ────
//
//  ★ PHÍM TẮT DÙNG CHUNG VỚI CopyTransformPieMenu ★
//  Không còn phím tắt riêng cho Paste nữa — Copy và Paste giờ dùng CHUNG 1 tổ
//  hợp phím (đổi tại Edit → Preferences → Blender Editor Tools → tab "Copy
//  Transform Pie"; tab "Paste Transform Pie" chỉ hiển thị/chỉnh lại cùng giá
//  trị đó cho tiện).
//
//  Paste KHÔNG bao giờ tự mở mới. Cách vào Paste duy nhất: giữ modifier (VD
//  Shift), gõ phím chính (VD X) rồi thả nhanh → pie Copy hiện & ở lại màn
//  hình (Toggle Mode). Vẫn giữ modifier, gõ phím chính THÊM LẦN NỮA → menu
//  CHUYỂN NGAY từ Copy sang Paste tại chỗ, không tắt/bật lại. Gõ tiếp lại quay
//  về Copy — cứ so le mỗi lần gõ. Xem CopyTransformPieMenu.SwitchInAsToggle()
//  và SwitchInAsToggle() bên dưới.
//
//  Đọc giá trị trực tiếp từ các field static của CopyTransformPieMenu
//  (LastLocalPosition, LastWorldRotation, v.v.) — không parse Clipboard text,
//  nên chính xác tuyệt đối (giữ nguyên Quaternion, không mất độ chính xác do
//  làm tròn khi hiển thị Euler).
//
//  Nút nào chưa có giá trị tương ứng đã được copy sẽ hiển thị mờ (disabled)
//  và không làm gì nếu chọn trúng — tránh dán nhầm giá trị rác.
//
//  [HOLD MODE]   Nhấn GIỮ phím tắt → menu hiện, lia chuột để chọn, thả để dán
//  [TOGGLE MODE] Nhấn thả nhanh → menu mở cố định, click để chọn
//  Phím 1–8 chọn nhanh khi menu đang mở. Escape / RMB để đóng không làm gì.
//
//  ┌─────────────────────────── Bố cục 8 nút ───────────────────────────┐
//  │                         [TRÊN]  Paste Local Transform (cả 3)        │
//  │                                                                     │
//  │   [TRÁI-TRÊN] Paste Local Position                                  │
//  │   [TRÁI-GIỮA] Paste Local Rotation    Paste World Position [PHẢI-TRÊN]│
//  │   [TRÁI-DƯỚI] Paste Local Scale       Paste World Rotation [PHẢI-GIỮA]│
//  │                                        Paste World Scale   [PHẢI-DƯỚI]│
//  │                         [DƯỚI]   Paste World Transform (cả 3)       │
//  └─────────────────────────────────────────────────────────────────────┘
// ─────────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    [InitializeOnLoad]
    public static class PasteTransformPieMenu
    {
        // ════════════════════════════════════════════════════════════════════════
        //  Hằng số animation
        // ════════════════════════════════════════════════════════════════════════
        private const float ANIM_SPEED = 10f;

        // Ở Toggle Mode (đã thả phím tắt nhưng menu vẫn mở), nếu người dùng kéo chuột ra
        // xa quá bán kính này (tính theo bội số của BackdropR) thì coi như đã "chốt" hướng
        // muốn chọn — tự động paste & đóng menu, không bắt buộc phải click chuột trái nữa.
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
        //  Phím tắt DÙNG CHUNG với CopyTransformPieMenu — xem ghi chú lớn ở đầu
        //  CopyTransformPieMenu.cs. Toàn bộ Enabled/HotKey/UseShift/UseAlt/UseCtrl/
        //  MatchesHotKey/HotKeyDisplayString bên dưới trỏ THẲNG về CopyTransformPieMenu
        //  (không còn EditorPrefs Key riêng "PTPM_*" nữa) để đảm bảo Copy và Paste luôn
        //  phản hồi đúng 1 tổ hợp phím vật lý, không thể lệch nhau.
        // ════════════════════════════════════════════════════════════════════════
        public static bool    Enabled  { get => CopyTransformPieMenu.Enabled;  set => CopyTransformPieMenu.Enabled  = value; }
        public static KeyCode HotKey   { get => CopyTransformPieMenu.HotKey;   set => CopyTransformPieMenu.HotKey   = value; }
        public static bool    UseShift { get => CopyTransformPieMenu.UseShift; set => CopyTransformPieMenu.UseShift = value; }
        public static bool    UseAlt   { get => CopyTransformPieMenu.UseAlt;   set => CopyTransformPieMenu.UseAlt   = value; }
        public static bool    UseCtrl  { get => CopyTransformPieMenu.UseCtrl;  set => CopyTransformPieMenu.UseCtrl  = value; }

        public static bool MatchesHotKey(Event e) => CopyTransformPieMenu.MatchesHotKey(e);

        public static string HotKeyDisplayString() => CopyTransformPieMenu.HotKeyDisplayString();

        // ════════════════════════════════════════════════════════════════════════
        //  Định nghĩa 8 slot paste — góc giống hệt CopyTransformPieMenu (45° đều nhau)
        // ════════════════════════════════════════════════════════════════════════
        private enum PasteKind
        {
            LocalPosition, LocalRotation, LocalScale, LocalAll,
            WorldPosition, WorldRotation, WorldScale, WorldAll,
        }

        private struct PasteEntry
        {
            public string    Label;
            public string    SubLabel;
            public Color     IconColor;
            public float     AngleDeg;
            public PasteKind Kind;
            public bool      IsCombo;
        }

        private static readonly Color s_LocalColor = new Color(0.30f, 0.69f, 0.91f); // xanh dương – Local
        private static readonly Color s_WorldColor = new Color(0.91f, 0.60f, 0.26f); // cam – World

        private static readonly PasteEntry[] s_Entries =
        {
            // 0 – Trái trên (-135°): Local Position
            new PasteEntry { Label="Position", SubLabel="LOCAL", IconColor=s_LocalColor, AngleDeg=-135f, Kind=PasteKind.LocalPosition },
            // 1 – Trái giữa (180°): Local Rotation
            new PasteEntry { Label="Rotation", SubLabel="LOCAL", IconColor=s_LocalColor, AngleDeg=180f, Kind=PasteKind.LocalRotation },
            // 2 – Trái dưới (135°): Local Scale
            new PasteEntry { Label="Scale",    SubLabel="LOCAL", IconColor=s_LocalColor, AngleDeg=135f, Kind=PasteKind.LocalScale },
            // 3 – Trên (-90°): Local Transform (cả 3)
            new PasteEntry { Label="Local",    SubLabel="ALL 3", IconColor=s_LocalColor, AngleDeg=-90f, Kind=PasteKind.LocalAll, IsCombo=true },
            // 4 – Phải trên (-45°): World Position
            new PasteEntry { Label="Position", SubLabel="WORLD", IconColor=s_WorldColor, AngleDeg=-45f, Kind=PasteKind.WorldPosition },
            // 5 – Phải giữa (0°): World Rotation
            new PasteEntry { Label="Rotation", SubLabel="WORLD", IconColor=s_WorldColor, AngleDeg=0f,   Kind=PasteKind.WorldRotation },
            // 6 – Phải dưới (45°): World Scale
            new PasteEntry { Label="Scale",    SubLabel="WORLD", IconColor=s_WorldColor, AngleDeg=45f,  Kind=PasteKind.WorldScale },
            // 7 – Dưới (90°): World Transform (cả 3)
            new PasteEntry { Label="World",    SubLabel="ALL 3", IconColor=s_WorldColor, AngleDeg=90f,  Kind=PasteKind.WorldAll, IsCombo=true },
        };

        private static readonly string[] s_FullNames =
        {
            "Paste Local Position  [1]",
            "Paste Local Rotation  [2]",
            "Paste Local Scale  [3]",
            "Paste Local Transform (Pos + Rot + Scale)  [4]",
            "Paste World Position  [5]",
            "Paste World Rotation  [6]",
            "Paste World Scale  [7]",
            "Paste World Transform (Pos + Rot + Scale)  [8]",
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
        public static bool IsOpen => s_Mode != MenuMode.Closed;

        static PasteTransformPieMenu()
        {
            SceneView.duringSceneGui += OnSceneGUI;

            // Gộp trạng thái "đang mở" của CẢ 4 pie menu thành 1 nguồn sự thật duy nhất.
            // Idempotent — 3 file pie menu còn lại cũng gán cùng nội dung lambda này.
            // Xem BlenderInputPriority.cs.
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
                // Paste KHÔNG bao giờ tự mở mới từ trạng thái đóng — chỉ có thể được
                // "chuyển vào" (switch) từ Copy đang mở Toggle Mode, xem SwitchInAsToggle()
                // bên dưới và nhánh tương ứng trong CopyTransformPieMenu.HandleKeyboard().
                // Gõ phím tắt khi Paste đang đóng sẽ luôn do CopyTransformPieMenu xử lý.
                if (s_Mode == MenuMode.Closed)
                    return;

                // Đến đây chắc chắn s_Mode == Toggle (Paste chỉ có thể ở Toggle, không có
                // đường nào dẫn vào Hold Mode của chính nó nữa). Gõ phím tắt thêm 1 lần
                // nữa (modifier vẫn giữ) → chuyển thẳng sang Copy, KHÔNG đóng-rồi-mở-lại.
                if (BlenderInputPriority.IsTransformSessionActive() ||
                    CreateObjectPieMenu.IsOpen || ViewAxisPieMenu.IsOpen)
                    return;

                e.Use();
                s_KeyHeld     = true;
                s_KeyDownTime = EditorApplication.timeSinceStartup;
                Vector2 center = s_Center;
                ForceClose();
                CopyTransformPieMenu.SwitchInAsToggle(center);
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
                            PasteEntryAt(s_HoveredIdx);
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
                if (idx >= 0) { e.Use(); PasteEntryAt(idx); ForceClose(); }
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

        /// Được CopyTransformPieMenu gọi khi người dùng gõ phím tắt thêm 1 lần nữa
        /// trong lúc Copy đang mở dạng Toggle (modifier vẫn giữ) — chuyển thẳng
        /// sang Paste tại đúng vị trí đó, KHÔNG đóng-rồi-mở-lại (không chớp/flicker).
        public static void SwitchInAsToggle(Vector2 center)
        {
            if (Selection.activeTransform == null) return; // không có gì để dán vào thì thôi, coi như đóng hẳn

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
                PasteEntryAt(s_HoveredIdx);
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
                { e.Use(); PasteEntryAt(s_HoveredIdx); ForceClose(); }
                else if (s_DeltaMag > BackdropR)
                { e.Use(); ForceClose(); }
            }
            if (e.type == EventType.MouseDown && e.button == 1)
            { e.Use(); ForceClose(); }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Paste logic
        // ════════════════════════════════════════════════════════════════════════

        /// True nếu slot này đang có giá trị copy sẵn sàng để dán.
        private static bool HasValue(PasteKind kind)
        {
            switch (kind)
            {
                case PasteKind.LocalPosition: return CopyTransformPieMenu.LastLocalPosition.HasValue;
                case PasteKind.LocalRotation: return CopyTransformPieMenu.LastLocalRotation.HasValue;
                case PasteKind.LocalScale:    return CopyTransformPieMenu.LastLocalScale.HasValue;
                case PasteKind.LocalAll:      return CopyTransformPieMenu.LastLocalPosition.HasValue
                                                   || CopyTransformPieMenu.LastLocalRotation.HasValue
                                                   || CopyTransformPieMenu.LastLocalScale.HasValue;
                case PasteKind.WorldPosition: return CopyTransformPieMenu.LastWorldPosition.HasValue;
                case PasteKind.WorldRotation: return CopyTransformPieMenu.LastWorldRotation.HasValue;
                case PasteKind.WorldScale:    return CopyTransformPieMenu.LastWorldScale.HasValue;
                case PasteKind.WorldAll:      return CopyTransformPieMenu.LastWorldPosition.HasValue
                                                   || CopyTransformPieMenu.LastWorldRotation.HasValue
                                                   || CopyTransformPieMenu.LastWorldScale.HasValue;
                default: return false;
            }
        }

        private static void PasteEntryAt(int index)
        {
            Transform t = Selection.activeTransform;
            if (t == null)
            {
                Notify("Không có object nào được chọn");
                return;
            }

            PasteEntry entry = s_Entries[index];

            if (!HasValue(entry.Kind))
            {
                Notify($"Chưa copy {entry.SubLabel} {entry.Label} — không có gì để dán");
                return;
            }

            Undo.RecordObject(t, $"Paste {entry.SubLabel} {entry.Label}");

            switch (entry.Kind)
            {
                case PasteKind.LocalPosition:
                    t.localPosition = CopyTransformPieMenu.LastLocalPosition.Value;
                    break;

                case PasteKind.LocalRotation:
                    t.localRotation = CopyTransformPieMenu.LastLocalRotation.Value;
                    break;

                case PasteKind.LocalScale:
                    t.localScale = CopyTransformPieMenu.LastLocalScale.Value;
                    break;

                case PasteKind.LocalAll:
                    if (CopyTransformPieMenu.LastLocalPosition.HasValue) t.localPosition = CopyTransformPieMenu.LastLocalPosition.Value;
                    if (CopyTransformPieMenu.LastLocalRotation.HasValue) t.localRotation = CopyTransformPieMenu.LastLocalRotation.Value;
                    if (CopyTransformPieMenu.LastLocalScale.HasValue)    t.localScale    = CopyTransformPieMenu.LastLocalScale.Value;
                    break;

                case PasteKind.WorldPosition:
                    t.position = CopyTransformPieMenu.LastWorldPosition.Value;
                    break;

                case PasteKind.WorldRotation:
                    t.rotation = CopyTransformPieMenu.LastWorldRotation.Value;
                    break;

                case PasteKind.WorldScale:
                    SetWorldScale(t, CopyTransformPieMenu.LastWorldScale.Value);
                    break;

                case PasteKind.WorldAll:
                    if (CopyTransformPieMenu.LastWorldPosition.HasValue) t.position = CopyTransformPieMenu.LastWorldPosition.Value;
                    if (CopyTransformPieMenu.LastWorldRotation.HasValue) t.rotation = CopyTransformPieMenu.LastWorldRotation.Value;
                    if (CopyTransformPieMenu.LastWorldScale.HasValue)    SetWorldScale(t, CopyTransformPieMenu.LastWorldScale.Value);
                    break;
            }

            Notify($"Đã dán {entry.SubLabel} {entry.Label} cho \"{t.name}\"");
        }

        /// Gán world (lossy) scale cho transform bằng cách quy đổi ngược qua scale
        /// của parent. Xấp xỉ theo từng trục (component-wise) — đủ dùng cho trường
        /// hợp thông thường, có thể lệch nếu parent bị xoay lệch trục kèm scale
        /// không đều (giới hạn cố hữu của khái niệm "world scale" trong Unity).
        private static void SetWorldScale(Transform t, Vector3 worldScale)
        {
            if (t.parent == null)
            {
                t.localScale = worldScale;
                return;
            }

            Vector3 parentLossy = t.parent.lossyScale;
            t.localScale = new Vector3(
                Mathf.Approximately(parentLossy.x, 0f) ? worldScale.x : worldScale.x / parentLossy.x,
                Mathf.Approximately(parentLossy.y, 0f) ? worldScale.y : worldScale.y / parentLossy.y,
                Mathf.Approximately(parentLossy.z, 0f) ? worldScale.z : worldScale.z / parentLossy.z);
        }

        private static void Notify(string msg)
        {
            //Debug.Log($"[PasteTransformPieMenu] {msg}");
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

            // Direction beam (Hold mode) — chỉ vẽ nếu slot đang hover có giá trị
            if (s_Mode == MenuMode.Hold && s_DeltaMag > DeadZone && s_HoveredIdx >= 0
                && HasValue(s_Entries[s_HoveredIdx].Kind))
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

            // Nút paste
            for (int i = 0; i < s_Entries.Length; i++)
                DrawPasteButton(i, t);

            DrawCenterDot(t);

            if (s_HoveredIdx >= 0 && t > 0.4f)
                DrawTooltip(s_HoveredIdx, t);

            DrawModeHint(t);

            Handles.EndGUI();
        }

        private static void DrawPasteButton(int index, float t)
        {
            PasteEntry entry   = s_Entries[index];
            bool       hasVal  = HasValue(entry.Kind);
            float      rad     = entry.AngleDeg * Mathf.Deg2Rad;
            Vector2    pos     = s_Center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * MenuRadius * t;

            bool  hovered = s_HoveredIdx == index;
            float scale   = hovered ? HoverScale : 1f;
            float r       = ButtonSize * 0.5f * t * scale;

            // Slot chưa có giá trị copy → vẽ mờ hẳn (disabled), không tô màu icon dù hover
            float dimA = hasVal ? 1f : 0.35f;

            Color bg = hovered && hasVal
                ? new Color(entry.IconColor.r, entry.IconColor.g, entry.IconColor.b, 0.26f * t)
                : new Color(0.16f, 0.16f, 0.16f, 0.93f * t * dimA);

            Color border = hovered && hasVal
                ? new Color(entry.IconColor.r, entry.IconColor.g, entry.IconColor.b, 1.00f * t)
                : new Color(0.44f, 0.44f, 0.44f, 0.50f * t * dimA);

            if (entry.IsCombo)
                DrawDiamond(pos, r, bg, border, hovered ? 2.0f : 0.75f);
            else
                DrawCircle(pos, r, bg, border, hovered ? 2.0f : 0.75f);

            if (hovered && hasVal)
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

            Color labelColor = hasVal
                ? new Color(entry.IconColor.r, entry.IconColor.g, entry.IconColor.b, t)
                : new Color(0.5f, 0.5f, 0.5f, t * 0.6f);

            GUIStyle lbl = new GUIStyle(GUI.skin.label)
            {
                fontSize  = Mathf.RoundToInt(BtnLabelFont * t * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = labelColor }
            };
            GUI.Label(new Rect(pos.x - r, pos.y - r * 0.50f, r * 2f, r * 0.95f), entry.Label, lbl);

            GUIStyle sub = new GUIStyle(GUI.skin.label)
            {
                fontSize  = Mathf.RoundToInt(BtnSubFont * t * scale),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(0.72f, 0.72f, 0.72f, t * 0.85f * dimA) }
            };
            GUI.Label(new Rect(pos.x - r, pos.y + r * 0.08f, r * 2f, r * 0.55f), entry.SubLabel, sub);
        }

        private static void DrawCenterDot(float t)
        {
            float cr = CenterR * t;
            DrawCircle(s_Center, cr,
                new Color(0.20f, 0.13f, 0.16f, 0.95f * t),
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
                "PASTE", cs);
        }

        private static void DrawTooltip(int index, float t)
        {
            PasteEntry entry  = s_Entries[index];
            string     txt    = s_FullNames[index];
            bool       hasVal = HasValue(entry.Kind);

            string objInfo = !hasVal
                ? "→ (chưa copy giá trị này)"
                : Selection.activeGameObject != null
                    ? $"→ {Selection.activeGameObject.name}"
                    : "→ (no selection)";

            Color txtColor = hasVal
                ? new Color(entry.IconColor.r, entry.IconColor.g, entry.IconColor.b, t)
                : new Color(0.6f, 0.6f, 0.6f, t);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize  = TooltipFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = txtColor }
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
            DrawRectBorder(bg, new Color(txtColor.r, txtColor.g, txtColor.b, 0.50f * t), 0.75f);

            Rect r1 = new Rect(bg.x, bg.y + 5f, bg.width, sz1.y + 2f);
            Rect r2 = new Rect(bg.x, r1.yMax,   bg.width, sz2.y + 4f);
            GUI.Label(r1, txt,     style);
            GUI.Label(r2, objInfo, objStyle);
        }

        private static void DrawModeHint(float t)
        {
            if (t < 0.5f) return;
            float alpha = (t - 0.5f) * 2f;

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