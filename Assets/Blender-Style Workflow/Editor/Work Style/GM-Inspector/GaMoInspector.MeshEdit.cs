// GaMoInspector.MeshEdit.cs  -  part of CustomsEditor (GaMo Inspector)
// Panel "Edit Mesh" cho Mesh asset (chon Mesh trong Project: sub-asset cua FBX hoac file .asset).
// Cach hoat dong: nhan ban sao Mesh trong bo nho -> thao tac tren ban sao (co Undo / Reset) -> Save As .asset moi hoac Overwrite (.asset).
// Thao tac: Recalculate Normals / Tangents, Flip Normals, Flip Faces, dat Pivot (Center / Bottom / Top / dich tuy y), Bake Rotation + Scale.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UIE = UnityEngine.UIElements;
using UObject = UnityEngine.Object;

namespace GaMo.BlenderEditorTools
{
    public partial class CustomsEditor
    {
        const string MeOpenKey = "GaMo.CustomsEditor.MeshEditOpen";
        const int MeMaxHist = 5;

        bool _meOpen, _meOpenInit;
        [NonSerialized] Mesh _meFor;          // mesh gốc đang chỉnh
        [NonSerialized] Mesh _meWork;         // bản sao đang sửa
        readonly List<Mesh> _meHist = new List<Mesh>();
        [NonSerialized] bool _meHooked;
        int _meMode;                          // 0 = chỉ Save As, 1 = Mesh trong Model (FBX), 2 = file .asset
        string _mePath = "";
        int _meOps;
        bool _meStatsDirty = true;
        string _meStats = "";
        Vector3 _meOffset = Vector3.zero, _meRot = Vector3.zero, _meScale = Vector3.one;
        Vector3 _meLiveApplied;                                   // phần "Dịch pivot" đã áp vào mesh làm việc (chỉnh trực tiếp, không cần nút)
        Matrix4x4 _meXform = Matrix4x4.identity;                  // tổng phép biến đổi đã bake vào vertex (mesh-local cũ -> mới: pivot, rotate, scale): dùng để bù transform node khi xuất FBX
        readonly List<Matrix4x4> _meXformHist = new List<Matrix4x4>(); // song song với _meHist (phục vụ Undo)
        [NonSerialized] static Matrix4x4 s_meXDelta = Matrix4x4.identity; // MeShift / MeTransform nhân dồn vào đây trong lúc MeOp chạy

