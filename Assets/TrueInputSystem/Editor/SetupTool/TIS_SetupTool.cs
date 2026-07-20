// ============================================================
//  TrueInputSystem — TISSetupTool.cs  [Partial — Core]
//  Phần 1/5: State, Bảng màu, Vòng đời, OnGUI chính.
//
//  CẢI TIẾN:
//  ─ Tỉ lệ panel tự điều chỉnh theo chiều rộng cửa sổ
//  ─ Thuần Tiếng Việt (giữ thuật ngữ kỹ thuật bằng tiếng Anh)
//  ─ Hệ thống tooltip khi di chuột vào tiêu đề / biến
//
//  ĐÃ LOẠI BỎ: Sirenix.OdinInspector / OdinEditorWindow
//  → Kế thừa thuần UnityEditor.EditorWindow
// ============================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISSetupTool : EditorWindow
    {
        // ══════════════════════════════════════════════════════
        //  MỞ CỬA SỔ
        // ══════════════════════════════════════════════════════

        //[MenuItem("Tools/True Input System/Setup", priority = 1)]
        public static void OpenWindow()
        {
            var win = GetWindow<TISSetupTool>();
            win.titleContent = new GUIContent(
                "TIS Setup",
                EditorGUIUtility.IconContent("Settings").image);
            win.minSize = new Vector2(900f, 560f);
            win.Show();
        }

        // ══════════════════════════════════════════════════════
        //  TRẠNG THÁI
        // ══════════════════════════════════════════════════════

        // ── Màn hình chào ─────────────────────────────────────
        private bool _showLanding = true;
        private TISInputProfileConfig _landingDropTarget = null;

        // Quick-pick grid
        private TISInputProfileConfig[] _allProfiles      = null;  // null = chưa scan
        private Texture2D[]             _allProfileIcons  = null;  // icon tương ứng
        private Vector2                 _profileGridScroll;

        // ── Dữ liệu đang làm việc ─────────────────────────────
        private TISInputProfileConfig _workingProfile;
        private TISInputProfileConfig _sourceAsset;
        private int    _selectedMapIndex  = 0;
        private bool   _isDirty           = false;
        private List<string> _validationErrors = new();
        private bool   _validationRan     = false;
        private string _renameBuffer      = "";
        private bool   _isRenaming        = false;
        private int    _renamingIndex     = -1;

        // ── Vị trí cuộn ───────────────────────────────────────
        private Vector2 _mapListScroll;
        private Vector2 _bindingScroll;
        private Vector2 _settingsScroll;

        // ── Bố cục panel (tỉ lệ % chiều rộng cửa sổ) ─────────
        // Tỉ lệ 1/1/3: Trái=20%, Giữa=20%, Phải=60%
        private const float LEFT_RATIO  = 0.20f;
        private const float RIGHT_RATIO = 0.60f;
        private const float MIN_LEFT_W  = 160f;
        private const float MIN_RIGHT_W = 360f;

        private float LeftPanelW  => Mathf.Max(MIN_LEFT_W,  position.width * LEFT_RATIO);
        private float RightPanelW => Mathf.Max(MIN_RIGHT_W, position.width * RIGHT_RATIO);

        // ── Tab cài đặt ───────────────────────────────────────
        private int _settingsTab = 0;
        private readonly string[] _settingsTabs = { "Mobile", "Camera", "Gamepad" };

        // ── Theo dõi hover ────────────────────────────────────
        private double _lastRepaintTime;

        // ── Cache style ───────────────────────────────────────
        private bool     _stylesBuilt = false;
        private GUIStyle _styleProfileNameField;

        // ── Hệ thống Tooltip ──────────────────────────────────
        private string  _tooltipText       = "";
        private bool    _showTooltip       = false;
        private bool    _tooltipRegistered = false;   // reset mỗi frame
        private Vector2 _tooltipMousePos   = Vector2.zero;
        // Mouse position trong window space — capture trước ScrollView để tránh bị offset
        private Vector2 _windowMousePos    = Vector2.zero;

        // ══════════════════════════════════════════════════════
        //  BẢNG MÀU — Unity Editor Style
        //  Tự động thích nghi Light / Dark skin của Unity.
        // ══════════════════════════════════════════════════════

        private static bool IsDark => EditorGUIUtility.isProSkin;

        // Nền — dựa theo màu nền chuẩn Unity Editor
        internal static Color C_BgDeep       => IsDark ? new Color(0.180f, 0.180f, 0.180f) : new Color(0.760f, 0.760f, 0.760f);
        internal static Color C_BgPanel      => IsDark ? new Color(0.220f, 0.220f, 0.220f) : new Color(0.820f, 0.820f, 0.820f);
        internal static Color C_BgCard       => IsDark ? new Color(0.250f, 0.250f, 0.250f) : new Color(0.860f, 0.860f, 0.860f);
        internal static Color C_BgRowEven    => IsDark ? new Color(0.230f, 0.230f, 0.230f) : new Color(0.840f, 0.840f, 0.840f);
        internal static Color C_BgRowOdd     => IsDark ? new Color(0.245f, 0.245f, 0.245f) : new Color(0.855f, 0.855f, 0.855f);
        internal static Color C_BgRowHover   => IsDark ? new Color(0.270f, 0.270f, 0.270f) : new Color(0.800f, 0.800f, 0.800f);
        internal static Color C_BgMapSel     => IsDark ? new Color(0.173f, 0.365f, 0.529f, 0.90f) : new Color(0.227f, 0.447f, 0.690f, 0.85f);
        internal static Color C_BgMapHov     => IsDark ? new Color(0.260f, 0.260f, 0.260f) : new Color(0.810f, 0.810f, 0.810f);
        internal static Color C_BgColHeader  => IsDark ? new Color(0.200f, 0.200f, 0.200f) : new Color(0.780f, 0.780f, 0.780f);
        internal static Color C_BgToolbar    => IsDark ? new Color(0.195f, 0.195f, 0.195f) : new Color(0.770f, 0.770f, 0.770f);
        internal static Color C_BgBanner     => IsDark ? new Color(0.170f, 0.170f, 0.170f) : new Color(0.750f, 0.750f, 0.750f);
        internal static Color C_BgStatus     => IsDark ? new Color(0.175f, 0.175f, 0.175f) : new Color(0.755f, 0.755f, 0.755f);
        internal static Color C_BgBinding    => IsDark ? new Color(0.215f, 0.230f, 0.250f) : new Color(0.820f, 0.830f, 0.845f);
        internal static Color C_BgBindingEmp => IsDark ? new Color(0.235f, 0.235f, 0.240f) : new Color(0.840f, 0.840f, 0.845f);

        // Màu nhấn — xanh Unity (dùng cho selection, highlight, accent)
        internal static Color C_Accent       => IsDark ? new Color(0.243f, 0.490f, 0.900f) : new Color(0.180f, 0.420f, 0.820f);
        internal static Color C_AccentDim    => IsDark ? new Color(0.180f, 0.350f, 0.620f) : new Color(0.150f, 0.320f, 0.600f);
        internal static Color C_Green        => IsDark ? new Color(0.350f, 0.780f, 0.350f) : new Color(0.200f, 0.580f, 0.200f);
        internal static Color C_Red          => IsDark ? new Color(0.870f, 0.280f, 0.280f) : new Color(0.750f, 0.150f, 0.150f);
        internal static Color C_Orange       => IsDark ? new Color(0.900f, 0.620f, 0.150f) : new Color(0.780f, 0.480f, 0.050f);
        internal static Color C_Purple       => IsDark ? new Color(0.650f, 0.420f, 0.900f) : new Color(0.500f, 0.280f, 0.780f);

        // Màu chữ — theo EditorStyles tiêu chuẩn
        internal static Color C_TextPrimary  => IsDark ? new Color(0.824f, 0.824f, 0.824f) : new Color(0.090f, 0.090f, 0.090f);
        internal static Color C_TextSecond   => IsDark ? new Color(0.600f, 0.600f, 0.600f) : new Color(0.320f, 0.320f, 0.320f);
        internal static Color C_TextDim      => IsDark ? new Color(0.420f, 0.420f, 0.420f) : new Color(0.520f, 0.520f, 0.520f);
        internal static Color C_TextAccent   => IsDark ? new Color(0.400f, 0.670f, 1.000f) : new Color(0.150f, 0.380f, 0.820f);

        // Đường kẻ
        internal static Color C_Divider      => IsDark ? new Color(0.140f, 0.140f, 0.140f) : new Color(0.620f, 0.620f, 0.620f);
        internal static Color C_DividerLight => IsDark ? new Color(0.180f, 0.180f, 0.180f) : new Color(0.680f, 0.680f, 0.680f);

        // Badge theo loại action
        internal static Color C_BadgeBtn     => IsDark ? new Color(0.200f, 0.360f, 0.680f) : new Color(0.180f, 0.320f, 0.640f);
        internal static Color C_BadgeV2      => IsDark ? new Color(0.180f, 0.500f, 0.280f) : new Color(0.150f, 0.440f, 0.220f);
        internal static Color C_BadgeAx      => IsDark ? new Color(0.560f, 0.330f, 0.100f) : new Color(0.500f, 0.280f, 0.060f);

        // ══════════════════════════════════════════════════════
        //  VÒNG ĐỜI
        // ══════════════════════════════════════════════════════

        private void OnEnable()
        {
            _showLanding = (_workingProfile == null);
        }

        private void OnGUI()
        {
            BuildStylesIfNeeded();

            // Capture mouse position NGAY ĐÂY — trước bất kỳ ScrollView nào.
            // Bên trong ScrollView, Event.current.mousePosition bị transform
            // theo scroll offset nên không dùng được cho tooltip overlay.
            var ev = Event.current;
            if (ev.type == EventType.MouseMove ||
                ev.type == EventType.MouseDrag  ||
                ev.type == EventType.Repaint    ||
                ev.type == EventType.Layout)
            {
                _windowMousePos = ev.mousePosition;
            }

            // Nền cửa sổ
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), C_BgDeep);

            if (_showLanding)
            {
                DrawLandingScreen();
                DrawTooltipOverlay();
                return;
            }

            DrawBanner();
            DrawToolbar();
            DrawMainArea();
            DrawStatusBar();
            DrawTooltipOverlay();

            if (ev.type == EventType.MouseMove)
                Repaint();
        }

        private void OnDestroy()
        {
            if (_isDirty)
            {
                bool save = EditorUtility.DisplayDialog(
                    "True Input System — Chưa Lưu",
                    "Có thay đổi chưa được lưu vào file .asset.\nBạn có muốn lưu trước khi đóng không?",
                    "Lưu", "Bỏ qua");
                if (save) GenerateAsset();
            }
        }

        // ══════════════════════════════════════════════════════
        //  XÂY DỰNG STYLE
        // ══════════════════════════════════════════════════════

        private void BuildStylesIfNeeded()
        {
            if (_stylesBuilt) return;
            _stylesBuilt = true;

            _styleProfileNameField = new GUIStyle(EditorStyles.textField)
            {
                normal    = { textColor = C_TextPrimary },
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                padding   = new RectOffset(6, 4, 0, 0),
                alignment = TextAnchor.MiddleLeft,
            };
        }

        // ══════════════════════════════════════════════════════
        //  HỆ THỐNG TOOLTIP
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Vẽ lớp tooltip trên cùng. Gọi ở cuối mỗi frame.
        /// </summary>
        private void DrawTooltipOverlay()
        {
            // Reset cờ sau khi vẽ xong — nếu frame tiếp theo không có
            // RegisterTooltip nào gọi thì _showTooltip sẽ = false
            if (Event.current.type == EventType.Layout)
            {
                if (!_tooltipRegistered)
                {
                    _showTooltip = false;
                    _tooltipText = "";
                }
                _tooltipRegistered = false;
            }

            if (!_showTooltip || string.IsNullOrEmpty(_tooltipText)) return;
            if (Event.current.type != EventType.Repaint) return;

            var tipStyle = new GUIStyle()
            {
                wordWrap  = true,
                richText  = true,
                fontSize  = 10,
                normal    = { textColor = new Color(0.87f, 0.90f, 0.94f) },
                padding   = new RectOffset(8, 8, 6, 6),
            };

            var content = new GUIContent(_tooltipText);
            float maxW  = Mathf.Min(300f, position.width * 0.35f);
            float w     = Mathf.Min(maxW, _tooltipText.Length * 6.5f + 20f);
            w           = Mathf.Max(w, 120f);
            float h     = tipStyle.CalcHeight(content, w) + 12f;

            // Góc trái-trên của box = vị trí chuột + offset nhỏ
            float offsetX = 14f;
            float offsetY = 14f;
            float x = _tooltipMousePos.x + offsetX;
            float y = _tooltipMousePos.y + offsetY;

            // Đẩy vào trong nếu tràn cạnh phải / dưới
            if (x + w > position.width  - 4f) x = _tooltipMousePos.x - w - 4f;
            if (y + h > position.height - 4f) y = _tooltipMousePos.y - h - 4f;
            x = Mathf.Max(4f, x);
            y = Mathf.Max(4f, y);

            var bgRect = new Rect(x, y, w, h);

            // Bóng đổ
            EditorGUI.DrawRect(new Rect(bgRect.x + 2f, bgRect.y + 2f, bgRect.width, bgRect.height),
                new Color(0f, 0f, 0f, 0.45f));
            // Nền
            EditorGUI.DrawRect(bgRect, new Color(0.06f, 0.07f, 0.10f, 0.98f));
            DrawBorder(bgRect, C_Accent * 0.35f, 1f);
            // Sọc nhấn trái
            EditorGUI.DrawRect(new Rect(bgRect.x, bgRect.y, 2f, bgRect.height), C_Accent * 0.7f);

            GUI.Label(bgRect, _tooltipText, tipStyle);
        }

        /// <summary>
        /// Đăng ký tooltip cho một rect. Gọi ngay sau khi vẽ phần tử UI.
        /// </summary>
        internal void RegisterTooltip(Rect rect, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            // Dùng Event.current.mousePosition (local/scroll space) để check
            // xem chuột có đang hover đúng rect không — vì rect cũng ở local space.
            // Dùng _windowMousePos (window space) để VẼ tooltip ở đúng vị trí chuột.
            if (!rect.Contains(Event.current.mousePosition)) return;

            _tooltipText       = text;
            _tooltipMousePos   = _windowMousePos;   // window space → vẽ đúng chỗ
            _showTooltip       = true;
            _tooltipRegistered = true;
        }

        /// <summary>
        /// Vẽ nhãn có tooltip. Trả về rect của nhãn.
        /// </summary>
        internal Rect LabelWithTooltip(Rect rect, string label, GUIStyle style, string tooltip)
        {
            GUI.Label(rect, label, style);
            RegisterTooltip(rect, tooltip);
            return rect;
        }

        // ══════════════════════════════════════════════════════
        //  TIỆN ÍCH DÙNG CHUNG
        // ══════════════════════════════════════════════════════

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        /// <summary>Đặt cờ dirty và reset trạng thái validation.</summary>
        private void MarkDirty()
        {
            _isDirty       = true;
            _validationRan = false;
            Repaint();
        }

        /// <summary>Ghi nhận Undo trên asset nguồn nếu có.</summary>
        private void SafeRecord(string op)
        {
            if (_workingProfile == null) return;
            if (_sourceAsset != null)
                Undo.RecordObject(_sourceAsset, op);
        }

        /// <summary>Hỏi người dùng có muốn bỏ thay đổi chưa lưu không.</summary>
        private bool ConfirmDiscardChanges()
        {
            if (!_isDirty) return true;
            return EditorUtility.DisplayDialog(
                "Có thay đổi chưa lưu",
                "Thay đổi hiện tại sẽ bị mất. Tiếp tục không?",
                "Tiếp tục", "Huỷ");
        }

        /// <summary>Tạo tên duy nhất tránh trùng trong tập hợp đã có.</summary>
        internal static string GetUniqueName(string baseName, IEnumerable<string> existing)
        {
            var set = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            if (!set.Contains(baseName)) return baseName;
            for (int i = 2; i < 100; i++)
            {
                string c = $"{baseName}_{i}";
                if (!set.Contains(c)) return c;
            }
            return baseName + "_" + Guid.NewGuid().ToString("N").Substring(0, 4);
        }

        /// <summary>Tính chiều rộng nút dựa vào độ dài nhãn.</summary>
        internal static float CalcButtonWidth(string label) =>
            Mathf.Max(60f, label.Length * 7f);
    }
}
#endif