// GaMoInspector.TabColors.cs  -  part of CustomsEditor (GaMo Inspector)
// Ghim màu cho nút component (Default / Selected) + context menu tự vẽ (Ghim, Edit, Copy, Remove, Properties...)
// Dữ liệu lưu ở: Assets/Blender-Style Workflow/Inspector-Colors/TabColors.json
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UIE = UnityEngine.UIElements;
using UObject = UnityEngine.Object;

namespace GaMo.BlenderEditorTools
{
    // ======================================================================
    //  Dữ liệu màu ghim: theo TỪNG component (object A ghim không ảnh hưởng object B cùng loại), lưu JSON trong Assets
    // ======================================================================
    internal static class CustomsTabColors
    {
        public const string Folder = "Assets/Blender-Style Workflow/Inspector-Colors";
        public const string FilePath = Folder + "/TabColors.json";

        public struct Preset
        {
            public Color top, bottom;
            public Preset(Color t, Color b) { top = t; bottom = b; }
            public bool Solid { get { return top == bottom; } }
        }

        static Color Hex(int rgb) { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f); }

        public const int CustomIndex = 5;                       // ô cuối: người dùng tự chọn
        public static readonly Color DefaultCustom = Hex(0x3A7BD5);

        // Index 0 = không ghim. Index 5 = màu tùy chỉnh (màu thật lấy từ Entry.defCol / selCol).
        // Muốn đổi bộ màu mẫu thì chỉ cần sửa các số hex ở đây.
        public static readonly Preset[] Presets =
        {
            new Preset(Color.clear, Color.clear),
            new Preset(Hex(0x2C5D87), Hex(0x2C5D87)),   // xanh selection của Unity
            new Preset(Hex(0x3F7F3F), Hex(0x3F7F3F)),   // xanh lá
            new Preset(Hex(0xA23B3B), Hex(0xA23B3B)),   // đỏ
            new Preset(Hex(0x585858), Hex(0x585858)),   // xám nút của Unity
            new Preset(Color.clear, Color.clear),       // 5 = Custom
        };

        [Serializable] class Entry
        {
            public string id; public int def; public int sel;   // id = khóa của TỪNG component (xem CustomsEditor.PinKeyOf)
            public Color defCol = DefaultCustom;        // màu của ô Custom khi dùng làm Default
            public Color selCol = DefaultCustom;        // màu của ô Custom khi dùng làm Selected
        }
        [Serializable] class Db { public List<Entry> items = new List<Entry>(); }

        static readonly Dictionary<string, Entry> _map = new Dictionary<string, Entry>();
        static readonly Dictionary<Component, string> _keys = new Dictionary<Component, string>(); // GetGlobalObjectIdSlow chậm: tính 1 lần / component
        static bool _loaded, _refreshQueued;

        static string Key(Component c)
        {
            if (!_keys.TryGetValue(c, out var k))
            {
                if (_keys.Count > 4096) _keys.Clear(); // component đã hủy vẫn nằm trong cache: dọn định kỳ
                _keys[c] = k = CustomsEditor.PinKeyOf(c);
            }
            return k;
        }

        static int Clamp(int i) { return Mathf.Clamp(i, 0, Presets.Length - 1); }

