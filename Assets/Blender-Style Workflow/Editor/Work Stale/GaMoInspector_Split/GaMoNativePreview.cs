// GaMoNativePreview.cs  -  part of GaMo Inspector tool
// Tach Preview panel thanh UnityEditor.PreviewWindow goc bang reflection
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

    // Mở Preview Window GỐC của Unity (UnityEditor.PreviewWindow, internal) bằng reflection.
    // PreviewWindow không tự có dữ liệu: nó vẽ preview từ tracker của một InspectorWindow "cha" (SetParentInspector).
    // Cửa sổ này không có Inspector gốc nên tạo 1 InspectorWindow ẨN (không Show), khóa tracker của nó vào asset, rồi gắn Preview vào.
    // Cha ẩn bị hủy khi Preview đóng / khi recompile (khi đó Preview gốc tự đóng). Lỗi ở bất kỳ bước nào => trả false (khung Preview trong cửa sổ chính vẫn còn).
    internal static class CustomsNativePreview
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        sealed class Pair { public EditorWindow parent, preview; public ActiveEditorTracker tracker; public CustomsEditor owner; public int lastOwnIdx = -2; public double nextSync, nextResolve; public ReorderableList own, theirs; }
        static readonly List<Pair> _pairs = new List<Pair>();
        static bool _hooked, _warned;

        public static bool Owns(UObject w)
        {
            foreach (var p in _pairs)
                if (p.parent == w || p.preview == w) return true;
            return false;
        }

        public static bool HasFor(CustomsEditor owner)
        {
            if (owner == null) return false;
            foreach (var p in _pairs)
                if (p.owner == owner && p.preview != null) return true;
            return false;
        }

        public static void CloseFor(CustomsEditor owner)
        {
            foreach (var p in _pairs)
                if (p.owner == owner && p.preview != null) p.preview.Close(); // Sweep dọn cha ẩn + vẽ lại cửa sổ chính
        }

        // Đổi asset: mở lại Preview gốc cho asset mới, giữ nguyên vị trí / kích thước (tracker của cha cache preview cũ nên dựng mới cho chắc)
        public static void ReopenFor(CustomsEditor owner, UObject[] assets)
        {
            Pair found = null;
            foreach (var p in _pairs)
                if (p.owner == owner && p.preview != null) { found = p; break; }
            if (found == null) return;
            var rect = found.preview.position;
            found.preview.Close();
            var copy = (UObject[])assets.Clone();
            EditorApplication.delayCall += () =>
            {
                if (owner == null) return;
                if (!TryOpen(owner, copy, rect)) owner.Repaint(); // mở lại lỗi: khung Preview trong cửa sổ chính hiện lại
            };
        }

        public static bool TryOpen(CustomsEditor owner, UObject[] assets, Rect rect)
        {
            EditorWindow parent = null, preview = null;
            try
            {
                if (assets == null || assets.Length == 0) return false;
                var asm = typeof(EditorWindow).Assembly;
                var inspT = asm.GetType("UnityEditor.InspectorWindow");
                var prevT = asm.GetType("UnityEditor.PreviewWindow");
                if (inspT == null || prevT == null) return false;

                parent = ScriptableObject.CreateInstance(inspT) as EditorWindow; // không Show: chỉ để giữ tracker
                if (parent == null) return false;

                var tracker = inspT.GetProperty("tracker", Any)?.GetValue(parent, null) as ActiveEditorTracker;
                var lockM = typeof(ActiveEditorTracker).GetMethod("SetObjectsLockedByThisTracker", Any, null, new[] { typeof(List<UObject>) }, null);
                var setParent = prevT.GetMethod("SetParentInspector", Any);
                if (tracker == null || lockM == null || setParent == null) throw new InvalidOperationException("API nội bộ khác bản này");

                var list = new List<UObject>();
                foreach (var a in assets) if (a != null) list.Add(a);
                if (list.Count == 0) { UObject.DestroyImmediate(parent); return false; }

                lockM.Invoke(tracker, new object[] { list });
                tracker.isLocked = true;
                tracker.ForceRebuild();

                preview = ScriptableObject.CreateInstance(prevT) as EditorWindow;
                if (preview == null) throw new InvalidOperationException("Không tạo được PreviewWindow");
                setParent.Invoke(preview, new object[] { parent });

                // Unity 2023+: cha cần biết đang có Preview nổi (không có thì bỏ qua)
                var fl = inspT.GetProperty("hasFloatingPreviewWindow", Any);
                if (fl != null && fl.CanWrite && fl.PropertyType == typeof(bool)) fl.SetValue(parent, true, null);

                preview.Show();
                preview.position = rect;

                _pairs.Add(new Pair { parent = parent, preview = preview, tracker = tracker, owner = owner });
                Hook();
                return true;
            }
            catch (Exception ex)
            {
                if (!_warned)
                {
                    _warned = true;
                    var inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                    Debug.LogWarning("[GaMo] Không mở được Preview Window gốc ở bản Unity này (khung Preview trong cửa sổ vẫn dùng bình thường). " + inner.Message);
                }
                try { if (preview != null) UObject.DestroyImmediate(preview); } catch (Exception) { }
                try { if (parent != null) UObject.DestroyImmediate(parent); } catch (Exception) { }
                return false;
            }
        }

        static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            EditorApplication.update += Sweep;
            EditorApplication.projectChanged += RebuildTrackers;
            AssemblyReloadEvents.beforeAssemblyReload += DestroyAll;
        }

        static void Unhook()
        {
            if (!_hooked) return;
            _hooked = false;
            EditorApplication.update -= Sweep;
            EditorApplication.projectChanged -= RebuildTrackers;
            AssemblyReloadEvents.beforeAssemblyReload -= DestroyAll;
        }

        // ---------- Đồng bộ clip đang chọn ----------
        // Preview gốc dùng editor RIÊNG do tracker của cha ẩn dựng (không phải editor trong cửa sổ Customs), nên clip đang chọn
        // (ReorderableList m_ClipList của ModelImporterClipEditor) của hai bên độc lập nhau: mới mở thì luôn là clip đầu tiên,
        // và đổi clip trong cửa sổ Customs thì Preview gốc không biết. Ta chép chỉ số clip từ bên Customs sang bên Preview.
        // Toàn bộ dựa vào reflection: không tìm thấy thì bỏ qua êm (Preview vẫn chạy, chỉ không đồng bộ clip).
        static ReorderableList FindClipList(UEditor importerEd)
        {
            if (importerEd == null) return null;
            try
            {
                for (var t = importerEd.GetType(); t != null && t != typeof(UEditor); t = t.BaseType)
                {
                    foreach (var f in t.GetFields(Any | BindingFlags.DeclaredOnly))
                    {
                        if (!f.FieldType.IsArray) continue;
                        var arr = f.GetValue(importerEd) as Array;
                        if (arr == null) continue;
                        foreach (var tab in arr)
                        {
                            if (tab == null || tab.GetType().Name != "ModelImporterClipEditor") continue;
                            var lf = tab.GetType().GetField("m_ClipList", Any);
                            var list = lf != null ? lf.GetValue(tab) as ReorderableList : null;
                            if (list != null) return list;
                        }
                    }
                }
            }
            catch (Exception) { }
            return null;
        }

        // ---------- Hỏi trạng thái Play của AvatarPreview (khung Preview animation của Model Import Settings) ----------
        // ModelImporterEditor.RequiresConstantRepaint() luôn false dù clip đang chạy (Unity gốc tự có cơ chế vẽ riêng), nên cửa sổ này
        // không biết khi nào cần vẽ liên tục. Đọc thẳng ModelImporterClipEditor.m_AvatarPreview.timeControl.playing bằng reflection.
        // Kết quả: 1 = đang Play, 0 = đang Pause, -1 = không đọc được (API nội bộ đổi) => coi như đang Play để Preview không bị đứng.
        public static int LastPlayState = -2;
        sealed class ApCache { public object tc; public MemberInfo playing; public double next; public int fails; }
        static readonly Dictionary<UEditor, ApCache> _apCache = new Dictionary<UEditor, ApCache>(); // mỗi editor 1 mục: nhiều cửa sổ không giành nhau

        static object FindClipTab(UEditor importerEd)
        {
            for (var t = importerEd.GetType(); t != null && t != typeof(UEditor); t = t.BaseType)
            {
                foreach (var f in t.GetFields(Any | BindingFlags.DeclaredOnly))
                {
                    if (!f.FieldType.IsArray) continue;
                    var arr = f.GetValue(importerEd) as Array;
                    if (arr == null) continue;
                    foreach (var tab in arr)
                        if (tab != null && tab.GetType().Name == "ModelImporterClipEditor") return tab;
                }
            }
            return null;
        }

        static bool _apLogged; static string _apDump = "";

        // Duyệt đồ thị đối tượng từ root (theo field), tìm object có TÊN KIỂU = typeName. Không phụ thuộc tên field nội bộ của Unity
        // (m_AvatarPreview có thể nằm trong editor con của tab clip, đổi tên theo bản). Giới hạn độ sâu + số node để rẻ.
        static object FindByTypeName(object root, string typeName, int maxDepth, int maxNodes)
        {
            _apDump = "";
            if (root == null) return null;
            var seen = new HashSet<object>(_refEq);                 // trước đây là List + duyệt tuyến tính (O(n^2))
            var order = new List<object>();
            var depths = new List<int>();
            var queue = new List<KeyValuePair<object, int>> { new KeyValuePair<object, int>(root, 0) };
            for (int qi = 0; qi < queue.Count && order.Count < maxNodes; qi++)
            {
                var o = queue[qi].Key; int depth = queue[qi].Value;
                if (!seen.Add(o)) continue;
                order.Add(o); depths.Add(depth);
                var ot = o.GetType();
                if (ot.Name == typeName) return o;
                if (depth >= maxDepth) continue;

                foreach (var f in WalkFields(ot))
                {
                    object val;
                    try { val = f.GetValue(o); } catch (Exception) { continue; }
                    if (val == null) continue;
                    if (val is UObject && !(val is UEditor)) continue; // asset / texture...: không có gì để tìm bên trong
                    if (val is System.Collections.IList list)
                    {
                        int n = 0;
                        foreach (var el in list)
                        {
                            if (n++ >= 16) break;
                            if (el == null || el.GetType().IsPrimitive || el is string || (el is UObject && !(el is UEditor))) continue;
                            queue.Add(new KeyValuePair<object, int>(el, depth + 1));
                        }
                    }
                    else queue.Add(new KeyValuePair<object, int>(val, depth + 1));
                }
            }
            // Không thấy: chỉ lúc này mới dựng chuỗi liệt kê kiểu đã duyệt (để log cảnh báo), đường thành công không tốn chuỗi nào.
            var sb = new StringBuilder();
            for (int i = 0; i < order.Count && sb.Length < 3000; i++)
                sb.Append(' ', depths[i] * 2).Append(order[i].GetType().Name).Append('\n');
            _apDump = sb.ToString();
            return null;
        }

        sealed class RefEq : IEqualityComparer<object>
        {
            bool IEqualityComparer<object>.Equals(object a, object b) { return ReferenceEquals(a, b); }
            int IEqualityComparer<object>.GetHashCode(object o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }
        static readonly RefEq _refEq = new RefEq();

        // Field đáng duyệt của một type (đã lọc kiểu nguyên thủy / enum / string / delegate / struct): GetFields cấp mảng mới mỗi lần gọi nên cache.
        static readonly Dictionary<Type, FieldInfo[]> _walkFields = new Dictionary<Type, FieldInfo[]>();

        static FieldInfo[] WalkFields(Type ot)
        {
            if (_walkFields.TryGetValue(ot, out var arr)) return arr;
            var list = new List<FieldInfo>();
            for (var bt = ot; bt != null && bt != typeof(object) && bt != typeof(UObject); bt = bt.BaseType)
            {
                foreach (var f in bt.GetFields(Any | BindingFlags.DeclaredOnly))
                {
                    var ft = f.FieldType;
                    if (ft.IsPrimitive || ft.IsEnum || ft == typeof(string) || ft.IsPointer || typeof(Delegate).IsAssignableFrom(ft) || ft == typeof(Type)) continue;
                    if (ft.IsValueType) continue;
                    list.Add(f);
                }
            }
            arr = list.ToArray();
            _walkFields[ot] = arr;
            return arr;
        }

        static object GetMember(object o, params string[] names)
        {
            if (o == null) return null;
            foreach (var n in names)
                for (var bt = o.GetType(); bt != null; bt = bt.BaseType)
                {
                    var fi = bt.GetField(n, Any | BindingFlags.DeclaredOnly);
                    if (fi != null) return fi.GetValue(o);
                    var pi = bt.GetProperty(n, Any | BindingFlags.DeclaredOnly);
                    if (pi != null && pi.GetIndexParameters().Length == 0) return pi.GetValue(o, null);
                }
            return null;
        }

        static MemberInfo FindPlayingMember(Type t)
        {
            string[] exact = { "playing", "isPlaying", "m_Playing" };
            for (var bt = t; bt != null && bt != typeof(object); bt = bt.BaseType)
            {
                foreach (var n in exact)
                {
                    var pi = bt.GetProperty(n, Any | BindingFlags.DeclaredOnly);
                    if (pi != null && pi.PropertyType == typeof(bool) && pi.CanRead && pi.GetIndexParameters().Length == 0) return pi;
                    var fi = bt.GetField(n, Any | BindingFlags.DeclaredOnly);
                    if (fi != null && fi.FieldType == typeof(bool)) return fi;
                }
            }
            // tên member đổi: lấy bool đầu tiên có chữ "play" trong tên
            for (var bt = t; bt != null && bt != typeof(object); bt = bt.BaseType)
            {
                foreach (var pi in bt.GetProperties(Any | BindingFlags.DeclaredOnly))
                    if (pi.PropertyType == typeof(bool) && pi.CanRead && pi.GetIndexParameters().Length == 0
                        && pi.Name.IndexOf("play", StringComparison.OrdinalIgnoreCase) >= 0) return pi;
                foreach (var fi in bt.GetFields(Any | BindingFlags.DeclaredOnly))
                    if (fi.FieldType == typeof(bool) && fi.Name.IndexOf("play", StringComparison.OrdinalIgnoreCase) >= 0) return fi;
            }
            return null;
        }

        static void PurgeApCache()
        {
            List<UEditor> dead = null;
            foreach (var k in _apCache.Keys)
                if (k == null) { if (dead == null) dead = new List<UEditor>(); dead.Add(k); }
            if (dead != null) foreach (var k in dead) _apCache.Remove(k);
        }

        static int AvatarPlayState(UEditor ed)
        {
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (!_apCache.TryGetValue(ed, out var c))
                {
                    c = new ApCache();
                    _apCache[ed] = c;
                    if (_apCache.Count > 16) PurgeApCache();
                }
                // Tìm lại TimeControl mỗi 0.5s (AvatarPreview có thể bị dựng lại khi đổi clip); chưa thấy thì thử thưa hơn (3s) cho rẻ.
                if (now >= c.next)
                {
                    // Model (FBX): TimeControl nằm trong tab clip. BlendTree / AnimationClip / Transition...: nằm trong editor của asset đó.
                    object root = ed.target is ModelImporter ? FindClipTab(ed) : (object)ed;
                    c.tc = FindByTypeName(root, "TimeControl", 6, 400);
                    c.playing = c.tc != null ? FindPlayingMember(c.tc.GetType()) : null;
                    if (c.tc != null) { c.fails = 0; c.next = now + 0.5; }
                    else
                    {
                        c.fails++;
                        // 5 lần đầu thử lại mỗi 0.3s (chờ AvatarPreview được dựng lười), sau đó thưa dần 3s
                        c.next = now + (c.fails < 5 ? 0.3 : 3.0);
                        if (c.fails == 8 && !_apLogged)
                        {
                            _apLogged = true;
                            Debug.LogWarning("[GaMo] Không tìm thấy TimeControl của AvatarPreview (Preview sẽ vẽ liên tục). Các kiểu đã duyệt:\n" + _apDump);
                        }
                    }
                }
                if (c.tc == null || c.playing == null) return -1;
                object v = c.playing is PropertyInfo pi ? pi.GetValue(c.tc, null) : ((FieldInfo)c.playing).GetValue(c.tc);
                return v is bool b ? (b ? 1 : 0) : -1;
            }
            catch (Exception) { return -1; }
        }

        // Audio: AudioClipInspector / AudioImporterInspector có thể không báo RequiresConstantRepaint dù đang phát => hỏi AudioUtil (internal).
        static MethodInfo _audioPlaying; static bool _audioInit;
        static bool AudioPreviewPlaying()
        {
            if (!_audioInit)
            {
                _audioInit = true;
                try
                {
                    var t = typeof(UEditor).Assembly.GetType("UnityEditor.AudioUtil");
                    if (t != null) _audioPlaying = t.GetMethod("IsPreviewClipPlaying", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                }
                catch (Exception) { }
            }
            try { return _audioPlaying != null && (bool)_audioPlaying.Invoke(null, null); }
            catch (Exception) { return false; }
        }

        // Asset có Preview animation (AvatarPreview): Model, BlendTree / AnimationClip (Motion), State, Transition, StateMachine, Avatar
        static bool IsAnimationTarget(UObject t)
        {
            return t is ModelImporter || t is Motion || t is Avatar
                || t is UnityEditor.Animations.AnimatorState
                || t is UnityEditor.Animations.AnimatorTransitionBase
                || t is UnityEditor.Animations.AnimatorStateMachine;
        }

        // Preview của editor này có đang cần vẽ liên tục không (animation đang Play)
        public static bool WantsConstantRepaint(UEditor ed)
        {
            if (ed == null) return false;
            if (ed.RequiresConstantRepaint()) { LastPlayState = 2; return true; }
            // Chỉ editor animation mới có AvatarPreview/TimeControl (Model, BlendTree, AnimationClip...). Material, Texture... không quét.
            if (ed.target is AudioImporter || ed.target is AudioClip) { bool ap = AudioPreviewPlaying(); LastPlayState = ap ? 3 : 0; return ap; }
            if (!IsAnimationTarget(ed.target)) { LastPlayState = -3; return false; }
            int st = AvatarPlayState(ed);
            LastPlayState = st;
            return st != 0; // 1 = Play; -1 = không biết => vẽ liên tục cho chắc
        }

        static void SyncClipSelection(Pair p)
        {
            try
            {
                if (p.owner == null || p.tracker == null || p.preview == null) return;

                // Dò ReorderableList bằng reflection khá tốn (duyệt field + activeEditors) => cache trong Pair,
                // dò lại mỗi 1s (0.25s khi chưa tìm thấy) thay vì 10 lần/giây.
                double now = EditorApplication.timeSinceStartup;
                if (now >= p.nextResolve)
                {
                    p.own = FindClipList(p.owner.ImporterEditorForSync);
                    p.theirs = null;
                    if (p.own != null)
                    {
                        var eds = p.tracker.activeEditors;
                        if (eds != null)
                            foreach (var e in eds) { p.theirs = FindClipList(e); if (p.theirs != null) break; }
                    }
                    p.nextResolve = now + (p.own != null && p.theirs != null ? 1.0 : 0.25);
                }
                var own = p.own;
                var theirs = p.theirs;
                if (own == null || theirs == null) return; // editor của cha ẩn chưa dựng xong: thử lại ở nhịp sau

                int idx = own.index;
                if (theirs.index != idx && idx != p.lastOwnIdx)
                {
                    theirs.index = idx;
                    if (theirs.onSelectCallback != null) theirs.onSelectCallback(theirs); // để ModelImporterClipEditor nạp clip mới vào preview
                    p.preview.Repaint();
                }
                p.lastOwnIdx = idx;
            }
            catch (Exception) { }
        }

        // Preview đã đóng => hủy cha ẩn (nếu không sẽ rò: PreviewWindow.OnDestroy cố ý không dọn tracker của cha)
        static void Sweep()
        {
            for (int i = _pairs.Count - 1; i >= 0; i--)
            {
                var p = _pairs[i];
                if (p.preview != null && p.parent != null)
                {
                    double now = EditorApplication.timeSinceStartup;
                    if (now >= p.nextSync) { p.nextSync = now + 0.1; SyncClipSelection(p); }
                    continue;
                }
                if (p.preview != null) p.preview.Close();
                if (p.parent != null) UObject.DestroyImmediate(p.parent);
                _pairs.RemoveAt(i);
                if (p.owner != null) p.owner.Repaint(); // Preview gốc đã đóng: khung Preview trong cửa sổ chính hiện lại
            }
            if (_pairs.Count == 0) Unhook();
        }

        // Cha ẩn không nhận Update của Unity nên tracker không tự làm mới sau reimport / đổi asset => ép dựng lại
        static void RebuildTrackers()
        {
            foreach (var p in _pairs)
            {
                try { p.own = null; p.theirs = null; p.nextResolve = 0; if (p.parent != null && p.tracker != null) p.tracker.ForceRebuild(); }
                catch (Exception) { }
            }
        }

        static void DestroyAll()
        {
            foreach (var p in _pairs)
            {
                try { if (p.preview != null) UObject.DestroyImmediate(p.preview); } catch (Exception) { }
                try { if (p.parent != null) UObject.DestroyImmediate(p.parent); } catch (Exception) { }
            }
            var owners = new List<CustomsEditor>();
            foreach (var p in _pairs) if (p.owner != null) owners.Add(p.owner);
            _pairs.Clear();
            Unhook();
            foreach (var o in owners) o.Repaint();
        }
    }
}
#endif