        // =====================================================
        //  Gắn vào thân Asset (gọi từ BuildAssetBody)
        // =====================================================
        void AddMeshEditSection(UIE.VisualElement cc)
        {
            if (!(_asset is Mesh) || _assets.Length != 1) return;
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_asset))) return;

            var sec = new UIE.VisualElement();
            sec.style.marginBottom = 8;
            sec.Add(AssetChunk(DrawMeshEditChunk, true));
            cc.Add(sec);
        }

        void MeCleanup()
        {
            FbxAutoStage(); // giữ lại mesh đã sửa cho lần xuất FBX (xem FbxExport.cs)
            if (_meWork != null) UObject.DestroyImmediate(_meWork);
            _meWork = null;
            foreach (var h in _meHist)
                if (h != null) UObject.DestroyImmediate(h);
            _meHist.Clear();
            _meXformHist.Clear();
            MePreviewCleanup();
            if (_meHooked)
            {
                _meHooked = false;
                AssemblyReloadEvents.beforeAssemblyReload -= MeCleanup;
            }
        }

        void MeReset(Mesh m)
        {
            MeCleanup();
            _meFor = m;
            _meVer++;
            _meOps = 0;
            _meXform = Matrix4x4.identity;
            _meStatsDirty = true;
            _meOffset = Vector3.zero;
            _meLiveApplied = Vector3.zero;
            _meRot = Vector3.zero;
            _meScale = Vector3.one;
            _mePath = m != null ? AssetDatabase.GetAssetPath(m) : "";
            if (m != null) _meViewRadius = m.bounds.extents.magnitude; // cỡ khung nhìn tối thiểu của Preview
            _meMode = 0;
            if (string.IsNullOrEmpty(_mePath)) return;
            if (AssetImporter.GetAtPath(_mePath) is ModelImporter) _meMode = 1;
            else if (AssetDatabase.IsMainAsset(m) && _mePath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) _meMode = 2;
        }

        Mesh MeCleanCopy()
        {
            var c = UObject.Instantiate(_meWork);
            c.hideFlags = HideFlags.None;
            c.name = _meFor != null ? _meFor.name : "Mesh";
            return c;
        }

        // =====================================================
        //  Vẽ panel
        // =====================================================
        void DrawMeshEditChunk()
        {
            var mesh = _asset as Mesh;
            if (mesh == null) return;
            var ev = Event.current;

            if (ev.type == EventType.Layout && _meFor != mesh) MeReset(mesh);
            if (_meFor != mesh) return;

            if (!_meOpenInit)
            {
                _meOpenInit = true;
                _meOpen = EditorPrefs.GetBool(MeOpenKey, false);
            }

            bool open;
            var oldIcon = EditorGUIUtility.GetIconSize();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                open = GUILayout.Toggle(_meOpen, GUIContent.none, EditorStyles.foldout, GUILayout.Width(14), GUILayout.Height(18));
                EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                GUILayout.Label(new GUIContent(_meOps > 0 ? " Edit Mesh *" : " Edit Mesh", EditorGUIUtility.IconContent("Mesh Icon").image),
                    EditorStyles.boldLabel, GUILayout.Height(18));
                EditorGUIUtility.SetIconSize(oldIcon);
                GUILayout.FlexibleSpace();
            }
            if (open != _meOpen)
            {
                _meOpen = open;
                EditorPrefs.SetBool(MeOpenKey, open);
                if (!open) MeCleanup();
                Repaint();
                GUIUtility.ExitGUI();
            }
            if (!_meOpen) return;

            // Bản sao chỉ tạo ở event Layout để Layout / Repaint luôn cùng số control
            if (_meWork == null)
            {
                if (ev.type != EventType.Layout) return;
                FbxStaged stg;
                if (_meMode == 1 && FbxTryGetStaged(_mePath, _meFor, out stg))
                {   // mesh này đã sửa trước đó (đã nằm trong danh sách xuất FBX): mở lại từ bản đã sửa, không mất chỉnh sửa
                    _meWork = UObject.Instantiate(stg.mesh);
                    _meOps = Mathf.Max(_meOps, 1);
                    _meXform = stg.xform;
                    _meVer++;
                }
                else _meWork = UObject.Instantiate(_meFor);
                _meWork.hideFlags = HideFlags.HideAndDontSave;
                _meWork.name = _meFor.name;
                _meStatsDirty = true;
                if (!_meHooked)
                {
                    _meHooked = true;
                    AssemblyReloadEvents.beforeAssemblyReload += MeCleanup;
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins))
                MeMainGUI();
        }

        static bool MeBtn(string label, string tip, bool enabled)
        {
            using (new EditorGUI.DisabledScope(!enabled))
                return GUILayout.Button(new GUIContent(label, tip), EditorStyles.miniButton);
        }

        void MeDone()
        {
            RepaintPreviewOnly(); // khung Preview là IMGUIContainer riêng: cần MarkDirtyRepaint mới vẽ lại
            Repaint();
            GUIUtility.ExitGUI();
        }

        void MeMainGUI()
        {
            var m = _meWork;
            if (m == null) return;
            var ev = Event.current;

            if (!m.isReadable)
            {
                EditorGUILayout.HelpBox("Mesh chưa bật Read/Write nên không đọc được vertex để sửa.", MessageType.Warning);
                if (GUILayout.Button("Bật Read/Write & Reimport", EditorStyles.miniButton))
                {
                    MeQueueEnableReadable(_meFor);
                    MeDone();
                }
                return;
            }

            if (ev.type == EventType.Layout && _meStatsDirty)
            {
                _meStats = MeBuildStats(m);
                _meStatsDirty = false;
            }

            float oldLw = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 96f;
            try
            {
                GUILayout.Label(_meStats, EditorStyles.wordWrappedMiniLabel);

                bool skinned = m.bindposes.Length > 0 || m.blendShapeCount > 0;
                if (skinned)
                    EditorGUILayout.HelpBox("Mesh có skin / blendshape: chỉ cho Recalculate và dịch Pivot (bindposes được bù tự động). Scale, Rotation, Flip bị khóa vì làm hỏng skin.", MessageType.Info);

                GUILayout.Space(4);
                GUILayout.Label("Normals", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (MeBtn("Recalc Normals", "Tính lại normals (smooth) theo hình học hiện tại", true))
                    { MeOp(x => x.RecalculateNormals()); MeDone(); }
                    if (MeBtn("Recalc Tangents", "Tính lại tangents (cần UV + normals)", m.uv.Length == m.vertexCount && m.vertexCount > 0))
                    { MeOp(x => { x.RecalculateTangents(); }); MeDone(); }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (MeBtn("Flip Normals", "Đảo hướng normals, giữ nguyên thứ tự tam giác", !skinned && m.normals.Length > 0))
                    { MeOp(MeFlipNormals); MeDone(); }
                    if (MeBtn("Flip Faces", "Đảo thứ tự tam giác (mặt trước <-> mặt sau) và đảo normals", !skinned))
                    { MeOp(x => { MeFlipWinding(x); MeFlipNormals(x); }); MeDone(); }
                }

                GUILayout.Space(4);
                GUILayout.Label("Pivot", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (MeBtn("Center", "Dời pivot về tâm bounds", true)) { MeOp(x => MePivot(x, 0)); MeDone(); }
                    if (MeBtn("Bottom", "Dời pivot xuống tâm mặt đáy (hợp cho nhân vật / props đặt trên sàn)", true)) { MeOp(x => MePivot(x, 1)); MeDone(); }
                    if (MeBtn("Top", "Dời pivot lên tâm mặt đỉnh", true)) { MeOp(x => MePivot(x, 2)); MeDone(); }
                }
                EditorGUI.BeginChangeCheck();
                _meOffset = EditorGUILayout.Vector3Field(new GUIContent("Dịch pivot", "Pivot mới = pivot cũ + giá trị này (đơn vị local của mesh). Preview vẽ lại ngay khi đổi, không cần bấm nút."), _meOffset);
                if (EditorGUI.EndChangeCheck()) MeLivePivot();

                GUILayout.Space(4);
                GUILayout.Label("Bake Transform", EditorStyles.miniBoldLabel);
                _meRot = EditorGUILayout.Vector3Field(new GUIContent("Rotation"), _meRot);
                _meScale = EditorGUILayout.Vector3Field(new GUIContent("Scale"), _meScale);
                bool trsIdle = _meRot == Vector3.zero && _meScale == Vector3.one;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (MeBtn("X −90°", "Điền sẵn Rotation X = -90 (hay gặp khi mesh từ Blender bị nằm)", true)) _meRot = new Vector3(-90f, 0f, 0f);
                    GUILayout.FlexibleSpace();
                    if (MeBtn("Bake", "Áp Rotation + Scale vào vertex rồi đưa các ô về mặc định",
                            !skinned && !trsIdle && _meScale.x != 0f && _meScale.y != 0f && _meScale.z != 0f))
                    {
                        var mat = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(_meRot), _meScale);
                        MeOp(x => MeTransform(x, mat));
                        _meRot = Vector3.zero;
                        _meScale = Vector3.one;
                        MeDone();
                    }
                }

                GUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (MeBtn("Undo", "Hoàn tác thao tác mesh gần nhất", _meHist.Count > 0)) { MeUndo(); MeDone(); }
                    if (MeBtn("Reset", "Bỏ mọi chỉnh sửa, quay về mesh gốc", _meOps > 0)) { _meOps = 0; FbxAutoStage(); MeReset(_meFor); MeDone(); }
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(_meOps > 0 ? _meOps + " thao tác chưa lưu" : "Chưa chỉnh sửa", EditorStyles.miniLabel);
                }

                GUILayout.Space(4);
                bool canOver = _meMode == 2 && !_assetImmutable && !_mePath.StartsWith("Packages/", StringComparison.Ordinal);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (MeBtn("Save As…", "Lưu mesh đã sửa thành file .asset mới (mesh gốc giữ nguyên). Sau đó gán vào MeshFilter / MeshCollider.", true))
                        MeQueueSaveAs();
                    if (MeBtn("Overwrite", canOver ? "Ghi đè file .asset gốc (có xác nhận, Ctrl+Z được)" : "Chỉ ghi đè được mesh .asset nằm ngoài Packages", canOver && _meOps > 0))
                        MeQueueOverwrite();
                }
                if (_meMode == 1)
                {
                    GUILayout.Label("Mesh của FBX không ghi đè được: Save As ra .asset, hoặc 'Thêm vào FBX mới' rồi 'Xuất FBX mới'.", EditorStyles.miniLabel);
                    DrawFbxStageGUI();
                }
            }
            finally { EditorGUIUtility.labelWidth = oldLw; }
        }

        // =====================================================
        //  Thao tác trên bản sao
        // =====================================================
        void MeOp(Action<Mesh> op)
        {
            if (_meWork == null) return;
            _meOffset = Vector3.zero; _meLiveApplied = Vector3.zero; // thao tác khác chốt phần dịch pivot đang chỉnh
            MePushHist();
            s_meXDelta = Matrix4x4.identity;
            try { op(_meWork); }
            catch (Exception ex) { Debug.LogException(ex); }
            _meXform = s_meXDelta * _meXform;
            _meOps++;
            _meVer++;
            _meStatsDirty = true;
        }

        // Chụp mesh làm việc vào lịch sử Undo
        void MePushHist()
        {
            var snap = UObject.Instantiate(_meWork);
            snap.hideFlags = HideFlags.HideAndDontSave;
            snap.name = _meWork.name;
            _meHist.Add(snap);
            _meXformHist.Add(_meXform);
            while (_meHist.Count > MeMaxHist)
            {
                if (_meHist[0] != null) UObject.DestroyImmediate(_meHist[0]);
                _meHist.RemoveAt(0);
                _meXformHist.RemoveAt(0);
            }
        }

        // Ô "Dịch pivot" đổi giá trị => áp độ chênh vào mesh ngay (cả phiên chỉnh chỉ là MỘT thao tác Undo).
        // Đưa ô về 0 => hoàn tác thao tác đó luôn.
        void MeLivePivot()
        {
            if (_meWork == null) return;
            Vector3 delta = _meOffset - _meLiveApplied;
            if (delta == Vector3.zero) return;

            if (_meLiveApplied == Vector3.zero)
            {
                MePushHist();
                _meOps++;
            }
            s_meXDelta = Matrix4x4.identity;
            try { MeShift(_meWork, -delta); }
            catch (Exception ex) { Debug.LogException(ex); }
            _meXform = s_meXDelta * _meXform;
            _meLiveApplied = _meOffset;
            _meVer++;
            _meStatsDirty = true;

            if (_meOffset == Vector3.zero)
            {
                var keep = _meOffset;
                MeUndo();            // về đúng mesh trước phiên chỉnh, bớt 1 thao tác
                _meOffset = keep;
            }
            RepaintPreviewOnly();
        }

        void MeUndo()
        {
            if (_meHist.Count == 0) return;
            _meOffset = Vector3.zero; _meLiveApplied = Vector3.zero;
            var prev = _meHist[_meHist.Count - 1];
            _meHist.RemoveAt(_meHist.Count - 1);
            if (_meXformHist.Count > 0)
            {
                _meXform = _meXformHist[_meXformHist.Count - 1];
                _meXformHist.RemoveAt(_meXformHist.Count - 1);
            }
            if (_meWork != null) UObject.DestroyImmediate(_meWork);
            _meWork = prev;
            _meOps = Mathf.Max(0, _meOps - 1);
            _meVer++;
            _meStatsDirty = true;
        }

        static string MeBuildStats(Mesh m)
        {
            int tris = 0;
            for (int s = 0; s < m.subMeshCount; s++)
                if (m.GetTopology(s) == MeshTopology.Triangles) tris += (int)(m.GetIndexCount(s) / 3u);
            var b = m.bounds;
            var sb = new StringBuilder();
            sb.Append(m.vertexCount).Append(" verts · ").Append(tris).Append(" tris · ").Append(m.subMeshCount).Append(" submesh\n");
            sb.Append("Size ").Append(b.size.ToString("0.###")).Append("   Tâm lệch pivot ").Append(b.center.ToString("0.###")).Append('\n');
            sb.Append("Có: ");
            if (m.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal)) sb.Append("Normals ");
            if (m.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent)) sb.Append("Tangents ");
            if (m.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color)) sb.Append("Colors ");
            for (int i = 0; i < 8; i++)
                if (m.HasVertexAttribute((UnityEngine.Rendering.VertexAttribute)((int)UnityEngine.Rendering.VertexAttribute.TexCoord0 + i)))
                    sb.Append("UV").Append(i).Append(' ');
            if (m.blendShapeCount > 0) sb.Append("· ").Append(m.blendShapeCount).Append(" blendshape ");
            if (m.bindposes.Length > 0) sb.Append("· ").Append(m.bindposes.Length).Append(" bones ");
            return sb.ToString();
        }

        static void MeFlipNormals(Mesh m)
        {
            var n = m.normals;
            if (n.Length == 0) return;
            for (int i = 0; i < n.Length; i++) n[i] = -n[i];
            m.normals = n;
        }

        static void MeFlipWinding(Mesh m)
        {
            for (int s = 0; s < m.subMeshCount; s++)
            {
                if (m.GetTopology(s) != MeshTopology.Triangles) continue;
                var tr = m.GetTriangles(s);
                for (int i = 0; i + 2 < tr.Length; i += 3)
                {
                    int t = tr[i + 1];
                    tr[i + 1] = tr[i + 2];
                    tr[i + 2] = t;
                }
                m.SetTriangles(tr, s);
            }
        }

        // mode: 0 = tâm bounds, 1 = tâm đáy, 2 = tâm đỉnh
        static void MePivot(Mesh m, int mode)
        {
            m.RecalculateBounds();
            var b = m.bounds;
            Vector3 p = b.center;
            if (mode == 1) p = new Vector3(b.center.x, b.min.y, b.center.z);
            else if (mode == 2) p = new Vector3(b.center.x, b.max.y, b.center.z);
            MeShift(m, -p);
        }

        // Dịch toàn bộ vertex một đoạn off. Mesh có bone: bù bindposes để skin không lệch
        // (bindpose' = bindpose * Translate(-off) => bindpose' * (v + off) = bindpose * v). Blendshape delta không đổi khi chỉ translate.
        static void MeShift(Mesh m, Vector3 off)
        {
            s_meXDelta = Matrix4x4.Translate(off) * s_meXDelta; // vertex mới = vertex cũ + off (dùng để bù node khi xuất FBX)
            var v = m.vertices;
            for (int i = 0; i < v.Length; i++) v[i] += off;
            m.vertices = v;

            var bp = m.bindposes;
            if (bp.Length > 0)
            {
                var t = Matrix4x4.Translate(-off);
                for (int i = 0; i < bp.Length; i++) bp[i] = bp[i] * t;
                m.bindposes = bp;
            }
            m.RecalculateBounds();
        }

        static void MeTransform(Mesh m, Matrix4x4 t)
        {
            s_meXDelta = t * s_meXDelta; // vertex mới = t * vertex cũ (dùng để bù node khi xuất FBX)
            var v = m.vertices;
            var n = m.normals;
            var tg = m.tangents;
            var nm = t.inverse.transpose;
            float det = t.determinant;

            for (int i = 0; i < v.Length; i++) v[i] = t.MultiplyPoint3x4(v[i]);
            m.vertices = v;

            if (n.Length == v.Length)
            {
                for (int i = 0; i < n.Length; i++) n[i] = nm.MultiplyVector(n[i]).normalized;
                m.normals = n;
            }
            if (tg.Length == v.Length)
            {
                for (int i = 0; i < tg.Length; i++)
                {
                    var d = t.MultiplyVector(new Vector3(tg[i].x, tg[i].y, tg[i].z)).normalized;
                    tg[i] = new Vector4(d.x, d.y, d.z, det < 0f ? -tg[i].w : tg[i].w);
                }
                m.tangents = tg;
            }
            if (det < 0f) MeFlipWinding(m); // scale âm lật mặt: sửa lại thứ tự tam giác
            m.RecalculateBounds();
        }

        // =====================================================
        //  Lưu / bật Read-Write (chạy ở delayCall: ngoài vòng OnGUI)
        // =====================================================
        void MeQueueSaveAs()
        {
            string srcPath = _mePath;
            string baseName = _meFor != null ? _meFor.name : "Mesh";
            EditorApplication.delayCall += () =>
            {
                if (this == null || _meWork == null) return;
                string dir = Path.GetDirectoryName(srcPath);
                dir = string.IsNullOrEmpty(dir) ? "Assets" : dir.Replace('\\', '/');
                string p = EditorUtility.SaveFilePanelInProject("Save Mesh As", baseName + "_edit", "asset",
                    "Lưu mesh đã sửa thành file .asset", dir);
                if (string.IsNullOrEmpty(p)) return;

                try
                {
                    var copy = MeCleanCopy();
                    var exist = AssetDatabase.LoadAssetAtPath<Mesh>(p);
                    Mesh saved;
                    if (exist != null && exist != _meFor)
                    {
                        EditorUtility.CopySerialized(copy, exist); // giữ nguyên GUID file cũ
                        UObject.DestroyImmediate(copy);
                        EditorUtility.SetDirty(exist);
                        saved = exist;
                    }
                    else if (exist == null)
                    {
                        AssetDatabase.CreateAsset(copy, p);
                        saved = copy;
                    }
                    else
                    {
                        UObject.DestroyImmediate(copy);
                        Debug.LogWarning("[GaMo] Không thể ghi đè chính mesh nguồn bằng Save As, hãy dùng Overwrite.");
                        return;
                    }
                    AssetDatabase.SaveAssets();
                    EditorGUIUtility.PingObject(saved);
                    Debug.Log("[GaMo] Đã lưu mesh: " + p);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("Lưu mesh lỗi", ex.Message, "OK");
                }
                Repaint();
            };
        }

        void MeQueueOverwrite()
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null || _meWork == null || _meFor == null) return;
                var orig = _meFor;
                if (!EditorUtility.DisplayDialog("Ghi đè mesh gốc",
                        "Ghi đè mesh:\n" + _mePath + "\n\nCác object đang dùng mesh này sẽ đổi theo. Có thể Ctrl+Z.", "Ghi đè", "Hủy")) return;
                try
                {
                    var copy = MeCleanCopy();
                    Undo.RegisterCompleteObjectUndo(orig, "GaMo Edit Mesh");
                    EditorUtility.CopySerialized(copy, orig);
                    UObject.DestroyImmediate(copy);
                    EditorUtility.SetDirty(orig);
                    AssetDatabase.SaveAssets();
                    _meFor = null; // Layout kế tiếp dựng lại bản sao từ mesh vừa ghi
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("Ghi đè mesh lỗi", ex.Message, "OK");
                }
                Repaint();
            };
        }

        void MeQueueEnableReadable(Mesh mesh)
        {
            string path = AssetDatabase.GetAssetPath(mesh);
            EditorApplication.delayCall += () =>
            {
                if (this == null || mesh == null) return;
                try
                {
                    var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (imp != null)
                    {
                        imp.isReadable = true;
                        imp.SaveAndReimport();
                    }
                    else
                    {
                        var so = new SerializedObject(mesh);
                        var prop = so.FindProperty("m_IsReadable");
                        if (prop == null) { EditorUtility.DisplayDialog("Không bật được", "Không tìm thấy thuộc tính Read/Write của mesh.", "OK"); return; }
                        prop.boolValue = true;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(mesh);
                        AssetDatabase.SaveAssets();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    return;
                }

                _meFor = null;
                _assetDirty = true;
                RequestSync();
                EditorApplication.delayCall += () =>
                {
                    if (this == null) return;
                    if (_lock) { if (_lockedObj != null) BindToObject(_lockedObj); }
                    else BindSelection();
                };
            };
        }
    }
}
#endif