        // Gọi khi project đổi (file JSON có thể bị sửa ngoài / git; lưu scene => object mới có GlobalObjectId) => lần Get kế tiếp đọc lại
        public static void Invalidate() { _loaded = false; _keys.Clear(); }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _map.Clear();
            try
            {
                if (!File.Exists(FilePath)) return;
                var db = JsonUtility.FromJson<Db>(File.ReadAllText(FilePath));
                if (db == null || db.items == null) return;
                foreach (var e in db.items)
                    if (e != null && !string.IsNullOrEmpty(e.id)) _map[e.id] = e; // mục cũ ghim theo loại (không có id) bị bỏ
            }
            catch (Exception) { }
        }

        public static void Get(Component c, out int def, out int sel, out Color defCol, out Color selCol)
        {
            EnsureLoaded();
            def = sel = 0; defCol = selCol = DefaultCustom;
            if (c == null || _map.Count == 0) return;
            string k = Key(c);
            if (k != null && _map.TryGetValue(k, out var e))
            { def = Clamp(e.def); sel = Clamp(e.sel); defCol = e.defCol; selCol = e.selCol; }
        }

        public static void Set(Component c, int def, int sel, Color defCol, Color selCol)
        {
            if (c == null) return;
            EnsureLoaded();
            string k = Key(c);
            if (k == null) return;
            defCol.a = 1f; selCol.a = 1f;
            if (def <= 0 && sel <= 0) _map.Remove(k);
            else _map[k] = new Entry { id = k, def = Clamp(def), sel = Clamp(sel), defCol = defCol, selCol = selCol };
            Save();
        }

        // Màu ghim đang áp dụng cho tab (open = tab đang mở => dùng màu Selected)
        public static void GetPin(Component c, bool open, out int idx, out Color top, out Color bot)
        {
            Get(c, out int d, out int s, out Color dc, out Color sc);
            idx = open ? s : d;
            top = bot = Color.clear;
            if (idx == CustomIndex) top = bot = open ? sc : dc;
            else if (idx > 0) { top = Presets[idx].top; bot = Presets[idx].bottom; }
        }

        static void Save()
        {
            bool newFolder = !Directory.Exists(Folder);
            try
            {
                var db = new Db();
                db.items.AddRange(_map.Values);
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FilePath, JsonUtility.ToJson(db, true), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GaMo] Không lưu được màu ghim: " + ex.Message);
                return;
            }
            if (_refreshQueued) return;
            _refreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                _refreshQueued = false;
                if (newFolder || !File.Exists(FilePath + ".meta")) AssetDatabase.Refresh(); // thư mục / file mới: cần tạo .meta
                else AssetDatabase.ImportAsset(FilePath);
            };
        }
    }

    // ======================================================================
    //  Texture bo góc tự bake: nền trắng bo góc, hoặc nền "mờ dần từ trên xuống" (để ghép gradient)
    //  Gradient = vẽ màu đáy đặc, rồi phủ màu đỉnh có alpha giảm dần => nội suy tuyến tính, không cần texture riêng cho từng màu.
    // ======================================================================
    internal static class CustomsRoundTextures
    {
        static Texture2D _fade, _big, _sq;
        static GUIStyle _fadeStyle, _bigStyle;
        static bool _hooked;

        static Texture2D Bake(int w, int h, int rad, bool fade)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            const int ss = 3;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float fx = x + (sx + 0.5f) / ss, fy = y + (sy + 0.5f) / ss;
                            float cx = Mathf.Clamp(fx, rad, w - rad), cy = Mathf.Clamp(fy, rad, h - rad);
                            float dx = fx - cx, dy = fy - cy;
                            if (dx * dx + dy * dy <= rad * rad) hit++;
                        }
                    float a = hit / (float)(ss * ss);
                    if (fade) a *= (y + 0.5f) / h;      // y lớn = phía trên (texture gốc dưới trái) => trên đặc, dưới trong suốt
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply(false, false);
            if (!_hooked)
            {
                _hooked = true;
                AssemblyReloadEvents.beforeAssemblyReload += DestroyAll; // HideAndDontSave không tự bị hủy khi reload domain
            }
            return tex;
        }

        static void DestroyAll()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= DestroyAll;
            _hooked = false;
            if (_fade != null) UObject.DestroyImmediate(_fade);
            if (_big != null) UObject.DestroyImmediate(_big);
            if (_sq != null) UObject.DestroyImmediate(_sq);
            _fade = _big = _sq = null;
            _fadeStyle = _bigStyle = null;
        }

        static GUIStyle MakeStyle(Texture2D t, int border)
        {
            var s = new GUIStyle();
            s.normal.background = s.hover.background = s.active.background = s.focused.background = t;
            s.border = new RectOffset(border, border, border, border);
            return s;
        }

        // Nút component (bo nhỏ): màu đỉnh mờ dần xuống đáy
        public static void DrawFade(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint || r.width < 1f || r.height < 1f) return;
            if (_fade == null) _fade = Bake(16, 64, 4, true);
            if (_fadeStyle == null || _fadeStyle.normal.background == null) _fadeStyle = MakeStyle(_fade, 6);
            var old = GUI.color;
            GUI.color = c;
            _fadeStyle.Draw(r, false, false, false, false);
            GUI.color = old;
        }

        // Nút popup (bo lớn)
        public static void DrawBig(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint || r.width < 1f || r.height < 1f) return;
            if (_big == null) _big = Bake(24, 24, 8, false);
            if (_bigStyle == null || _bigStyle.normal.background == null) _bigStyle = MakeStyle(_big, 9);
            var old = GUI.color;
            GUI.color = c;
            _bigStyle.Draw(r, false, false, false, false);
            GUI.color = old;
        }

        // Ô màu vuông (không bo): màu đỉnh mờ dần xuống đáy
        public static void DrawSquareFade(Rect r, Color top)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (_sq == null) _sq = Bake(2, 64, 0, true);
            var old = GUI.color;
            GUI.color = top;
            GUI.DrawTexture(r, _sq, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }
    }

    public partial class CustomsEditor
    {
        // ---------- Màu nút component theo ghim ----------
        // pinIdx: 0 = không ghim. Đang mở dùng màu Selected, không mở dùng màu Default.
        // Không ghim: mở = cam (Accent), đóng = nền nút mặc định (không tô).
        static void ResolveTabColors(bool isOpen, int pinIdx, Color pinTop, Color pinBot, bool en, out Color top, out Color bot)
        {
            if (pinIdx > 0) { top = pinTop; bot = pinBot; }
            else top = bot = isOpen ? Accent : Color.white;

            if (!en)
            {
                if (!isOpen && pinIdx <= 0) top = bot = DisabledColor;
                else
                {
                    top = Color.Lerp(top, DisabledColor, 0.6f);
                    bot = Color.Lerp(bot, DisabledColor, 0.6f);
                }
            }
        }

        // Chữ theo màu chữ chuẩn của Unity (không dùng trắng/đen tuyệt đối)
        static Color ContrastText(Color a, Color b)
        {
            var m = Color.Lerp(a, b, 0.5f);
            float l = 0.299f * m.r + 0.587f * m.g + 0.114f * m.b;
            return l > 0.58f ? new Color(0.035f, 0.035f, 0.035f) : new Color(0.82f, 0.82f, 0.82f);
        }

        static void DrawTabFill(Rect r, Color top, Color bot, bool hover, float radius)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (top == bot) { DrawSolid(r, top, hover, radius); return; }

            float a = GUI.enabled ? 1f : 0.4f;
            var border = new Color(bot.r * 0.6f, bot.g * 0.6f, bot.b * 0.6f, a);
            DrawRounded(r, border, radius);
            var inner = new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2);
            var fillB = hover && GUI.enabled ? Color.Lerp(bot, Color.white, 0.15f) : bot;
            var fillT = hover && GUI.enabled ? Color.Lerp(top, Color.white, 0.15f) : top;
            fillB.a = a; fillT.a = a;
            DrawRounded(inner, fillB, Mathf.Max(0f, radius - 1f));
            CustomsRoundTextures.DrawFade(inner, fillT);
        }

        // =====================================================
        //  Context menu của nút component (chuột phải)
        // =====================================================
        class TabMenuPopup : PopupWindowContent
        {
            const float W = 232f, Pad = 8f, Gap = 5f, BtnH = 26f, HdrH = 28f, PanelH = 112f, CustomH = 24f;
            const string OpenKey = "GaMo.CustomsEditor.PinFoldout";
            static readonly Color BtnBg = Color.gray4;
            static readonly Color BtnHover = new Color(0.64f, 0.64f, 0.64f);
            static readonly Color PanelBg = Color.gray2;
            static GUIStyle _txt;
            static readonly Vector3[] _pts = new Vector3[3];

            struct Item { public string label; public bool enabled; public Action act; }

            readonly CustomsEditor _owner;
            readonly Type _type;
            readonly Component _comp;                 // màu ghim gắn với ĐÚNG component này
            readonly List<Item> _items = new List<Item>();
            bool _open;
            int _def, _sel; Color _dc, _sc; string _hexD, _hexS;

            public TabMenuPopup(CustomsEditor owner, Component c)
            {
                _owner = owner;
                _type = c.GetType();
                _comp = c;
                _open = EditorPrefs.GetBool(OpenKey, true);
                CustomsTabColors.Get(_comp, out _def, out _sel, out _dc, out _sc);
                _hexD = ColorUtility.ToHtmlStringRGB(_dc);
                _hexS = ColorUtility.ToHtmlStringRGB(_sc);

                var go = c.gameObject;
                var all = go.GetComponents<Component>();
                var mb = c as MonoBehaviour;
                bool canEdit = mb != null && MonoScript.FromMonoBehaviour(mb) != null;
                bool canRemove = !(c is Transform) && !IsRequiredByOthers(c, all);

                _items.Add(new Item
                {
                    label = "Reset", enabled = true,
                    act = () =>
                    {
                        if (c == null) return;
                        Undo.RecordObject(c, "Reset " + c.GetType().Name);
                        Unsupported.SmartReset(c); // giống mục Reset của Inspector gốc (gọi cả hàm Reset() của script)
                        owner.Repaint();
                    }
                });
                _items.Add(new Item
                {
                    label = "Edit", enabled = canEdit,
                    act = () => { var s = MonoScript.FromMonoBehaviour(mb); if (s != null) AssetDatabase.OpenAsset(s); }
                });
                _items.Add(new Item { label = "Copy", enabled = true, act = () => CustomsComponentClipboard.Copy(c) });

                var copied = CustomsComponentClipboard.CopiedType;
                if (copied != null)
                {
                    _items.Add(new Item
                    {
                        label = "Paste New Component", enabled = true,
                        act = () =>
                        {
                            if (go == null) return;
                            ComponentUtility.PasteComponentAsNew(go);
                            owner.InvalidateStruct(); owner.Repaint(); owner.RequestSync();
                        }
                    });
                    if (copied == _type)
                        _items.Add(new Item
                        {
                            label = "Paste Values", enabled = true,
                            act = () =>
                            {
                                if (c == null) return;
                                Undo.RecordObject(c, "Paste Component Values");
                                if (!ComponentUtility.PasteComponentValues(c))
                                    owner.ShowNotification(new GUIContent("Không dán được giá trị: clipboard hiện không phải component cùng loại"));
                                owner.Repaint();
                            }
                        });
                }

                _items.Add(new Item
                {
                    label = "Remove", enabled = canRemove,
                    act = () =>
                    {
                        if (c == null) return;
                        Undo.DestroyObjectImmediate(c);
                        owner.InvalidateStruct(); owner.Repaint(); owner.RequestSync();
                    }
                });
                _items.Add(new Item { label = "Properties...", enabled = true, act = () => EditorUtility.OpenPropertyEditor(c) });
            }

            float PanelHeight()
            {
                return PanelH + (_def == CustomsTabColors.CustomIndex ? CustomH : 0f)
                              + (_sel == CustomsTabColors.CustomIndex ? CustomH : 0f);
            }
            float Height() { return Pad * 2f + HdrH + (_open ? PanelHeight() : 0f) + _items.Count * (BtnH + Gap); }
            void Resize() { var p = editorWindow.position; editorWindow.position = new Rect(p.x, p.y, p.width, Height()); }

            public override Vector2 GetWindowSize() { return new Vector2(W, Height()); }

            public override void OnOpen() { editorWindow.wantsMouseMove = true; }

            void EnsureStyle()
            {
                if (_txt != null) return;
                _txt = new GUIStyle(EditorStyles.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
                SetTextColor(_txt, Color.white);
            }

            public override void OnGUI(Rect rect)
            {
                var ev = Event.current;
                EnsureStyle();
                if (ev.type == EventType.Repaint) EditorGUI.DrawRect(rect, Color.gray2);

                float x = Pad, w = rect.width - Pad * 2f, y = Pad;
                float ph = PanelHeight();
                Vector2 m = ev.mousePosition;

                // ----- Ghim (foldout) -----
                var hr = new Rect(x, y, w, HdrH);
                if (_open)
                {
                    // nền sáng đặt phía sau header (góc trên bị header che) => liền khối như thiết kế
                    CustomsRoundTextures.DrawBig(new Rect(x, y + HdrH - 14f, w, ph + 14f), PanelBg);
                    DrawPinPanel(x, y + HdrH, w);
                }
                CustomsRoundTextures.DrawBig(hr, hr.Contains(m) ? BtnHover : BtnBg);
                DrawChevron(hr, _open);
                GUI.Label(new Rect(hr.x + 38f, hr.y, hr.width - 44f, hr.height), "Ghim", _txt);
                if (GUI.Button(hr, GUIContent.none, GUIStyle.none))
                {
                    _open = !_open;
                    EditorPrefs.SetBool(OpenKey, _open);
                    Resize();
                    editorWindow.Repaint();
                    return;
                }
                y += HdrH + (_open ? ph : 0f);

                // ----- Các nút hành động -----
                var oldCol = GUI.contentColor;
                foreach (var it in _items)
                {
                    y += Gap;
                    var r = new Rect(x, y, w, BtnH);
                    y += BtnH;

                    bool hover = it.enabled && r.Contains(m);
                    CustomsRoundTextures.DrawBig(r, hover ? BtnHover : BtnBg);
                    GUI.contentColor = it.enabled ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                    GUI.Label(new Rect(r.x + 14f, r.y, r.width - 20f, r.height), it.label, _txt);
                    GUI.contentColor = oldCol;

                    if (it.enabled && GUI.Button(r, GUIContent.none, GUIStyle.none))
                    {
                        var act = it.act;
                        editorWindow.Close();
                        EditorApplication.delayCall += () => { if (act != null) act(); }; // chạy ngoài vòng OnGUI
                        GUIUtility.ExitGUI();
                    }
                }
                GUI.contentColor = oldCol;
            }

            void DrawChevron(Rect hr, bool open)
            {
                if (Event.current.type != EventType.Repaint) return;
                float ax = hr.x + 14f, ay = hr.y + hr.height * 0.5f;
                if (open) { _pts[0] = new Vector3(ax, ay - 3f); _pts[1] = new Vector3(ax + 7f, ay + 4f); _pts[2] = new Vector3(ax + 14f, ay - 3f); }
                else { _pts[0] = new Vector3(ax + 3f, ay - 7f); _pts[1] = new Vector3(ax + 10f, ay); _pts[2] = new Vector3(ax + 3f, ay + 7f); }
                var old = Handles.color;
                Handles.color = new Color(0.08f, 0.08f, 0.08f);
                Handles.DrawAAPolyLine(2.5f, _pts);
                Handles.color = old;
            }

            void DrawPinPanel(float x, float y0, float w)
            {
                int d = _def, s = _sel; Color dc = _dc, sc = _sc;
                float ix = x + 12f, iw = w - 24f, sw = 26f, sg = (iw - 6f * sw) / 5f;
                float yy = y0 + 6f;

                GUI.Label(new Rect(ix, yy, iw, 18f), "Default Color", _txt); yy += 20f;
                int pick = SwatchRow(ix, yy, sw, sg, d, dc);
                if (pick >= 0) d = pick;
                yy += sw + 6f;
                if (d == CustomsTabColors.CustomIndex) { dc = CustomStrip(ix, yy, iw, dc, ref _hexD); yy += CustomH; }

                GUI.Label(new Rect(ix, yy, iw, 18f), "Selected Color", _txt); yy += 20f;
                pick = SwatchRow(ix, yy, sw, sg, s, sc);
                if (pick >= 0) s = pick;
                yy += sw + 6f;
                if (s == CustomsTabColors.CustomIndex) sc = CustomStrip(ix, yy, iw, sc, ref _hexS);

                if (d != _def || s != _sel || dc != _dc || sc != _sc)
                {
                    bool resize = (d == CustomsTabColors.CustomIndex) != (_def == CustomsTabColors.CustomIndex)
                               || (s == CustomsTabColors.CustomIndex) != (_sel == CustomsTabColors.CustomIndex);
                    _def = d; _sel = s; _dc = dc; _sc = sc;
                    CustomsTabColors.Set(_comp, d, s, dc, sc);
                    _owner.RepaintBar();         // chỉ vẽ lại hàng nút, không đụng các editor nặng
                    if (resize) Resize();
                    editorWindow.Repaint();
                }
            }

            // Ô hex + 3 thanh H/S/V (không mở ColorPicker rời vì popup sẽ tự đóng khi mất focus)
            Color CustomStrip(float ix, float yy, float iw, Color c, ref string hex)
            {
                EditorGUI.BeginChangeCheck();
                Color newColor = EditorGUI.ColorField(new Rect(ix, yy, iw, 18f), GUIContent.none, c, false, false, false);
                if (EditorGUI.EndChangeCheck())
                {
                    c = newColor;
                    hex = ColorUtility.ToHtmlStringRGB(c);
                }
                return c;
            }

            // Trả về index ô vừa bấm, -1 nếu không bấm
            int SwatchRow(float ix, float yy, float sw, float sg, int cur, Color custom)
            {
                int clicked = -1;
                for (int i = 0; i < CustomsTabColors.Presets.Length; i++)
                {
                    var r = new Rect(ix + i * (sw + sg), yy, sw, sw);
                    if (Event.current.type == EventType.Repaint)
                    {
                        if (cur == i) EditorGUI.DrawRect(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), Accent);
                        DrawSwatch(r, i, custom);
                    }
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) clicked = i;
                }
                return clicked;
            }

            static void DrawSwatch(Rect r, int i, Color custom)
            {
                if (i == 0)
                {
                    EditorGUI.DrawRect(r, Color.white);
                    var old = Handles.color;
                    Handles.color = Color.red;
                    Handles.DrawAAPolyLine(3f, new Vector3(r.x + 1f, r.yMax - 1f), new Vector3(r.xMax - 1f, r.y + 1f));
                    Handles.color = old;
                    return;
                }
                if (i == CustomsTabColors.CustomIndex)
                {
                    EditorGUI.DrawRect(r, custom);
                    var cc = ContrastText(custom, custom);              // dấu "+" báo đây là ô tùy chỉnh
                    EditorGUI.DrawRect(new Rect(r.center.x - 5f, r.center.y - 1f, 10f, 2f), cc);
                    EditorGUI.DrawRect(new Rect(r.center.x - 1f, r.center.y - 5f, 2f, 10f), cc);
                    return;
                }
                var p = CustomsTabColors.Presets[i];
                EditorGUI.DrawRect(r, p.bottom);
                if (!p.Solid) CustomsRoundTextures.DrawSquareFade(r, p.top);
            }
        }
    }
}
#endif