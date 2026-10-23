// GaMoInspector.Core.cs  -  part of CustomsEditor (GaMo Inspector)
// Khai bao class, mau sac/const, GUIStyle dung chung, RefreshStylesIfNeeded
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UEditor = UnityEditor.Editor;
using UIE = UnityEngine.UIElements;
using UEUI = UnityEditor.UIElements;
using UObject = UnityEngine.Object;

namespace GaMo.BlenderEditorTools
{
    public partial class CustomsEditor : EditorWindow, ISerializationCallbackReceiver, IHasCustomMenu
    {
        static readonly Color Accent = new Color(0.918f, 0.376f, 0.180f); // cam #EA602E (theo icon Event.png)
        static readonly Color LockColor = Accent;
        const float IconPx = 16f;
        static readonly Color DisabledColor = new Color(0.45f, 0.45f, 0.50f);

        // ---------- Kích thước nút component (zoom), chỉnh bằng thanh trượt ở TopBar ----------
        const float MinTabScale = 0.7f;
        const float MaxTabScale = 1.8f;
        const float TabSpacing = 5f; // khoảng cách giữa các nút component (ngang + dọc), tự nhân theo _tabScale. Chỉnh số này để tăng/giảm.
        const string TabScalePrefKey = "GaMo.CustomsEditor.TabScale";
        [SerializeField] float _tabScale = 1f;

        static void SetTextColor(GUIStyle st, Color col)
        {
            st.normal.textColor = col;  st.hover.textColor = col;  st.active.textColor = col;  st.focused.textColor = col;
            st.onNormal.textColor = col; st.onHover.textColor = col; st.onActive.textColor = col; st.onFocused.textColor = col;
        }

        // Màu chữ nút mặc định của Editor. KHÔNG lấy GUI.skin.button.normal.textColor: ở Unity 6 giá trị này có thể là (0,0,0)
        // (skin chưa nạp màu / đang là skin game) => chữ nút không chọn bị đen trên dark skin. Lấy từ miniButton, nếu
        // không hợp lệ (trong suốt hoặc tương phản kém với skin hiện tại) thì dùng màu chữ label.
        static Color DefaultButtonTextColor()
        {
            var col = EditorStyles.miniButton.normal.textColor;
            float l = 0.299f * col.r + 0.587f * col.g + 0.114f * col.b;
            if (col.a < 0.5f || (EditorGUIUtility.isProSkin ? l < 0.5f : l > 0.5f))
                col = EditorStyles.label.normal.textColor;
            return col;
        }

        // Style nút tab không có nền (nền do box chung vẽ), tạo lười vì GUI.skin chỉ dùng được trong OnGUI.
        [NonSerialized] GUIStyle _tabLabelStyle;
        GUIStyle TabLabelStyle
        {
            get
            {
                if (_tabLabelStyle == null)
                {
                    // Lấy từ EditorStyles.label (không có nền) — KHÔNG copy từ GUI.skin.button rồi gán background = null,
                    // vì cách đó không xóa được nền ở một số bản Unity => nút xám đè lên nền cam vẽ bên dưới.
                    _tabLabelStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = false,
                        clipping = TextClipping.Clip,
                        stretchWidth = true,
                        padding = new RectOffset(2, 2, 0, 0),
                        margin = new RectOffset(0, 0, 0, 0),
                    };
                    SetTextColor(_tabLabelStyle, DefaultButtonTextColor()); // chữ cùng màu với nút mặc định
                }
                return _tabLabelStyle;
            }
        }

