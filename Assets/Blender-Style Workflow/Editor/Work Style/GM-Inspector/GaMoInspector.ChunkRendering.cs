// GaMoInspector.ChunkRendering.cs  -  part of CustomsEditor (GaMo Inspector)
// Ha tang ve IMGUI theo 'chunk', dong bo cay UI voi trang thai
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
        // ---------- Chunk IMGUI ----------
        // kind: 0 = luôn chạy (vùng trên), 1 = chỉ khi đang ở chế độ GameObject, 2 = chỉ khi đang ở chế độ Asset
        // hover: vẽ lại riêng chunk này khi rê chuột (nút có highlight). cull: chỉ bật cho chunk nặng (editor, material).
        // key: khóa nhớ chiều cao đã đo (giữ qua lần dựng lại cây) để chunk bị culling có chỗ giữ đúng cỡ.
        UIE.IMGUIContainer GoChunk(Action draw, bool hover = false, bool cull = false, object key = null) => MakeBodyChunk(draw, 1, hover, cull, key);
        UIE.IMGUIContainer AssetChunk(Action draw, bool hover = false) => MakeBodyChunk(draw, 2, hover);

        UIE.IMGUIContainer MakeBodyChunk(Action draw, int kind, bool hover = false, bool cull = false, object key = null)
        {
            ChunkCull cs = null;
            if (cull)
            {
                cs = new ChunkCull { key = key };
                if (key != null && _chunkH.TryGetValue(key, out var h0)) cs.h = h0;
            }
            var c = new UIE.IMGUIContainer(() => RunChunk(draw, kind, cs));
            if (cs != null) { cs.c = c; _cullList.Add(cs); }
            if (hover) AddHoverRepaint(c);
            _bodyChunks.Add(c);
            return c;
        }

        void RunChunk(Action draw, int kind, ChunkCull cs = null)
        {
            if (cs != null && cs.culled) return; // chunk bị culling: bỏ qua TRƯỚC ChunkBegin (khỏi reset GUI state / xóa cache vô ích)
            if (!ChunkBegin()) return;
            if ((kind == 1 && (_asset != null || _go == null)) || (kind == 2 && _asset == null && _pfPrevEditor == null)) // Preview: Asset, hoặc prefab có model ở chế độ GameObject
            {
                RequestSync(); // trạng thái đã đổi (object bị xóa, đổi sang asset...): dựng lại cây
                return;
            }
            if (cs == null) { draw(); return; }

            // Chunk bị culling: không gọi draw (kể cả Layout), container giữ nguyên chiều cao đã đo.
            // Trạng thái culled chỉ đổi NGOÀI vòng IMGUI (EvaluateCulling) nên Layout và Repaint của một frame luôn khớp nhau.
            if (cs.culled) return;
            var type = Event.current.type;
            draw();
            if (type == EventType.Repaint) MeasureChunk(cs);
        }

        // Phần đầu OnGUI cũ, chạy ở đầu mỗi chunk
        bool ChunkBegin()
        {
            var ev = Event.current;

            // Chặn lệnh Paste khi clipboard không phải đường dẫn asset (tránh lỗi Invalid AssetDatabase path)
            if ((ev.type == EventType.ValidateCommand || ev.type == EventType.ExecuteCommand)
                && ev.commandName == "Paste" && !EditorGUIUtility.editingTextField)
            {
                var cb = EditorGUIUtility.systemCopyBuffer ?? "";
                if (!cb.StartsWith("Assets/") && !cb.StartsWith("Packages/"))
                    return false;
            }

            // Màu / icon size là trạng thái toàn cục dùng chung giữa các IMGUIContainer: đặt lại về mặc định ở đầu mỗi chunk
            // để một chunk lỡ rò trạng thái (ExitGUI, exception...) không làm hỏng các chunk khác.
            GUI.color = Color.white;
            GUI.contentColor = Color.white;
            GUI.backgroundColor = Color.white;
            EditorGUIUtility.SetIconSize(Vector2.zero);
            EditorGUI.showMixedValue = false;
            ResetFoldoutHeaderGroup();

            if (ev.type == EventType.Layout || !_styleInit) RefreshStylesIfNeeded();

            if (ev.type == EventType.Layout)
            {
                bool pruned = _gos.RemoveAll(g => g == null) > 0;
                if (_go == null && _gos.Count > 0) _go = _gos[0];
                _matShader.Clear();
                if (CachesStale(ref _layoutVer, ref _layoutAt) || pruned)
                {
                    _targetCache.Clear();
                    _validCache = null;
                    _compsCache = null;
                }
            }
            return true;
        }

        // EditorGUI đếm số FoldoutHeaderGroup đang mở bằng một biến static; Begin khi biến > 0 => báo
        // "You can't nest Foldout Headers". Inspector gốc reset nó (EditorGUI.ClearStacks) mỗi lần vẽ, IMGUIContainer thì không:
        // chỉ cần 1 lần Begin mà chưa tới End (ExitGUI / exception, vd khi bấm tab Settings của Terrain) là kẹt vĩnh viễn.
        static readonly FieldInfo _foldoutHeaderActive =
            typeof(EditorGUI).GetField("s_FoldoutHeaderGroupActive", BindingFlags.Static | BindingFlags.NonPublic);

        static void ResetFoldoutHeaderGroup()
        {
            if (_foldoutHeaderActive == null) return;
            if (_foldoutHeaderActive.FieldType == typeof(int)) _foldoutHeaderActive.SetValue(null, 0);
            else if (_foldoutHeaderActive.FieldType == typeof(bool)) _foldoutHeaderActive.SetValue(null, false);
        }

        void DrawTopChunk()
        {
            DrawTopBar();

            if (_asset != null)
            {
                DrawAssetHeader();
                return;
            }
            if (_go == null)
            {
                EditorGUILayout.HelpBox("Chọn một GameObject trong Hierarchy hoặc một asset trong Project.", MessageType.Info);
                return;
            }
            DrawHeader();
            DrawPrefabBar();
        }

        // ---------- Đồng bộ cây UI với trạng thái ----------
        // Gọi ngoài vòng IMGUI (không đụng cây UI khi đang trong OnGUI của chính nó).
        void RequestSync()
        {
            if (_uiRoot == null || _syncQueued) return;
            _syncQueued = true;
            _uiRoot.schedule.Execute(() => { _syncQueued = false; SyncUI(); });
        }

        void SyncUI()
        {
            if (_uiRoot == null || _scrollV == null || _reloading) return;

            bool prunedSync = _gos.RemoveAll(g => g == null) > 0;
            if (CachesStale(ref _syncVer, ref _syncAt) || prunedSync || _compsCache == null)
            {
                _compsCache = null;
                _targetCache.Clear();
                _validCache = null;
            }
            if (_go == null && _gos.Count > 0) _go = _gos[0];

            var sig = _uiSigNew;
            sig.Clear();
            _models.Clear();
            UEditor previewEd = null;

            // Asset bị xóa => dọn editor; editor bị hủy (vd sau Apply/reimport, recompile) => dựng lại.
            if (_asset == null && (_objectEditor != null || _importerEditor != null || _expectImporter || _expectObject)) ClearAssetState();
            else if (_asset != null && (_assetDirty || AssetEditorsStale())) { _assetDirty = false; BuildAssetEditors(); }

            if (_asset != null)
            {
                EnsureMotionPreview();
                previewEd = GetPreviewEditor();
                sig.Add("asset"); sig.Add(_asset); sig.Add(_importerEditor); sig.Add(_objectEditor);
                sig.Add(_drawObjectBody); sig.Add(_assetImmutable); sig.Add(_assetNotEditable);
            }
            else if (_go == null) sig.Add("none");
            else
            {
                sig.Add("go"); sig.Add(_go);
                EnsurePrefabPreview();
                previewEd = GetPreviewEditor();

                // Danh sách tab đang hiện (chọn nhiều object: chỉ component có ở TẤT CẢ object) + dọn tab đã xóa/ẩn
                var all = GetComps();
                bool multiSel = _gos.Count > 1;
                _shownTmp.Clear();
                var shown = _shownTmp;
                foreach (var c0 in all)
                {
                    if (c0 == null) { if (!multiSel) shown.Add(null); continue; }
                    if (multiSel && GetTargets(c0) == null) continue;
                    shown.Add(c0);
                }
                PruneSelected(shown);

                CleanupEditors();
                _matUsed.Clear(); // dùng làm tập "material đã được panel phía trên nhận"

                foreach (var c in shown)
                {
                    if (c == null || !_selected.Contains(c)) continue;
                    var targets = GetTargets(c);
                    if (targets == null) continue;

                    var m = new PanelModel { c = c, targets = targets, collapsed = _collapsed.Contains(c), debug = _debug.Contains(c) };
                    sig.Add(c); sig.Add(m.collapsed); sig.Add(m.debug); sig.Add(targets.Length);

                    if (!m.collapsed)
                    {
                        m.ed = EnsureEditor(c, targets);
                        sig.Add(m.ed);

                        // Renderer: Material nằm dưới panel. Material dùng chung giữa nhiều Renderer chỉ vẽ MỘT lần
                        // (ở panel đầu tiên), các panel sau chỉ ghi chú => MaterialEditor không bị vẽ trùng.
                        if (c is Renderer rend && targets.Length == 1)
                        {
                            m.mats = new List<Material>();
                            m.dupes = new List<Material>();
                            rend.GetSharedMaterials(_tmpMats);
                            foreach (var mat in _tmpMats)
                            {
                                if (mat == null || m.mats.Contains(mat) || m.dupes.Contains(mat)) continue;
                                if (_matUsed.Add(mat)) m.mats.Add(mat); else m.dupes.Add(mat);
                            }
                            foreach (var mat in m.mats) sig.Add(mat);
                            sig.Add("|");
                            foreach (var mat in m.dupes) sig.Add(mat);
                        }
                    }
                    _models.Add(m);
                }
                CleanupMaterialEditors();
            }

            sig.Add(previewEd);

            bool same = _uiSig.Count == sig.Count;
            for (int i = 0; same && i < sig.Count; i++)
                if (!object.Equals(_uiSig[i], sig[i])) same = false;

            _scrollV.style.display = UIE.DisplayStyle.Flex; // ClearEditors có thể đã ẩn tạm
            _hasPreviewEd = previewEd != null;
            ApplyPreviewHeight();
            // Chữ ký khớp nhưng thân rỗng (cây UI bị dọn / dựng dở dang) => coi như chưa dựng. Trước đây trường hợp này kẹt vĩnh viễn tới khi đóng mở cửa sổ.
            if (same && _go != null && _asset == null && _scrollV.contentContainer.childCount == 0) same = false;
            if (same) return;

            var tmp = _uiSig; _uiSig = _uiSigNew; _uiSigNew = tmp;
            _viewedVer = -1; // cấu trúc hiển thị đổi (vd gán material mới) => lập lại tập id đang xem ở event kế
            try
            {
                RebuildBody();
                _syncFailed = false;
            }
            catch (Exception ex)
            {
                // Lỗi giữa chừng RebuildBody: chữ ký đã ghi nhưng thân đã bị xóa => nếu không xử lý, các nhịp sau thấy "không đổi" và thân trống mãi.
                _uiSig.Clear(); // ép dựng lại ở nhịp kế tiếp
                if (!_syncFailed) { _syncFailed = true; Debug.LogException(ex); }
                var errCc = _scrollV.contentContainer;
                errCc.Clear();
                errCc.Add(new UIE.HelpBox("GM-Inspector dựng giao diện bị lỗi (xem Console): " + ex.Message, UIE.HelpBoxMessageType.Error));
            }
            Repaint();
        }

        void RebuildBody()
        {
            DisposeDebugSOs();
            _bodyChunks.Clear();
            _cullList.Clear();
            _barChunk = null;
            var cc = _scrollV.contentContainer;
            cc.Clear();

            if (_asset != null) BuildAssetBody(cc);
            else if (_go != null) BuildGoBody(cc);
        }

        UEditor EnsureEditor(Component c, Component[] targets)
        {
            if (_reloading) return null;
            if (!_editors.TryGetValue(c, out var ed) || ed == null
                || !_editorTargets.TryGetValue(c, out var oldTargets) || !SameTargets(oldTargets, targets))
            {
                if (ed != null) DestroyImmediate(ed);
                ed = targets.Length > 1 ? UEditor.CreateEditor((UObject[])targets) : UEditor.CreateEditor(c);
                _editors[c] = ed;
                _editorTargets[c] = targets;
            }
            return ed;
        }

        static UIE.VisualElement TryCreateInspectorElement(UEditor ed)
        {
            try { return new UEUI.InspectorElement(ed.serializedObject); }
            catch (Exception) { return null; }
        }
    }
}
#endif
