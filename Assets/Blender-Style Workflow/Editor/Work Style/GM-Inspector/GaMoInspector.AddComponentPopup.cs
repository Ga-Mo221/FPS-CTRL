// GaMoInspector.AddComponentPopup.cs  -  part of CustomsEditor (GaMo Inspector)
// Popup 'Add Component' tu ve (tim kiem + menu + Recent + New Script)
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
    public partial class CustomsEditor
    {
        class AddComponentPopup : PopupWindowContent
        {
            class Entry
            {
                public Type type;
                public string label;       // tên hiển thị (lấy từ [AddComponentMenu] nếu có, ví dụ "Copy Transform")
                public string category;    // nhóm menu ("Physics", "Rendering", "Cinemachine/Extensions"...), rỗng = gốc
                public string tip;         // tooltip: đường dẫn menu + tên type
                public string key;         // chuỗi để tìm kiếm (chữ thường)
                public string keyCompact;  // key bỏ khoảng trắng và "/"
                public bool hasMenu;       // có [AddComponentMenu] với đường dẫn không rỗng
                public string menuLower;   // đường dẫn [AddComponentMenu] (chữ thường), rỗng nếu không có
                public string labelLower;  // tên hiển thị (chữ thường)
                public string nameLower;   // tên class đã tách từ (chữ thường), vd "tis controller"
                public GUIContent content; // tạo lười lần đầu hiển thị (icon + tooltip)
            }

            enum RowKind { Header, Folder, Item, NewScript }

            class Row
            {
                public RowKind kind;
                public Entry entry;        // Item
                public string text;        // Header: tiêu đề | Folder: đường dẫn nhóm đầy đủ | NewScript: tên gợi ý (có thể rỗng)
                public GUIContent content; // tạo lười ở OnGUI
            }

            enum Mode { List, NewScript }

            const string FolderPrefKey = "GaMo.CustomsEditor.NewScriptFolder";
            const string ScriptTemplate =
                "using UnityEngine;\n\npublic class {0} : MonoBehaviour\n{{\n    void Start()\n    {{\n    }}\n\n    void Update()\n    {{\n    }}\n}}\n";

            static List<Entry> _cache;
            static Dictionary<Type, Entry> _byType;
            static Dictionary<string, List<Entry>> _byCat;           // nhóm -> component nằm trực tiếp trong nhóm
            static Dictionary<string, SortedSet<string>> _subCats;   // nhóm -> tên các nhóm con
            static readonly Dictionary<string, bool> _userAsm = new Dictionary<string, bool>();
            static readonly HashSet<string> _keywords = new HashSet<string>
            {
                "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const","continue","decimal",
                "default","delegate","do","double","else","enum","event","explicit","extern","false","finally","fixed","float","for",
                "foreach","goto","if","implicit","in","int","interface","internal","is","lock","long","namespace","new","null",
                "object","operator","out","override","params","private","protected","public","readonly","ref","return","sbyte",
                "sealed","short","sizeof","stackalloc","static","string","struct","switch","this","throw","true","try","typeof",
                "uint","ulong","unchecked","unsafe","ushort","using","virtual","void","volatile","while"
            };
            static GUIStyle _rightMini;

            readonly GameObject[] _targets;
            readonly float _width;
            readonly List<Row> _rows = new List<Row>();
            readonly List<Entry> _recent = new List<Entry>();
            string _search = "";
            string _lastSearch = "";
            string _catPath = "";            // đang đứng trong nhóm nào ("" = gốc)
            Vector2 _scroll;
            bool _focused, _scrollToSel;
            int _sel;

            // New script
            Mode _mode = Mode.List;
            string _newName = "NewBehaviourScript";
            string _newFolder = "Assets";
            bool _newFocused;
            string _valName, _valFolder, _valErr;   // cache kết quả kiểm tra (không quét type mỗi event)
            string _runtimeErr;

            public AddComponentPopup(GameObject[] targets, float width)
            {
                _targets = targets;
                _width = Mathf.Max(width, 300f);
                if (_cache == null) BuildCache();

                foreach (var name in CustomsRecentComponents.Load())
                {
                    Type t = null;
                    try { t = Type.GetType(name); } catch (Exception) { }
                    if (t != null && _byType.TryGetValue(t, out var e)) _recent.Add(e);
                }
                _newFolder = EditorPrefs.GetString(FolderPrefKey, "Assets");
                Rebuild();
            }

            public override void OnOpen()
            {
                editorWindow.wantsMouseMove = true; // hover chỉ repaint khi chuột di chuyển, không repaint liên tục
            }

            // Assembly/namespace chỉ dành cho Editor. So khớp THEO TỪNG TỪ, không dùng Contains("Editor"),
            // vì namespace như "GaMo.BlenderEditorTools" chứa chữ Editor nhưng vẫn là component runtime.
            static bool IsEditorOnly(Type t)
            {
                string an = t.Assembly.GetName().Name;
                if (an.StartsWith("UnityEditor", StringComparison.Ordinal)) return true;
                foreach (var tok in an.Split('.', '-', '_'))
                    if (tok == "Editor") return true;

                string ns = t.Namespace;
                return ns != null && (ns == "UnityEditor" || ns.StartsWith("UnityEditor.", StringComparison.Ordinal));
            }

            // Assembly code của người dùng (Assembly-CSharp hoặc asmdef nằm trong Assets/): cho phép MonoBehaviour non-public.
            static bool IsUserAssembly(string asmName)
            {
                if (_userAsm.TryGetValue(asmName, out var r)) return r;
                r = asmName == "Assembly-CSharp" || asmName == "Assembly-CSharp-firstpass";
                if (!r)
                {
                    try
                    {
                        string p = UnityEditor.Compilation.CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(asmName);
                        r = !string.IsNullOrEmpty(p) && p.StartsWith("Assets/", StringComparison.Ordinal);
                    }
                    catch (Exception) { }
                }
                _userAsm[asmName] = r;
                return r;
            }

            // Nhóm dự đoán cho component KHÔNG có [AddComponentMenu] (phần lớn component built-in: menu của Unity nằm ở native
            // nên không đọc được). Chỉ là suy đoán theo loại; component có [AddComponentMenu] luôn dùng đúng đường dẫn của nó.
            static readonly HashSet<string> _renderingNames = new HashSet<string>
            {
                "Skybox","LODGroup","LightProbeGroup","LightProbeProxyVolume","ReflectionProbe","OcclusionArea","OcclusionPortal",
                "SortingGroup","CanvasRenderer","LensFlare","Projector","Halo"
            };

            static string GuessCategory(Type t)
            {
                string asm = t.Assembly.GetName().Name;
                if (!asm.StartsWith("UnityEngine", StringComparison.Ordinal)) return "Scripts"; // script người dùng / package: Unity cũng gom vào "Scripts"

                if (typeof(Collider2D).IsAssignableFrom(t) || typeof(Rigidbody2D).IsAssignableFrom(t)
                    || typeof(Joint2D).IsAssignableFrom(t) || typeof(Effector2D).IsAssignableFrom(t)
                    || typeof(ConstantForce2D).IsAssignableFrom(t)) return "Physics 2D";

                if (typeof(ParticleSystem).IsAssignableFrom(t) || typeof(TrailRenderer).IsAssignableFrom(t)
                    || typeof(LineRenderer).IsAssignableFrom(t)) return "Effects";

                if (typeof(MeshFilter).IsAssignableFrom(t) || typeof(MeshRenderer).IsAssignableFrom(t)
                    || typeof(SkinnedMeshRenderer).IsAssignableFrom(t) || typeof(TextMesh).IsAssignableFrom(t)) return "Mesh";

                if (typeof(Collider).IsAssignableFrom(t) || typeof(Rigidbody).IsAssignableFrom(t) || typeof(Joint).IsAssignableFrom(t)
                    || typeof(Cloth).IsAssignableFrom(t) || typeof(ConstantForce).IsAssignableFrom(t)
                    || typeof(ArticulationBody).IsAssignableFrom(t)) return "Physics";

                if (typeof(AudioBehaviour).IsAssignableFrom(t) || typeof(AudioReverbZone).IsAssignableFrom(t)
                    || t.Name.StartsWith("Audio", StringComparison.Ordinal)) return "Audio";

                if (typeof(Renderer).IsAssignableFrom(t) || typeof(Camera).IsAssignableFrom(t) || typeof(Light).IsAssignableFrom(t)
                    || typeof(Canvas).IsAssignableFrom(t) || _renderingNames.Contains(t.Name)) return "Rendering";

                if (t.Name == "PlayableDirector") return "Playables";
                if (t.Name == "VideoPlayer") return "Video";
                return "Miscellaneous";
            }

            static void BuildCache()
            {
                _cache = new List<Entry>();
                foreach (var t in TypeCache.GetTypesDerivedFrom<Component>())
                {
                    if (t.IsAbstract || t.IsGenericType || t.IsNested) continue;
                    if (!t.IsPublic && !(typeof(MonoBehaviour).IsAssignableFrom(t) && IsUserAssembly(t.Assembly.GetName().Name))) continue;
                    if (t == typeof(Transform) || t == typeof(RectTransform)) continue;
                    if (IsEditorOnly(t)) continue;
                    if (t.IsDefined(typeof(ObsoleteAttribute), false)) continue;

                    string label = ObjectNames.NicifyVariableName(t.Name);
                    string path = "";
                    string cat;
                    var attr = (AddComponentMenu)Attribute.GetCustomAttribute(t, typeof(AddComponentMenu), false);
                    if (attr != null)
                    {
                        path = attr.componentMenu ?? "";
                        if (path.Length == 0) continue; // [AddComponentMenu("")] = Unity ẩn khỏi menu
                        int slash = path.LastIndexOf('/');
                        label = slash >= 0 ? path.Substring(slash + 1) : path;
                        cat = slash >= 0 ? path.Substring(0, slash) : "";
                    }
                    else cat = GuessCategory(t);
                    cat = cat.Trim('/', ' ');

                    string key = (label + " " + t.Name + " " + path).ToLowerInvariant();
                    _cache.Add(new Entry
                    {
                        type = t,
                        label = label,
                        category = cat,
                        tip = (path.Length > 0 ? path + "\n" : "") + t.FullName,
                        key = key,
                        keyCompact = key.Replace(" ", "").Replace("/", ""),
                        hasMenu = path.Length > 0,
                        menuLower = path.ToLowerInvariant(),
                        labelLower = label.ToLowerInvariant(),
                        nameLower = ObjectNames.NicifyVariableName(t.Name).ToLowerInvariant(),
                    });
                }
                _cache.Sort((x, y) => string.Compare(x.label, y.label, StringComparison.OrdinalIgnoreCase));

                _byType = new Dictionary<Type, Entry>();
                _byCat = new Dictionary<string, List<Entry>>();
                _subCats = new Dictionary<string, SortedSet<string>>();
                foreach (var e in _cache)
                {
                    _byType[e.type] = e;
                    if (!_byCat.TryGetValue(e.category, out var list)) _byCat[e.category] = list = new List<Entry>();
                    list.Add(e);

                    // đăng ký chuỗi nhóm cha: "A/B" => gốc có "A", "A" có "B"
                    string cur = e.category;
                    while (cur.Length > 0)
                    {
                        int sl = cur.LastIndexOf('/');
                        string parent = sl >= 0 ? cur.Substring(0, sl) : "";
                        string name = sl >= 0 ? cur.Substring(sl + 1) : cur;
                        if (!_subCats.TryGetValue(parent, out var set))
                            _subCats[parent] = set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                        set.Add(name);
                        cur = parent;
                    }
                }
            }

            // Mọi từ trong ô tìm kiếm đều phải có trong key; hoặc chuỗi gõ liền ("copytransform") khớp key liền.
            static bool Match(Entry e, string[] words, string compact)
            {
                bool all = true;
                foreach (var w in words)
                    if (e.key.IndexOf(w, StringComparison.Ordinal) < 0) { all = false; break; }
                return all || e.keyCompact.IndexOf(compact, StringComparison.Ordinal) >= 0;
            }

            static bool ContainsAll(string text, string[] words)
            {
                foreach (var w in words)
                    if (text.IndexOf(w, StringComparison.Ordinal) < 0) return false;
                return true;
            }

            // Từ khóa nằm ở ĐẦU một từ (sau '/', khoảng trắng, '_'...), không phải giữa từ ("tis" khớp "TIS Controller" nhưng không khớp "Artist").
            static bool HasWordStart(string text, string q)
            {
                int idx = text.IndexOf(q, StringComparison.Ordinal);
                while (idx >= 0)
                {
                    if (idx == 0 || !char.IsLetterOrDigit(text[idx - 1])) return true;
                    idx = text.IndexOf(q, idx + 1, StringComparison.Ordinal);
                }
                return false;
            }

            // Xếp hạng kết quả tìm kiếm: số nhỏ = đứng trước, -1 = không khớp.
            // Ưu tiên component CÓ [AddComponentMenu] mà đường dẫn menu chứa từ khóa (gõ "tis" => "TIS/..." lên đầu),
            // sau đó mới tới component khớp theo tên hiển thị / tên class, từ khóa nằm giữa từ xếp cuối.
            static int Rank(Entry e, string[] words, string compact)
            {
                if (!Match(e, words, compact)) return -1;
                string first = words.Length > 0 ? words[0] : compact;

                // NHÓM 1 (rank 0-5): class có [AddComponentMenu]. Luôn đứng trước nhóm 2, bất kể từ khóa nằm ở đường dẫn menu
                // hay ở tên class. Trong nhóm: khớp đường dẫn menu trước, rồi mới tới khớp tên class / tên hiển thị.
                if (e.hasMenu)
                {
                    if (ContainsAll(e.menuLower, words))
                    {
                        if (e.menuLower.StartsWith(first, StringComparison.Ordinal)) return 0; // "TIS/Foo"
                        if (HasWordStart(e.menuLower, first)) return 1;                        // "Game/TIS Foo"
                        return 2;
                    }
                    if (e.labelLower.StartsWith(first, StringComparison.Ordinal) || e.nameLower.StartsWith(first, StringComparison.Ordinal)) return 3;
                    if (HasWordStart(e.labelLower, first) || HasWordStart(e.nameLower, first)) return 4;
                    return 5;
                }
                // NHÓM 2 (rank 10+): class không có [AddComponentMenu] (phần lớn component built-in + script thường)
                if (e.labelLower.StartsWith(first, StringComparison.Ordinal)) return 10;
                if (HasWordStart(e.labelLower, first)) return 11;
                if (e.nameLower.StartsWith(first, StringComparison.Ordinal)) return 12;
                if (HasWordStart(e.nameLower, first)) return 13;
                return 14;
            }

            static bool IsValidClassName(string n)
            {
                return n.Length > 0 && Regex.IsMatch(n, "^[A-Za-z_][A-Za-z0-9_]*$") && !_keywords.Contains(n);
            }

            // Dựng lại danh sách dòng khi đổi chuỗi tìm kiếm / đổi nhóm (không dựng lại mỗi event)
            void Rebuild()
            {
                _rows.Clear();
                string raw = _search.Trim();
                string s = raw.ToLowerInvariant();

                if (s.Length > 0)
                {
                    string[] words = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string compact = s.Replace(" ", "");
                    // Xếp hạng TRƯỚC rồi mới cắt 150 dòng, nếu không kết quả tốt có thể bị cắt mất vì nằm sau theo alphabet.
                    var hits = new List<KeyValuePair<int, Entry>>();
                    foreach (var e in _cache)
                    {
                        int rank = Rank(e, words, compact);
                        if (rank >= 0) hits.Add(new KeyValuePair<int, Entry>(rank, e));
                    }
                    hits.Sort((a, b) => a.Key != b.Key
                        ? a.Key.CompareTo(b.Key)
                        : string.Compare(a.Value.label, b.Value.label, StringComparison.OrdinalIgnoreCase));
                    for (int i = 0; i < hits.Count && i < 150; i++)
                        _rows.Add(new Row { kind = RowKind.Item, entry = hits[i].Value });
                    if (IsValidClassName(raw)) _rows.Add(new Row { kind = RowKind.NewScript, text = raw });
                }
                else
                {
                    if (_catPath.Length == 0 && _recent.Count > 0)
                    {
                        _rows.Add(new Row { kind = RowKind.Header, text = "Recent" });
                        foreach (var e in _recent) _rows.Add(new Row { kind = RowKind.Item, entry = e });
                        _rows.Add(new Row { kind = RowKind.Header, text = "All" });
                    }
                    if (_subCats.TryGetValue(_catPath, out var subs))
                        foreach (var sc in subs)
                            _rows.Add(new Row { kind = RowKind.Folder, text = _catPath.Length == 0 ? sc : _catPath + "/" + sc });
                    if (_byCat.TryGetValue(_catPath, out var list))
                        foreach (var e in list) _rows.Add(new Row { kind = RowKind.Item, entry = e });
                    if (_catPath.Length == 0) _rows.Add(new Row { kind = RowKind.NewScript, text = "" });
                }

                _sel = 0;
                while (_sel < _rows.Count && _rows[_sel].kind == RowKind.Header) _sel++;
                if (_sel >= _rows.Count) _sel = 0;
                _scrollToSel = true;
            }

            void Step(int dir)
            {
                int i = _sel;
                for (int guard = 0; guard < _rows.Count; guard++)
                {
                    i += dir;
                    if (i < 0 || i >= _rows.Count) return;
                    if (_rows[i].kind != RowKind.Header) { _sel = i; _scrollToSel = true; return; }
                }
            }

            void GoUp()
            {
                int sl = _catPath.LastIndexOf('/');
                _catPath = sl >= 0 ? _catPath.Substring(0, sl) : "";
                Rebuild();
                editorWindow.Repaint();
            }

            void Activate(Row r)
            {
                switch (r.kind)
                {
                    case RowKind.Item: AddAndClose(r.entry); break;
                    case RowKind.Folder:
                        _catPath = r.text;
                        Rebuild();
                        editorWindow.Repaint();
                        break;
                    case RowKind.NewScript:
                        _newName = r.text.Length > 0 ? r.text : "NewBehaviourScript";
                        _newFocused = false;
                        _runtimeErr = null;
                        _mode = Mode.NewScript;
                        editorWindow.Repaint();
                        break;
                }
            }

            void AddAndClose(Entry e)
            {
                foreach (var g in _targets)
                    if (g != null) Undo.AddComponent(g, e.type);
                CustomsRecentComponents.Record(e.type);
                editorWindow.Close();
                GUIUtility.ExitGUI();
            }

            public override Vector2 GetWindowSize() => new Vector2(_width, 380);

            static GUIContent ContentOf(Entry e)
            {
                if (e.content == null)
                    e.content = new GUIContent(" " + e.label, EditorGUIUtility.ObjectContent(null, e.type).image, e.tip);
                return e.content;
            }

            static string LastSegment(string path)
            {
                int sl = path.LastIndexOf('/');
                return sl >= 0 ? path.Substring(sl + 1) : path;
            }

            public override void OnGUI(Rect rect)
            {
                var ev = Event.current;
                if (_mode == Mode.NewScript) { DrawNewScript(ev); return; }

                // Phím điều hướng xử lý TRƯỚC ô tìm kiếm (ô text sẽ nuốt ↑↓ / Enter nếu để sau)
                if (ev.type == EventType.KeyDown)
                {
                    bool searchEmpty = _search.Length == 0;
                    switch (ev.keyCode)
                    {
                        case KeyCode.DownArrow:
                            Step(1); ev.Use(); editorWindow.Repaint();
                            break;
                        case KeyCode.UpArrow:
                            Step(-1); ev.Use(); editorWindow.Repaint();
                            break;
                        case KeyCode.Return:
                        case KeyCode.KeypadEnter:
                            if (_sel >= 0 && _sel < _rows.Count) Activate(_rows[_sel]);
                            ev.Use();
                            break;
                        case KeyCode.RightArrow:
                            if (searchEmpty && _sel >= 0 && _sel < _rows.Count && _rows[_sel].kind == RowKind.Folder)
                            {
                                Activate(_rows[_sel]);
                                ev.Use();
                            }
                            break;
                        case KeyCode.LeftArrow:
                            if (searchEmpty && _catPath.Length > 0) { GoUp(); ev.Use(); }
                            break;
                        case KeyCode.Escape:
                            editorWindow.Close();
                            GUIUtility.ExitGUI();
                            break;
                    }
                }

                GUI.SetNextControlName("GaMoSearch");
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                if (!_focused)
                {
                    EditorGUI.FocusTextInControl("GaMoSearch");
                    _focused = true;
                }
                if (_search != _lastSearch)
                {
                    _lastSearch = _search;
                    if (_search.Length > 0) _catPath = "";
                    Rebuild();
                }

                // Đang ở trong một nhóm: thanh quay lại
                bool goUp = false;
                if (_search.Length == 0 && _catPath.Length > 0)
                {
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                        if (GUILayout.Button("◂  " + _catPath.Replace("/", "  /  "), EditorStyles.toolbarButton))
                            goUp = true;
                }

                if (_rightMini == null)
                    _rightMini = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };

                Row pending = null;
                // Danh sách chỉ cuộn dọc: ẩn thanh cuộn ngang và luôn giữ x = 0, nếu không popup mở ra bị lệch sang phải
                // (chữ bên trái bị cắt) vì ScrollTo cuộn cả theo chiều ngang.
                _scroll = EditorGUILayout.BeginScrollView(_scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUI.skin.scrollView);
                _scroll.x = 0f;
                for (int i = 0; i < _rows.Count; i++)
                {
                    var row = _rows[i];

                    if (row.kind == RowKind.Header)
                    {
                        var hr = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
                        if (ev.type == EventType.Repaint) GUI.Label(hr, row.text, EditorStyles.miniBoldLabel);
                        continue;
                    }

                    if (row.content == null)
                    {
                        if (row.kind == RowKind.Item) row.content = ContentOf(row.entry);
                        else if (row.kind == RowKind.Folder)
                            row.content = new GUIContent(" " + LastSegment(row.text), EditorGUIUtility.IconContent("Folder Icon").image);
                        else
                            row.content = new GUIContent(row.text.Length > 0 ? " New script \"" + row.text + "\"" : " New script...",
                                EditorGUIUtility.IconContent("cs Script Icon").image);
                    }

                    var r = GUILayoutUtility.GetRect(row.content, EditorStyles.label, GUILayout.Height(20));

                    if (ev.type == EventType.MouseMove && r.Contains(ev.mousePosition) && _sel != i)
                    {
                        _sel = i;
                        editorWindow.Repaint();
                    }
                    if (ev.type == EventType.Repaint && i == _sel)
                    {
                        EditorGUI.DrawRect(r, new Color(Accent.r, Accent.g, Accent.b, 0.35f));
                        if (_scrollToSel)
                        {
                            _scrollToSel = false;
                            GUI.ScrollTo(new Rect(0f, r.y, 1f, r.height)); // chỉ cuộn dọc
                            editorWindow.Repaint();
                        }
                    }

                    var oldIconSize = EditorGUIUtility.GetIconSize();
                    EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                    bool clicked = GUI.Button(r, row.content, EditorStyles.label);
                    EditorGUIUtility.SetIconSize(oldIconSize);

                    // Chú thích bên phải: nhóm của component (khi đang tìm kiếm) hoặc mũi tên vào nhóm
                    if (ev.type == EventType.Repaint)
                    {
                        var rr = new Rect(r.x, r.y, r.width - 6, r.height);
                        if (row.kind == RowKind.Folder) GUI.Label(rr, "▸", _rightMini);
                        else if (row.kind == RowKind.Item && _search.Length > 0 && row.entry.category.Length > 0)
                            GUI.Label(rr, row.entry.category, _rightMini);
                    }

                    if (clicked) pending = row;
                }

                if (_rows.Count == 0) GUILayout.Label("Không tìm thấy.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndScrollView();

                // Xử lý sau khi đã EndScrollView: Activate có thể đổi _rows / đóng popup
                if (goUp) GoUp();
                if (pending != null) Activate(pending);
            }

            // ---------- New script ----------
            string ValidateNew()
            {
                string n = (_newName ?? "").Trim();
                string folder = NormalizeFolder(_newFolder);
                if (n == _valName && folder == _valFolder) return _valErr;
                _valName = n; _valFolder = folder;

                string err = null;
                if (!IsValidClassName(n)) err = "Tên không hợp lệ: chỉ dùng chữ, số, _ và không bắt đầu bằng số.";
                else if (folder != "Assets" && !folder.StartsWith("Assets/", StringComparison.Ordinal) || folder.Contains(".."))
                    err = "Thư mục phải nằm trong Assets/.";
                else if (File.Exists(folder + "/" + n + ".cs")) err = "Đã có file " + folder + "/" + n + ".cs";
                else
                {
                    foreach (var t in TypeCache.GetTypesDerivedFrom<UObject>())
                        if (t.Name == n && t.Namespace == null) { err = "Đã có class tên \"" + n + "\" trong project."; break; }
                }
                return _valErr = err;
            }

            static string NormalizeFolder(string f)
            {
                f = (f ?? "").Trim().Replace('\\', '/').TrimEnd('/');
                return f.Length == 0 ? "Assets" : f;
            }

            void DrawNewScript(Event ev)
            {
                if (ev.type == EventType.KeyDown)
                {
                    if (ev.keyCode == KeyCode.Escape)
                    {
                        _mode = Mode.List;
                        ev.Use();
                        editorWindow.Repaint();
                        return;
                    }
                    if ((ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter) && ValidateNew() == null)
                    {
                        ev.Use();
                        CreateScriptAndAdd();
                        return;
                    }
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if (GUILayout.Button("◂", EditorStyles.toolbarButton, GUILayout.Width(26)))
                    {
                        _mode = Mode.List;
                        editorWindow.Repaint();
                        GUIUtility.ExitGUI();
                    }
                    GUILayout.Label("New script", EditorStyles.boldLabel);
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins))
                {
                    GUILayout.Space(6);
                    GUILayout.Label("Tên (cũng là tên class)");
                    GUI.SetNextControlName("GaMoNewName");
                    _newName = EditorGUILayout.TextField(_newName);
                    if (!_newFocused)
                    {
                        EditorGUI.FocusTextInControl("GaMoNewName");
                        _newFocused = true;
                    }

                    GUILayout.Space(4);
                    GUILayout.Label("Thư mục");
                    _newFolder = EditorGUILayout.TextField(_newFolder);

                    GUILayout.Space(6);
                    string err = ValidateNew();
                    if (err != null) EditorGUILayout.HelpBox(err, MessageType.Warning);
                    else if (_runtimeErr != null) EditorGUILayout.HelpBox(_runtimeErr, MessageType.Error);
                    else EditorGUILayout.HelpBox("Tạo file .cs rồi tự thêm vào " + _targets.Length
                        + " object đang chọn sau khi Unity compile xong.", MessageType.None);

                    using (new EditorGUI.DisabledScope(err != null))
                        if (GUILayout.Button("Create and Add", GUILayout.Height(24)))
                            CreateScriptAndAdd();
                }
            }

            void CreateScriptAndAdd()
            {
                string name = _newName.Trim();
                string folder = NormalizeFolder(_newFolder);
                string path = folder + "/" + name + ".cs";
                try
                {
                    Directory.CreateDirectory(folder);
                    File.WriteAllText(path, string.Format(ScriptTemplate, name), new UTF8Encoding(false));
                }
                catch (Exception ex)
                {
                    _runtimeErr = "Không tạo được file: " + ex.Message;
                    return;
                }

                EditorPrefs.SetString(FolderPrefKey, folder);
                CustomsPendingScriptAdd.Enqueue(name, _targets); // Unity recompile (domain reload) => thêm component sau khi reload xong
                AssetDatabase.Refresh();
                editorWindow.Close();
                GUIUtility.ExitGUI();
            }
        }
    }
}
#endif