        // Style chữ của nút component, co giãn cỡ chữ theo _tabScale. Tách riêng khỏi TabLabelStyle
        // vì SolidTextStyle (Add Component, Lock ở TopBar) kế thừa TabLabelStyle và KHÔNG được co giãn theo.
        [NonSerialized] GUIStyle _scaledTabLabelStyle;
        float _scaledTabLabelStyleAt = -1f;
        GUIStyle ScaledTabLabelStyle
        {
            get
            {
                if (_scaledTabLabelStyle == null || !Mathf.Approximately(_scaledTabLabelStyleAt, _tabScale))
                {
                    _scaledTabLabelStyle = new GUIStyle(TabLabelStyle);
                    int baseSize = EditorStyles.label.fontSize;
                    if (baseSize <= 0) baseSize = 11; // 0 nghĩa là "theo skin mặc định" => tự quy ước 11px
                    _scaledTabLabelStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(baseSize * _tabScale));
                    _scaledTabLabelStyleAt = _tabScale;
                }
                return _scaledTabLabelStyle;
            }
        }

        // Style chữ trắng, nền trong suốt: dùng cho các nút nền cam đặc (Add Component, Lock...).
        // GUIContent của TopBar: dựng 1 lần, xóa cùng lúc với style (RefreshStylesIfNeeded) để theo kịp đổi skin / đổi zoom.
        GUIContent _addGC, _lockOnGC, _lockOffGC, _zoomOutGC, _zoomInGC;
        static readonly GUIContent _backGC = new GUIContent("‹");
        static readonly GUIContent _fwdGC = new GUIContent("›");
        // Style nút của TopBar: bản sao của GUI.skin.button nhưng ép màu chữ tường minh (lấy từ miniButton, loại nút đang hiện chữ bình thường
        // ở Open/Select/Overrides) để chữ không phụ thuộc trạng thái màu của skin.
        [NonSerialized] GUIStyle _topBtnStyle;
        GUIStyle TopBtnStyle
        {
            get
            {
                if (_topBtnStyle == null)
                {
                    _topBtnStyle = new GUIStyle(GUI.skin.button);
                    SetTextColor(_topBtnStyle, DefaultButtonTextColor());
                }
                return _topBtnStyle;
            }
        }
        [NonSerialized] GUIStyle _solidTextStyle;
        GUIStyle SolidTextStyle
        {
            get
            {
                if (_solidTextStyle == null)
                {
                    _solidTextStyle = new GUIStyle(TabLabelStyle) { fontStyle = FontStyle.Bold };
                    SetTextColor(_solidTextStyle, Color.white);
                }
                return _solidTextStyle;
            }
        }

        // ---------- Làm mới style khi môi trường GUI đổi ----------
        // Style + bề rộng chữ được cache. Khi đổi màn hình (DPI khác), đổi skin, hoặc font gốc của Editor đổi,
        // cache cũ không còn đúng => chữ to hơn ô nút (lỗi tràn chữ). Gắn "chữ ký" môi trường, đổi là dựng lại.
        // NonSerialized: EditorWindow serialize cả field private khi domain reload => style cũ (vd chữ đen) sẽ sống mãi nếu không dựng lại.
        [NonSerialized] bool _styleInit;
        int _sigFont; float _sigPpp; bool _sigPro; float _sigScale;

        void RefreshStylesIfNeeded()
        {
            int font = EditorStyles.label.fontSize;
            float ppp = EditorGUIUtility.pixelsPerPoint;
            bool pro = EditorGUIUtility.isProSkin;
            if (_styleInit && font == _sigFont && Mathf.Approximately(ppp, _sigPpp)
                && pro == _sigPro && Mathf.Approximately(_tabScale, _sigScale)) return;
            _styleInit = true; _sigFont = font; _sigPpp = ppp; _sigPro = pro; _sigScale = _tabScale;
            _tabLabelStyle = null;
            _scaledTabLabelStyle = null;
            _scaledTabLabelStyleAt = -1f;
            _solidTextStyle = null;
            _topBtnStyle = null;
            _lockBtnStyle = null;
            _tabTxt = null;
            _addGC = _lockOnGC = _lockOffGC = _zoomOutGC = _zoomInGC = null;
            _textWCache.Clear();
            _textWCacheScale = -1f;
            _iconCache.Clear(); _iconMissing.Clear(); _tabContentCache.Clear(); // đổi skin / DPI => icon dựng lại
        }

        GameObject _go;                                   // object "chính" (active)
        readonly List<GameObject> _gos = new List<GameObject>(); // tất cả object đang chọn (multi-edit)
        [SerializeField] bool _lock;
        [SerializeField] UObject _lockedObj;              // object/asset đang khóa (giữ qua recompile / Play)
        // ---------- UI Toolkit shell (xem mục UI Toolkit bên dưới) ----------
        UIE.VisualElement _uiRoot;
        UIE.IMGUIContainer _topIm, _previewIm;            // vùng trên cùng (TopBar/Header) và Preview ở đáy, cố định ngoài vùng cuộn
        UIE.VisualElement _previewSplit;                  // thanh kéo đổi chiều cao Preview (UI Toolkit thuần, nằm NGOÀI _previewIm)
        float _splitDragStartY, _splitDragStartH;
        bool _hasPreviewEd;                               // có Editor để vẽ Preview không (cập nhật ở SyncUI)
        UEditor _previewEdCache;                          // editor đang vẽ Preview ở lần OnGUI gần nhất (để Update() hỏi RequiresConstantRepaint)
        static readonly bool DebugPreviewFps = false;                // true: tiêu đề Preview hiện số lần vẽ/giây (để so với Inspector gốc)
        int _fpsCount; double _fpsT; float _fps;
        const double PreviewFrameTime = 1.0 / 60.0 - 0.002; // khoảng cách tối thiểu giữa 2 lần xin vẽ Preview (~60 fps)
        double _pumpAt;
        bool _previewConstant;                            // kết quả RequiresConstantRepaint() đo TRONG OnGUI (Repaint): Update() hỏi ngoài OnGUI có thể ra false
        int _dbgLive; int[] _dbgWhy = new int[7];          // debug: live = số lần RequiresConstantRepaint() ngoài OnGUI trả true; why = số lần PreviewPump thoát ở từng điều kiện A..F
        int _dbgTick, _dbgPass; double _dbgT; string _dbgText = ""; // debug: tick = số lần EditorApplication.update chạy/giây, pass = số lần xin vẽ Preview/giây
        const float PreviewBarH = 21f;                    // chiều cao thanh tiêu đề Preview (khớp HorizontalScope trong DrawPreviewPanel)
        UIE.ScrollView _scrollV;                          // vùng cuộn: thanh nút component + các panel
        readonly List<UIE.IMGUIContainer> _bodyChunks = new List<UIE.IMGUIContainer>();
        List<object> _uiSig = new List<object>();         // "chữ ký" cấu trúc panel hiện tại: đổi mới dựng lại cây UI
        List<object> _uiSigNew = new List<object>();
        readonly List<PanelModel> _models = new List<PanelModel>();
        bool _syncQueued, _dropArmed;
        bool _syncFailed;                                 // lần dựng UI gần nhất bị lỗi (chỉ log 1 lần, tự thử lại ở nhịp sau)
        int _tick;
        int _hoverTab = -1;
        bool _scalePrefDirty;
        GameObject[] _validCache;                         // ValidGos() cache theo frame GUI
        bool _mixActive, _mixName, _mixStatic, _mixTag, _mixLayer; // tính 1 lần ở Layout
        bool _pfInstance, _pfAsset, _pfHasRoot;         // Prefab bar: tính 1 lần ở Layout
        string _pfPath = "";

        // Các component đang mở (nhiều tab)
        readonly HashSet<Component> _selected = new HashSet<Component>();
        HashSet<Type> _lastTypes = new HashSet<Type>();   // nhớ tab khi đổi sang object khác
        // Mỗi object nhớ riêng các tab của nó: "Sphere" mở Mesh Renderer, "Sphere (1)" mở Sphere Collider,
        // chuyển qua lại thì mỗi object hiện đúng tab của mình. _lastTypes chỉ là dự phòng cho object chưa từng xem.
        Dictionary<GameObject, HashSet<Type>> _perObject = new Dictionary<GameObject, HashSet<Type>>();
        [SerializeField] List<string> _lastTypeNames = new List<string>(); // bản serialize của _lastTypes (giữ qua recompile / Play)
        readonly Dictionary<Component, UEditor> _editors = new Dictionary<Component, UEditor>();
        readonly Dictionary<Component, Component[]> _editorTargets = new Dictionary<Component, Component[]>();
        readonly Dictionary<Component, Component[]> _targetCache = new Dictionary<Component, Component[]>(); // cache theo frame GUI

        // ---------- Cache hiệu năng ----------
        Component[] _compsCache;                                   // GetComponents chỉ gọi 1 lần / frame GUI
        readonly Dictionary<Type, Texture> _iconCache = new Dictionary<Type, Texture>();
        readonly Dictionary<Type, string> _nameCache = new Dictionary<Type, string>();
        readonly HashSet<Type> _iconMissing = new HashSet<Type>();                       // type không có icon: khỏi tra lại mỗi frame
        readonly Dictionary<Type, GUIContent> _tabContentCache = new Dictionary<Type, GUIContent>();
        readonly Dictionary<Material, bool> _matRO = new Dictionary<Material, bool>();   // material chỉ-đọc (cache theo lần Bind)
        readonly Dictionary<Material, string> _matShader = new Dictionary<Material, string>(); // tên shader (xóa mỗi Layout)
        readonly List<Component> _tmpA = new List<Component>();
        readonly List<Component> _tmpB = new List<Component>();
        readonly List<Material> _tmpMats = new List<Material>();
        readonly List<Component> _tmpRemove = new List<Component>(), _barTmp = new List<Component>(), _shownTmp = new List<Component>();
        readonly Dictionary<string, float> _textWCache = new Dictionary<string, float>();
        float _textWCacheScale = -1f;

        // ---------- Lọc component / phím tắt / thu gọn panel ----------
        string _filter = "";
        readonly List<Component> _barComps = new List<Component>(); // tab đang hiện trên thanh (để phím tắt chuyển tab)
        readonly HashSet<Component> _collapsed = new HashSet<Component>();

        // ---------- Trạng thái giữ qua recompile / Play ----------
        // _collapsed và _perObject là HashSet/Dictionary nên Unity không serialize. OnBeforeSerialize chép sang các list này,
        // OnEnable đọc ngược lại (tham chiếu UObject nên cả object ở scene chưa lưu cũng giữ được).
        [SerializeField] List<Component> _collapsedSer = new List<Component>();
        [SerializeField] List<GameObject> _perObjKeys = new List<GameObject>();
        [SerializeField] List<string> _perObjTypes = new List<string>();   // mỗi phần tử: AssemblyQualifiedName các type, nối bằng '\n'
        static readonly Dictionary<Type, string> _aqnCache = new Dictionary<Type, string>();
        const int MaxPersistedObjects = 256;
        // Bản dự phòng bằng GlobalObjectId (GUID scene + localId): tham chiếu object ở scene thường mất khi vào/thoát Play,
        // còn GlobalObjectId thì tìm lại được object cùng id trong scene đã lưu. Chỉ tính ở thời điểm hiếm (trước recompile / đổi Play mode)
        // vì GetGlobalObjectIdSlow chậm, không làm trong OnBeforeSerialize.
        [SerializeField] List<string> _collapsedGid = new List<string>();
        [SerializeField] List<string> _perObjGid = new List<string>();
        [SerializeField] List<string> _perObjGidTypes = new List<string>();
        const int MaxGidObjects = 128;

        // Trạng thái chờ khớp: scene có thể CHƯA nạp xong lúc OnEnable chạy (nhất là ngay sau khi thoát Play), khi đó
        // GlobalObjectIdentifierToObjectSlow trả về null và trạng thái mất luôn. Giữ lại theo chuỗi id, đến khi Bind
        // một object thì tính id của chính nó rồi so khớp (1 object / 1 lần Bind, không phụ thuộc thời điểm nạp scene).
        readonly Dictionary<string, string> _pendingTabs = new Dictionary<string, string>();   // gid GameObject -> tên type nối '\n'
        readonly HashSet<string> _pendingCollapsed = new HashSet<string>();                    // gid Component đang thu gọn
        int _pendingTries;
        const int MaxPendingTries = 200;

        void ResolvePendingState(Component[] cs)
        {
            if (_pendingTabs.Count == 0 && _pendingCollapsed.Count == 0) return;
            if (_go == null) return;                       // chưa có object thì chưa tính là 1 lần thử
            if (++_pendingTries > MaxPendingTries) { _pendingTabs.Clear(); _pendingCollapsed.Clear(); return; }

            // Bản chụp lúc ở Edit mode là NGUỒN ĐÚNG => được ưu tiên hơn _perObject. Trước đây có điều kiện
            // "!_perObject.ContainsKey(_go)": chỉ cần một lần Bind sớm chọn nhầm tab mặc định thì SaveStateOfCurrent
            // ghi tab nhầm đó vào _perObject, từ đó bản chụp không bao giờ được áp lại => mất tab sau khi thoát Play.
            if (_gos.Count <= 1 && _pendingTabs.Count > 0)
            {
                var g = GidOf(_go);
                if (g != null && _pendingTabs.TryGetValue(g, out var types))
                {
                    var set = ParseTypes(types);
                    if (set.Count > 0) _perObject[_go] = set;
                    _pendingTabs.Remove(g);
                }
            }

            if (_pendingCollapsed.Count > 0 && cs != null)
            {
                // id component = id GameObject + "#chỉ số": tính id GameObject 1 lần rồi nối chỉ số, thay vì gọi GidOf(c)
                // cho từng component (mỗi lần lại GetComponents + dựng chuỗi đường dẫn => O(n²) và sinh rác chuỗi).
                string baseG = GidOf(_go);
                if (baseG != null)
                {
                    var raw = _go.GetComponents<Component>();
                    foreach (var c in cs)
                    {
                        if (c == null || _collapsed.Contains(c)) continue;
                        int ci = Array.IndexOf(raw, c);
                        if (ci >= 0 && _pendingCollapsed.Remove(baseG + "#" + ci)) _collapsed.Add(c);
                        if (_pendingCollapsed.Count == 0) break;
                    }
                }
            }
        }

        // ---------- Phiên bản dữ liệu (cache theo thay đổi thay vì quét mỗi Layout) ----------
        int _structVer;            // tăng khi cấu trúc đổi (thêm/xóa/sắp xếp component, đổi cha, đổi selection...)
        int _dataVer;              // tăng khi BẤT KỲ dữ liệu object đổi (tên, tag, layer, static...)
        int _layoutVer = -1, _syncVer = -1;
        double _layoutAt, _syncAt;   // mốc thời gian lần xóa cache gần nhất (xem CachesStale)
        int _mixVer = -1, _mixCount = -1;
        double _mixAt;

        // ---------- Cache header asset ----------
        GUIContent _hdrTitleGC;
        UObject _hdrFor;
        int _hdrCount = -1;
        bool _hdrDirty = true;

        // ---------- Lịch sử chọn (Back / Forward) ----------
        [SerializeField] List<UObject> _history = new List<UObject>();
        [SerializeField] int _histIndex = -1;
        const int MaxHistory = 50;

        // ---------- Material editor dưới Renderer ----------
        readonly Dictionary<Material, UEditor> _matEditors = new Dictionary<Material, UEditor>();
        readonly HashSet<Material> _matUsed = new HashSet<Material>();

        // ---------- Trạng thái editor của asset ----------
        bool _expectImporter, _expectObject, _assetDirty;
        bool _assetImmutable, _assetNotEditable;          // tính 1 lần khi dựng editor (không quét mỗi event)
        bool _drawObjectBody = true;                      // false: chỉ vẽ importer (+ preview), không vẽ thân object

    }
}
#endif