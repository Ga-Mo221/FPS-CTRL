// GaMoInspector.ComponentButtons.cs  -  part of CustomsEditor (GaMo Inspector)
// QUAN LY nut component (tab): click chon, thu tu, DrawComponentBar
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
        // ---------- Xử lý click tab ----------
        void OnTabClicked(Component c, bool multi)
        {
            if (multi)
            {
                if (_selected.Contains(c))
                {
                    if (_selected.Count > 1) _selected.Remove(c);   // luôn giữ lại ít nhất 1 tab
                }
                else _selected.Add(c);
            }
            else
            {
                _selected.Clear();
                _selected.Add(c);
            }

            RememberSelectedTypes();

            if (Event.current != null) GUI.FocusControl(null); // phím tắt gọi từ ngoài IMGUI thì không có Event
            RequestSync();
        }

        // Bỏ tab đã bị xóa/ẩn khỏi _selected; nếu hết tab thì mở tab mặc định.
        void PruneSelected(List<Component> shown)
        {
            _tmpRemove.Clear(); // không dùng RemoveWhere + lambda bắt biến (cấp closure mỗi lần, mà hàm này chạy mỗi event)
            foreach (var cc in _selected)
                if (cc == null || !shown.Contains(cc)) _tmpRemove.Add(cc);
            for (int i = 0; i < _tmpRemove.Count; i++) _selected.Remove(_tmpRemove[i]);
            _tmpRemove.Clear();
            if (_selected.Count > 0) return;
            var def = PickDefaultTab(shown);
            if (def != null) _selected.Add(def);
        }

        // Tab mặc định khi chưa có tab nào được mở: nút component thứ 2 (thường là component "thật" đầu tiên sau Transform)
        // nếu có từ 2 nút trở lên; chỉ có 1 nút thì lấy nút đó.
        static Component PickDefaultTab(IList<Component> list)
        {
            Component first = null;
            foreach (var c in list)
            {
                if (c == null) continue;
                if (first == null) first = c; else return c;
            }
            return first;
        }

        // Component "tương ứng" trên MỌI object đang chọn (cùng type, cùng thứ tự). null nếu có object thiếu.
        // Phần tử [0] luôn là c (của object chính). Cache theo frame GUI vì hàm này gọi nhiều lần.
        Component[] GetTargets(Component c)
        {
            if (c == null) return null;
            if (_targetCache.TryGetValue(c, out var cached)) return cached;

            Component[] result = null;
            if (_gos.Count <= 1) result = new[] { c };
            else
            {
                var t = c.GetType();
                int k = 0; // c là component thứ k của type t trên object chính
                foreach (var cc in GetComps())
                {
                    if (cc == c) break;
                    if (cc != null && cc.GetType() == t) k++;
                }

                var list = new List<Component>(_gos.Count) { c };
                bool ok = true;
                foreach (var g in _gos)
                {
                    if (g == null || g == _go) continue;
                    Component found = null;
                    int idx = 0;
                    g.GetComponents(_tmpB);
                    foreach (var cc in _tmpB)
                    {
                        if (cc == null || cc.GetType() != t || (cc.hideFlags & HideFlags.HideInInspector) != 0) continue;
                        if (idx++ == k) { found = cc; break; }
                    }
                    if (found == null) { ok = false; break; }
                    list.Add(found);
                }
                if (ok) result = list.ToArray();
            }

            _targetCache[c] = result;
            return result;
        }

        static bool SameTargets(Component[] a, Component[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        // ---------- Hàng nút component ----------
        const int FilterThreshold = 8; // nhiều hơn số component này thì tự hiện ô lọc 

        Component[] GetComps()
        {
            if (_compsCache == null)
            {
                _go.GetComponents(_tmpA);
                // Giống Inspector gốc: component gắn HideFlags.HideInInspector (vd URP Additional Data) không hiện thành tab
                _tmpA.RemoveAll(cc => cc != null && (cc.hideFlags & HideFlags.HideInInspector) != 0);
                _compsCache = _tmpA.ToArray();
            }
            return _compsCache;
        }

        Texture GetCompIcon(Component c)
        {
            if (c == null) return null;
            var t = c.GetType();
            if (_iconCache.TryGetValue(t, out var tex) && tex != null) return tex;
            if (_iconMissing.Contains(t)) return null;
            tex = EditorGUIUtility.ObjectContent(c, t).image;
            if (tex != null) _iconCache[t] = tex; else _iconMissing.Add(t);
            return tex;
        }

        string GetCompName(Type t)
        {
            if (!_nameCache.TryGetValue(t, out var n))
            {
                n = ObjectNames.NicifyVariableName(t.Name);
                _nameCache[t] = n;
            }
            return n;
        }

        float GetTextWidth(GUIStyle style, string text)
        {
            if (!Mathf.Approximately(_textWCacheScale, _tabScale)) { _textWCache.Clear(); _textWCacheScale = _tabScale; }
            if (!_textWCache.TryGetValue(text, out var w))
            {
                w = style.CalcSize(new GUIContent(text)).x;
                _textWCache[text] = w;
            }
            return w;
        }

        GUIStyle _tabTxt;                 // style chữ dùng chung cho MỌI tab (có ghim hay không)
        const float TabTextNudgeY = 0f;   // nếu mắt thấy chữ còn lệch 1px thì chỉnh số này (+ xuống, - lên)

        void DrawComponentBar()
        {
            bool multiSel = _gos.Count > 1;
            if (Event.current.type == EventType.Layout) RefreshOverrides();

            // Chọn nhiều object: chỉ hiện component có ở TẤT CẢ object (giống Inspector gốc)
            var all = GetComps();
            _barTmp.Clear();
            var comps = _barTmp;
            foreach (var c0 in all)
            {
                if (c0 == null) { if (!multiSel) comps.Add(null); continue; }
                if (multiSel && GetTargets(c0) == null) continue;
                comps.Add(c0);
            }

            PruneSelected(comps); // dọn tab đã bị xóa/ẩn, luôn có ít nhất 1 tab mở

            // Ô lọc component (tự hiện khi có nhiều component)
            if (_filter.Length > 0 || comps.Count > FilterThreshold)
            {
                GUILayout.Space(4);
                _filter = EditorGUILayout.TextField(_filter ?? "", EditorStyles.toolbarSearchField);
            }
            string fl = (_filter ?? "").Trim();

            GUILayout.Space(4);
            float maxW = position.width - 24f, x = 0; // -24: lề + bề rộng thanh cuộn dọc (bar giờ nằm trong scrollview)

            // Cỡ nút theo _tabScale (chỉnh bằng nút -/+ ở TopBar, hoặc Ctrl/Cmd + cuộn chuột ngay trên hàng này).
            float sIconPx = Mathf.Round(IconPx * _tabScale);
            float sToggleBox = Mathf.Round(16f * _tabScale);
            float sToggleW = Mathf.Round(4f * _tabScale) + sToggleBox + Mathf.Round(2f * _tabScale);
            float sRowH = Mathf.Round(22f * _tabScale);
            float sGap = Mathf.Round(4f * _tabScale);
            float sSpace = Mathf.Round(TabSpacing * _tabScale);
            var tabFont = ScaledTabLabelStyle;

            _barComps.Clear();
            int newHover = -1;
            bool dropHit = false; // có tab nào nhận sự kiện kéo-thả trong event này không
            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < comps.Count; i++)
            {
                var c = comps[i];
                string name = c == null ? "Missing" : GetCompName(c.GetType());

                // Đang lọc: ẩn tab không khớp (tab đang mở vẫn giữ lại)
                if (c != null && fl.Length > 0 && !_selected.Contains(c)
                    && name.IndexOf(fl, StringComparison.OrdinalIgnoreCase) < 0) continue;
                _barComps.Add(c);

                Texture icon = GetCompIcon(c);
                var tkey = c == null ? typeof(void) : c.GetType();
                if (!_tabContentCache.TryGetValue(tkey, out var content))
                {
                    content = new GUIContent(" " + name, icon);
                    _tabContentCache[tkey] = content;
                }

                bool hasEnable = c is Behaviour || c is Renderer || c is Collider;
                // Đo CHỮ riêng (theo cỡ chữ đã scale) + cộng icon đã scale. KHÔNG đo cả content có icon, vì
                // CalcSize lấy kích thước GỐC của texture (icon custom 64/128px sẽ làm nút phình to).
                float textW = GetTextWidth(tabFont, content.text);
                // Chỉ component bật/tắt được mới có ô vuông phía trước (Transform, MeshFilter... không có => đỡ chiếm chỗ).
                float lead = hasEnable ? sToggleW : Mathf.Round(2f * _tabScale);
                float w = textW + (icon != null ? sIconPx : 0) + lead + sGap;

                if (x + w > maxW && x > 0)
                {
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(sSpace); // khoảng cách giữa các dòng nút
                    EditorGUILayout.BeginHorizontal();
                    x = 0;
                }
                x += w + sSpace;

                var r = GUILayoutUtility.GetRect(w, sRowH, GUILayout.Width(w));
                if (Event.current.type == EventType.MouseMove && r.Contains(Event.current.mousePosition)) newHover = i;
                GUILayout.Space(sSpace); // khoảng cách giữa các nút trên cùng dòng
                var btnRect = r;

                bool isOpen = c != null && _selected.Contains(c);
                bool multi = Event.current.shift || EditorGUI.actionKey;

                // Trạng thái enable (khi chọn nhiều object: mixed nếu các object không giống nhau)
                var tg = hasEnable ? GetTargets(c) : null;
                bool curEn = hasEnable && GetEnabled(c);
                bool mixedEn = false;
                if (tg != null)
                    foreach (var t in tg)
                        if (GetEnabled(t) != curEn) { mixedEn = true; break; }
                bool en = !hasEnable || mixedEn || curEn;

                // Màu nền theo màu ghim (Default / Selected); không ghim: mở = cam, đóng = nút mặc định, disable = xám
                int pin = 0; Color pinTop = Color.clear, pinBot = Color.clear;
                if (c != null) CustomsTabColors.GetPin(c.GetType(), isOpen, out pin, out pinTop, out pinBot);
                ResolveTabColors(isOpen, pin, pinTop, pinBot, en, out Color topC, out Color botC);
                bool tinted = pin > 0 || isOpen || !en;

                // 1) Vẽ MỘT nền duy nhất cho cả box (toggle + icon + tên nằm chung trong box này)
                if (Event.current.type == EventType.Repaint)
                {
                    bool hover = r.Contains(Event.current.mousePosition);
                    if (tinted) DrawTabFill(r, topC, botC, hover, Mathf.Max(2f, Mathf.Round(CornerRadius * _tabScale)));
                    else GUI.skin.button.Draw(r, GUIContent.none, hover, false, false, false);
                    // Thanh xanh = component có override so với prefab gốc (hoặc là component thêm vào instance)
                    if (c != null && !multiSel && (_ovrAdded.Contains(c) || _ovrMod.Contains(c)))
                        EditorGUI.DrawRect(new Rect(r.x, r.y + 3f, 3f, r.height - 6f), OverrideBlue); // thụt 3px trên/dưới cho khỏi lòi ra góc bo
                    if (c != null && _dropTarget == c) DrawDropMark(r, true, DropAfter(c));
                }

                // 2) Ô vuông nằm bên trong box (vẽ sau nền, trước nút => nhận click trước).
                var tRect = new Rect(r.x + Mathf.Round(4f * _tabScale), r.y + (r.height - sToggleBox) * 0.5f, sToggleBox, sToggleBox);
                var clickRect = new Rect(r.x + lead, r.y, r.width - lead, r.height);
                if (hasEnable)
                {
                    EditorGUI.showMixedValue = mixedEn;
                    EditorGUI.BeginChangeCheck();
                    bool newEn = EditorGUI.Toggle(tRect, curEn);
                    bool enChanged = EditorGUI.EndChangeCheck();
                    EditorGUI.showMixedValue = false;
                    if (enChanged)
                        foreach (var t in tg ?? new[] { c }) SetEnabled(t, newEn); // áp cho tất cả object đang chọn
                }

                // 3) Vùng bấm chọn tab: style trong suốt (không tự vẽ nền) để chỉ còn nền chung ở bước 1
                var oldIconSize = EditorGUIUtility.GetIconSize();
                var oldContent = GUI.contentColor;
                EditorGUIUtility.SetIconSize(new Vector2(sIconPx, sIconPx)); // icon theo cỡ đã scale
                if (!en) GUI.contentColor = new Color(1f, 1f, 1f, 0.5f);   // chữ + icon mờ khi disable
                // Bắt đầu kéo để đổi thứ tự: nhấn trên tab rồi kéo quá 5px (GUI.Button bên dưới vẫn nhận click bình thường)
                if (c != null && !multiSel && !(c is Transform))
                {
                    var dv = Event.current;
                    if (dv.type == EventType.MouseDown && dv.button == 0 && clickRect.Contains(dv.mousePosition))
                    { _pressTab = c; _pressPos = dv.mousePosition; }
                    else if (_pressTab == c && dv.type == EventType.MouseDrag && (dv.mousePosition - _pressPos).sqrMagnitude > 25f)
                    {
                        _pressTab = null;
                        StartReorderDrag(c, name);
                        GUIUtility.hotControl = 0;
                        dv.Use();
                    }
                }
                // Chuột phải: menu nhanh. PHẢI xử lý TRƯỚC GUI.Button: Button nuốt mọi MouseDown (kể cả chuột phải)
                // và gọi Use(), nên kiểm tra sau nó thì event đã là Used và menu không bao giờ hiện.
                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 1 && btnRect.Contains(e.mousePosition))
                {
                    var at = new Rect(e.mousePosition.x, e.mousePosition.y, 0, 0);
                    e.Use();
                    if (c != null) ShowQuickTabMenu(c, at);
                    else ShowMissingMenu(at);
                    // ExitGUI ném exception => các dòng khôi phục bên dưới KHÔNG chạy. contentColor / icon size là trạng thái
                    // toàn cục nên nếu để rò sẽ làm mờ/mất chữ ở các nút khác (thanh trên cùng...). Khôi phục trước khi thoát.
                    GUI.contentColor = oldContent;
                    EditorGUIUtility.SetIconSize(oldIconSize);
                    GUIUtility.ExitGUI();
                }
                // Vẽ icon + chữ THỦ CÔNG, căn giữa theo chiều cao nút. GUI.Button(content có icon) căn khối theo kích thước GỐC của texture:
                // icon gốc cao hơn nút => không căn giữa được, icon + chữ dính lên mép trên.
                bool clicked = GUI.Button(clickRect, GUIContent.none, GUIStyle.none); // vùng bấm
                if (Event.current.type == EventType.Repaint)
                {
                    float tx = clickRect.x + Mathf.Round(2f * _tabScale);
                    if (icon != null)
                    {
                        var ir = new Rect(tx, r.y + Mathf.Round((r.height - sIconPx) * 0.5f), sIconPx, sIconPx);
                        var oldCol = GUI.color;
                        GUI.color = new Color(oldCol.r, oldCol.g, oldCol.b, oldCol.a * (en ? 1f : 0.5f)); // mờ khi disable
                        GUI.DrawTexture(ir, icon, ScaleMode.ScaleToFit, true);
                        GUI.color = oldCol;
                        tx += sIconPx;
                    }
                    if (_tabTxt == null)
                        _tabTxt = new GUIStyle(tabFont) { padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
                                                          alignment = TextAnchor.MiddleCenter };
                    _tabTxt.fontSize = tabFont.fontSize;
                    // Không nền màu: giữ đúng màu chữ của nút Unity. Có nền màu: chữ theo độ sáng nền.
                    SetTextColor(_tabTxt, tinted ? ContrastText(topC, botC) : tabFont.normal.textColor);

                    float lh = Mathf.Ceil(_tabTxt.lineHeight);
                    var textRect = new Rect(tx, r.y + Mathf.Round((r.height - lh) * 0.5f) + TabTextNudgeY, clickRect.xMax - tx, lh);
                    GUI.Label(textRect, content.text, _tabTxt);
                }
                GUI.contentColor = oldContent;
                EditorGUIUtility.SetIconSize(oldIconSize);

                if (clicked && c != null) OnTabClicked(c, multi);
                if (c != null && HandleReorderDrop(r, c)) dropHit = true;
            }

            EditorGUILayout.EndHorizontal();
            if (Event.current.rawType == EventType.MouseUp) _pressTab = null;
            if (Event.current.type == EventType.DragUpdated && !dropHit && _dropTarget != null) { _dropTarget = null; Repaint(); } // kéo ra khoảng trống giữa các tab
            if (Event.current.type == EventType.MouseMove && newHover != _hoverTab) { _hoverTab = newHover; RepaintBar(); }
            GUILayout.Space(2);
            var line = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, new Color(0, 0, 0, 0.4f));
        }

    }
}
#endif