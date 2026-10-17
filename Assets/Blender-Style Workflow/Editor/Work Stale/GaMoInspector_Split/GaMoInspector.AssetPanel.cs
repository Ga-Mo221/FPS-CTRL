// GaMoInspector.AssetPanel.cs  -  part of CustomsEditor (GaMo Inspector)
// Than Inspector cho Asset (ScriptableObject, Importer...), Preview panel, Apply/Revert
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
        void BuildAssetBody(UIE.VisualElement cc)
        {
            if (_importerEditor != null) AddAssetSection(cc, _importerEditor, "Import Settings", false);
            if (_objectEditor != null && _drawObjectBody)
                AddAssetSection(cc, _objectEditor, _importerEditor != null ? "Imported Object" : null, true);
        }

        void AddAssetSection(UIE.VisualElement cc, UEditor ed, string title, bool isObj)
        {
            if (ed == null || ed.target == null) return;
            var sec = new UIE.VisualElement();
            sec.style.marginBottom = 8;
            cc.Add(sec);

            sec.Add(AssetChunk(() => DrawAssetSectionHead(ed, title, isObj), true));

            bool multiBad = ed.targets.Length > 1 && !ed.GetType().IsDefined(typeof(CanEditMultipleObjects), true);
            if (multiBad)
            {
                sec.Add(AssetChunk(() => EditorGUILayout.HelpBox("Multi-object editing not supported.", MessageType.Info)));
            }
            else
            {
                // Importer editor dựng bằng UI Toolkit cần liên kết đặc biệt (Apply/Revert, extraData) => giữ bản IMGUI + thông báo.
                // Editor của object (ScriptableObject, Volume Profile...) thì nhúng bằng InspectorElement.
                var ve = (isObj && UsesUitk(ed.GetType())) ? TryCreateInspectorElement(ed) : null;
                if (ve != null)
                {
                    ve.SetEnabled(!(_assetImmutable || _assetNotEditable));
                    sec.Add(ve);
                }
                else sec.Add(AssetChunk(() => DrawAssetEditorChunk(ed, isObj)));
            }

            if (!isObj) sec.Add(AssetChunk(() => DrawApplyRevertChunk(ed), true));
        }

        void DrawAssetSectionHead(UEditor ed, string title, bool isObj)
        {
            if (ed == null || ed.target == null) return;
            if (!string.IsNullOrEmpty(title))
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    GUILayout.Label(title, EditorStyles.boldLabel);
            GUILayout.Space(4);

            // Material: MaterialEditor chỉ vẽ khi cờ "expanded" bật, và ô chọn Shader nằm ở header của editor
            if (isObj && ed.targets.Length == 1 && ed.target is Material)
            {
                bool old = GUI.enabled;
                GUI.enabled = old && !(_assetImmutable || _assetNotEditable);
                try
                {
                    InternalEditorUtility.SetIsInspectorExpanded(ed.target, true);
                    ed.DrawHeader();
                }
                finally { GUI.enabled = old; }
            }
        }

        // Importer (Import Settings): chỉ mờ khi asset nằm trong Package không sửa được (immutable).
        // Object đã import (Texture2D, StyleSheet...) thì Unity gắn NotEditable sẵn => mờ giống Inspector gốc.
        void DrawAssetEditorChunk(UEditor ed, bool isObj)
        {
            if (ed == null || ed.target == null) return;
            bool ro = isObj ? (_assetImmutable || _assetNotEditable) : _assetImmutable;
            bool old = GUI.enabled;
            GUI.enabled = old && !ro;
            try
            {
                DrawEditorLikeInspector(ed, !isObj);
                if (Event.current.type == EventType.Repaint && ed.RequiresConstantRepaint())
                {
                    // Editor này chính là editor đang vẽ khung Preview (importer của FBX: clip đang play): thứ cần vẽ lại liên tục chỉ là Preview.
                    // Gọi Repaint() đầy đủ sẽ đánh dấu dirty MỌI panel (cả Import Settings nặng) ở mỗi frame => Preview rớt xuống vài fps, nhảy frame.
                    if (ReferenceEquals(ed, GetPreviewEditor())) RepaintForPreview(ed);
                    else Repaint();
                }
            }
            finally { GUI.enabled = old; }
        }

        void DrawApplyRevertChunk(UEditor ed)
        {
            if (ed != null && EditorDrawsOwnApplyRevert(ed.GetType())) return; // tránh 2 cặp nút Revert/Apply
            bool old = GUI.enabled;
            GUI.enabled = old && !_assetImmutable;
            try { DrawApplyRevert(ed); }
            finally { GUI.enabled = old; }
        }

        void DrawPreviewChunk()
        {
            if (CustomsNativePreview.HasFor(this)) return; // đã tách ra Preview Window gốc: giống Inspector gốc, ẩn khung Preview ở đây
            var pe = GetPreviewEditor();
            _previewEdCache = pe;
            if (pe != null) DrawPreviewPanel(pe);
        }

        bool IsImmutableAsset()
        {
            foreach (var o in _assets)
                if (IsImmutablePath(AssetDatabase.GetAssetPath(o))) return true;
            return false;
        }

        static bool IsImmutablePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Packages/", StringComparison.Ordinal)) return false;
            var pi = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            return pi != null && pi.source != UnityEditor.PackageManager.PackageSource.Embedded
                              && pi.source != UnityEditor.PackageManager.PackageSource.Local;
        }

        // ---------- Revert / Apply cho Import Settings ----------
        // Unity 6 vẽ 2 nút này bằng footer riêng của InspectorWindow nên cửa sổ tự dựng phải tự vẽ,
        // gọi các hàm protected của AssetImporterEditor bằng reflection.
        const BindingFlags AnyInst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static MethodInfo FindParamless(Type t, string name)
        {
            try { return t.GetMethod(name, AnyInst, null, Type.EmptyTypes, null); }
            catch (Exception) { return null; }
        }

        static void InvokeSafe(MethodInfo m, object target)
        {
            try { m.Invoke(target, null); }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException is ExitGUIException) throw ex.InnerException;
                Debug.LogWarning("[GaMo] " + m.Name + " lỗi: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }

        sealed class ImporterApi { public PropertyInfo needs; public MethodInfo hasModified, reset, apply; }
        static readonly Dictionary<Type, ImporterApi> _impApi = new Dictionary<Type, ImporterApi>();
        bool _impShow, _impModified = true;

        static ImporterApi GetImporterApi(Type t)
        {
            if (_impApi.TryGetValue(t, out var api)) return api;
            api = new ImporterApi();
            try { api.needs = t.GetProperty("needsApplyRevert", AnyInst); } catch (Exception) { }
            api.hasModified = FindParamless(t, "HasModified");
            api.reset = FindParamless(t, "ResetValues");
            api.apply = FindParamless(t, "ApplyAndImport") ?? FindParamless(t, "Apply");
            _impApi[t] = api;
            return api;
        }

        // Importer editor dựng sẵn trong UnityEditor (Model, Texture, Audio...) đã tự vẽ Revert/Apply ngay trong OnInspectorGUI.
        // Editor từ package / của dự án (InputActions, ScriptedImporter tùy biến...) thường dựa vào footer của InspectorWindow
        // nên vẫn cần cặp nút do cửa sổ này vẽ.
        static bool EditorDrawsOwnApplyRevert(Type t)
        {
            var n = t.Assembly.GetName().Name;
            return n != null && n.StartsWith("UnityEditor", StringComparison.Ordinal);
        }

        void DrawApplyRevert(UEditor ed)
        {
            if (ed == null || ed.target == null) return;
            var api = GetImporterApi(ed.GetType());
            if (api.apply == null) return;

            // Chỉ hỏi editor ở event Layout (không phải mọi event) => bố cục ổn định giữa Layout/Repaint, đỡ tốn.
            if (Event.current.type == EventType.Layout)
            {
                _impShow = true;
                _impModified = true;
                try { if (api.needs != null && api.needs.PropertyType == typeof(bool) && !(bool)api.needs.GetValue(ed)) _impShow = false; }
                catch (Exception) { }
                try { if (api.hasModified != null) _impModified = (bool)api.hasModified.Invoke(ed, null); }
                catch (Exception) { }
            }
            if (!_impShow) return;

            var reset = api.reset;
            var apply = api.apply;
            bool modified = _impModified;

            GUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!modified || reset == null))
                    if (GUILayout.Button("Revert", GUILayout.Width(60)))
                    {
                        InvokeSafe(reset, ed);
                        GUI.FocusControl(null);
                        _assetDirty = true;
                        RequestSync();
                        GUIUtility.ExitGUI();
                    }
                using (new EditorGUI.DisabledScope(!modified))
                    if (GUILayout.Button("Apply", GUILayout.Width(60)))
                    {
                        InvokeSafe(apply, ed);
                        if (apply.Name == "Apply") // không có ApplyAndImport => tự reimport
                            foreach (var tg in ed.targets)
                                if (tg is AssetImporter imp) imp.SaveAndReimport();
                        GUI.FocusControl(null);
                        _assetDirty = true;
                        RequestSync();
                        GUIUtility.ExitGUI();
                    }
                GUILayout.Space(4);
            }
        }

        void RebuildAssetHeader()
        {
            string title = string.IsNullOrEmpty(_asset.name) ? _asset.GetType().Name : _asset.name;
            if (_assets.Length > 1) title += "  (" + _assets.Length + " objects)";
            string path = AssetDatabase.GetAssetPath(_asset);
            string tip = _asset.GetType().Name + (string.IsNullOrEmpty(path) ? "" : "\n" + path); // tooltip: type + đường dẫn
            _hdrTitleGC = new GUIContent(" " + title, AssetPreview.GetMiniThumbnail(_asset), tip);
            _hdrFor = _asset;
            _hdrCount = _assets.Length;
            _hdrDirty = false;
        }

        void DrawAssetHeader()
        {
            // GetAssetPath / GetMiniThumbnail / ghép chuỗi chỉ làm khi bind lại, đổi asset hoặc projectChanged (xem OnProjectChanged).
            if (_hdrTitleGC == null
                || (Event.current.type == EventType.Layout && (_hdrDirty || _hdrFor != _asset || _hdrCount != _assets.Length)))
                RebuildAssetHeader();

            using (new EditorGUILayout.HorizontalScope())
            {
                var oldIconSize = EditorGUIUtility.GetIconSize();
                EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                GUILayout.Space(4);
                GUILayout.Label(_hdrTitleGC, EditorStyles.boldLabel, GUILayout.Height(20));
                EditorGUIUtility.SetIconSize(oldIconSize);

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(40)))
                    EditorGUIUtility.PingObject(_asset);
                if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    AssetDatabase.OpenAsset(_asset);
                    GUIUtility.ExitGUI();
                }
                GUILayout.Space(4);
            }

            GUILayout.Space(2);
            var line = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, new Color(0, 0, 0, 0.4f));
        }

        // Giống Inspector gốc: chuột phải tiêu đề Preview = tách Preview ra cửa sổ Preview gốc của Unity, khung Preview trong cửa sổ này ẩn đi.
        // Mở ở delayCall (ngoài vòng OnGUI) cho an toàn. Capture bản sao dữ liệu, không capture editor (editor có thể bị dựng lại).
        internal UEditor ImporterEditorForSync { get { return _importerEditor; } } // CustomsNativePreview đọc clip đang chọn từ đây

        void DetachPreview()
        {
            if (_assets == null || _assets.Length == 0) return;
            var assets = (UObject[])_assets.Clone();
            var p = position;
            var rect = new Rect(p.x + 40, p.y + 40, 420, 420);
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                if (CustomsNativePreview.TryOpen(this, assets, rect)) Repaint();
            };
        }

        // Chiều cao khung Preview do UI Toolkit quyết định (xem ApplyPreviewHeight + _previewSplit),
        // hàm này chỉ vẽ nội dung vào đúng vùng container được cấp => không còn vòng phản hồi layout gây giật.
        void DrawPreviewPanel(UEditor ed)
        {
            // Thanh tiêu đề Preview + nút cài đặt của preview (Play/Pause cho animation, 2D/3D...)
            Rect barRect;
            var barStyle = GUI.skin.FindStyle("preToolbar") ?? EditorStyles.toolbar;           // style của Inspector gốc (không có thì dùng toolbar thường)
            var titleStyle = GUI.skin.FindStyle("ToolbarBoldLabel") ?? EditorStyles.boldLabel;
            using (var hs = new EditorGUILayout.HorizontalScope(barStyle, GUILayout.Height(21f)))
            {
                barRect = hs.rect;
                bool showNow = GUILayout.Toggle(_showPreview, GUIContent.none, EditorStyles.foldout, GUILayout.Width(14), GUILayout.Height(18));
                if (showNow != _showPreview)
                {
                    _showPreview = showNow;
                    if (_previewIm != null) _previewIm.schedule.Execute(ApplyPreviewHeight); // đổi style NGOÀI vòng IMGUI
                }
                var title = ed.GetPreviewTitle();
                string titleText = title != null && !string.IsNullOrEmpty(title.text) ? title.text : "Preview";
                if (DebugPreviewFps)
                {
                    if (Event.current.type == EventType.Repaint)
                    {
                        _fpsCount++;
                        double tNow = EditorApplication.timeSinceStartup;
                        if (tNow - _fpsT >= 1.0) { _fps = (float)(_fpsCount / (tNow - _fpsT)); _fpsCount = 0; _fpsT = tNow; }
                    }
                    titleText += "  [" + _fps.ToString("0") + " fps]" + _dbgText;
                }
                GUILayout.Label(new GUIContent(titleText, "Chuột phải: tách ra cửa sổ Preview của Unity"), titleStyle, GUILayout.Height(18));
                GUILayout.FlexibleSpace();
                if (_showPreview) ed.OnPreviewSettings();
            }

            // Chuột phải vào thanh tiêu đề => tách Preview Window gốc. Xử lý SAU các control: nút / toggle của preview
            // (OnPreviewSettings) tự nuốt MouseDown của chúng nên chuột phải lên nút đó không tách.
            var pev = Event.current;
            if (pev.type == EventType.MouseDown && pev.button == 1 && barRect.Contains(pev.mousePosition))
            {
                pev.Use();
                DetachPreview();
            }

            if (!_showPreview) return;
            var pr = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)); // lấp đầy phần còn lại của container
            var bg = GUI.skin.FindStyle("preBackground");
            if (bg != null && Event.current.type == EventType.Repaint) bg.Draw(pr, false, false, false, false); // nền như Inspector gốc
            ed.DrawPreview(pr);

            if (Event.current.type == EventType.Repaint)
            {
                _previewConstant = CustomsNativePreview.WantsConstantRepaint(ed);
                if (_previewConstant) RepaintForPreview(ed); // animation đang chạy: vẽ lại liên tục RIÊNG khung Preview, không đụng các panel khác
            }
        }

        // Editor của OBJECT (BlendTree, Transition, State...) có phần Inspector phụ thuộc thời gian preview (playhead timeline, v.v.)
        // nên phải vẽ lại cả cửa sổ như Inspector gốc. Editor của IMPORTER (Model) thì thân Import Settings nặng và không phụ thuộc => chỉ vẽ Preview.
        void RepaintForPreview(UEditor pe)
        {
            if (_importerEditor == null && ReferenceEquals(pe, _objectEditor)) Repaint();
            else RepaintPreviewOnly();
        }

        // Cần CẢ HAI: base.Repaint() xin host view vẽ khung hình kế tiếp (không có thì UI Toolkit chỉ vẽ lại khi có sự kiện khác,
        // ~10 lần/giây => animation giật), và MarkDirtyRepaint() để OnGUI của chính IMGUIContainer này chạy lại.
        void RepaintPreviewOnly()
        {
            base.Repaint();
            if (_previewIm != null) _previewIm.MarkDirtyRepaint();
        }

        // Đặt chiều cao / hiển thị của khung Preview + thanh kéo. Gọi ngoài vòng IMGUI (kéo chuột, đổi asset, đổi cửa sổ).
        void ApplyPreviewHeight()
        {
            if (_previewIm == null || _previewSplit == null) return;
            bool has = _hasPreviewEd && !CustomsNativePreview.HasFor(this); // đã tách Preview gốc => ẩn khung này
            float h = Mathf.Clamp(_previewHeight, 80f, Mathf.Max(100f, position.height - 160f));
            _previewIm.style.display = has ? UIE.DisplayStyle.Flex : UIE.DisplayStyle.None;
            _previewIm.style.height = _showPreview ? h + PreviewBarH : PreviewBarH;
            _previewSplit.style.display = (has && _showPreview) ? UIE.DisplayStyle.Flex : UIE.DisplayStyle.None;
        }

        // Thanh kéo đổi chiều cao Preview: pointer capture + toạ độ tuyệt đối (không dùng delta) nên không bị giật / trễ frame.
        UIE.VisualElement BuildPreviewSplitter()
        {
            var sp = new UIE.VisualElement();
            sp.style.height = 5;
            sp.style.flexShrink = 0;
            sp.style.display = UIE.DisplayStyle.None;
            var idle = new Color(0f, 0f, 0f, 0.35f);
            bool dragging = false;
            sp.style.backgroundColor = idle;
            sp.RegisterCallback<UIE.PointerEnterEvent>(e => sp.style.backgroundColor = Accent);
            sp.RegisterCallback<UIE.PointerLeaveEvent>(e => { if (!dragging) sp.style.backgroundColor = idle; });
            sp.RegisterCallback<UIE.PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                _splitDragStartY = e.position.y;
                _splitDragStartH = Mathf.Clamp(_previewHeight, 80f, Mathf.Max(100f, position.height - 160f));
                dragging = true;
                UIE.PointerCaptureHelper.CapturePointer(sp, e.pointerId);
                e.StopPropagation();
            });
            sp.RegisterCallback<UIE.PointerMoveEvent>(e =>
            {
                if (!UIE.PointerCaptureHelper.HasPointerCapture(sp, e.pointerId)) return;
                float max = Mathf.Max(100f, position.height - 160f);
                _previewHeight = Mathf.Clamp(_splitDragStartH - (e.position.y - _splitDragStartY), 80f, max);
                ApplyPreviewHeight();
                e.StopPropagation();
            });
            sp.RegisterCallback<UIE.PointerUpEvent>(e =>
            {
                if (!UIE.PointerCaptureHelper.HasPointerCapture(sp, e.pointerId)) return;
                dragging = false;
                UIE.PointerCaptureHelper.ReleasePointer(sp, e.pointerId);
                sp.style.backgroundColor = idle;
                EditorPrefs.SetFloat(PreviewHPrefKey, _previewHeight);
                e.StopPropagation();
            });
            return sp;
        }

        // Lưu các tab đang mở của object hiện tại (chỉ khi đang chọn đúng 1 object, vì khi chọn nhiều
        // object danh sách tab đã bị lọc theo component chung nên không phản ánh riêng object nào).
    }
}
#endif
