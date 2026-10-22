// GaMoInspector.FbxExport.cs  -  part of CustomsEditor (GaMo Inspector)
// Xuat FBX moi tu 1 file FBX co nhieu mesh, bang package Unity "FBX Exporter" (xem GaMoInspector.FbxPackage.cs).
// Quy trinh: chon tung Mesh trong FBX -> sua rieng (Edit Mesh) -> "Them vao FBX moi" -> lap lai cho cac mesh khac -> "Xuat FBX moi...".
// Mesh khong duoc them thi giu nguyen ban goc. Mesh phai bat Read/Write.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UIE = UnityEngine.UIElements;

namespace GaMo.BlenderEditorTools
{
    sealed class FbxStaged
    {
        public Mesh mesh;      // ban da sua (ban sao trong bo nho)
        public Matrix4x4 xform; // tong phep bien doi da bake vao vertex (pivot/rotate/scale), mesh-local cu -> moi: de bu transform node
    }

    public partial class CustomsEditor
    {
        // fbx path -> (mesh goc -> mesh da sua)
        static readonly Dictionary<string, Dictionary<Mesh, FbxStaged>> _fbxStage = new Dictionary<string, Dictionary<Mesh, FbxStaged>>();
        static bool _fbxHooked;

        static int FbxStagedCount(string path)
        {
            Dictionary<Mesh, FbxStaged> d;
            return !string.IsNullOrEmpty(path) && _fbxStage.TryGetValue(path, out d) ? d.Count : 0;
        }

        static bool FbxIsStaged(string path, Mesh src)
        {
            Dictionary<Mesh, FbxStaged> d;
            return src != null && !string.IsNullOrEmpty(path) && _fbxStage.TryGetValue(path, out d) && d.ContainsKey(src);
        }

        static void FbxClearAll()
        {
            foreach (var kv in _fbxStage)
                foreach (var st in kv.Value.Values)
                    if (st.mesh != null) UnityEngine.Object.DestroyImmediate(st.mesh);
            _fbxStage.Clear();
        }

        static void FbxUnstage(string path, Mesh src)
        {
            Dictionary<Mesh, FbxStaged> d;
            if (!_fbxStage.TryGetValue(path, out d)) return;
            FbxStaged st;
            if (d.TryGetValue(src, out st))
            {
                if (st.mesh != null) UnityEngine.Object.DestroyImmediate(st.mesh);
                d.Remove(src);
            }
        }

        void FbxStage()
        {
            if (_meWork == null || _meFor == null || string.IsNullOrEmpty(_mePath)) return;
            if (!_fbxHooked)
            {
                _fbxHooked = true;
                AssemblyReloadEvents.beforeAssemblyReload += FbxClearAll;
            }
            FbxUnstage(_mePath, _meFor);
            Dictionary<Mesh, FbxStaged> d;
            if (!_fbxStage.TryGetValue(_mePath, out d)) { d = new Dictionary<Mesh, FbxStaged>(); _fbxStage[_mePath] = d; }
            var copy = MeCleanCopy();
            copy.hideFlags = HideFlags.HideAndDontSave;
            d[_meFor] = new FbxStaged { mesh = copy, xform = _meXform };
        }

        void DrawFbxStageGUI()
        {
            GUILayout.Space(4);
            GUILayout.Label("Xuất FBX mới (nhiều mesh)", EditorStyles.miniBoldLabel);
            DrawFbxApplyScaleToggle();
            bool pkgOk = DrawFbxPkgNotice();
            int n = FbxStagedCount(_mePath);
            bool staged = FbxIsStaged(_mePath, _meFor);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (MeBtn(staged ? "Cập nhật vào FBX mới" : "Thêm vào FBX mới",
                        "Lưu bản mesh đang sửa này vào danh sách để xuất FBX mới. Chọn mesh khác của cùng FBX, sửa riêng rồi thêm tiếp.", _meOps > 0))
                { FbxStage(); MeDone(); }
                if (MeBtn("Bỏ khỏi FBX mới", "Mesh này sẽ giữ nguyên bản gốc khi xuất", staged))
                { FbxUnstage(_mePath, _meFor); MeDone(); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (MeBtn("Xuất FBX mới…", "Ghi cả file FBX: mesh đã thêm dùng bản sửa, mesh còn lại giữ nguyên", pkgOk && (n > 0 || _meOps > 0 || FbxApplyScale)))
                    FbxQueueExport(_mePath);
                GUILayout.FlexibleSpace();
                GUILayout.Label(n + " mesh đã sửa" + (staged ? " (gồm mesh này)" : ""), EditorStyles.miniLabel);
            }
        }

        // =====================================================
        //  Mục "Export FBX" khi chọn file model (FBX) trong Project: Apply Scale + xuất FBX mới
        // =====================================================
        const string FbxApplyScaleKey = "GaMo.CustomsEditor.FbxApplyScale";
        const string FbxOpenKey = "GaMo.CustomsEditor.FbxExportOpen";
        const string FbxTipApply =
            "Giống Blender Apply Scale: nướng scale của MỌI node (mesh con, armature...) vào vertex / vị trí bone rồi đặt scale về 1,1,1.\n" +
            "Kích thước và vị trí trong world giữ nguyên. Mesh skin: bindpose được tính lại theo bone scale 1.\n" +
            "Scale không đều (x, y, z khác nhau) mà node có xoay xen kẽ thì chỉ gần đúng.";
        static bool _fbxApplyScale, _fbxApplyScaleInit;
        bool _fbxOpen, _fbxOpenInit;
        GameObject _fbxScanFor;
        string _fbxScanInfo = "";
        int _fbxScanCount;

        static bool FbxApplyScale
        {
            get
            {
                if (!_fbxApplyScaleInit) { _fbxApplyScaleInit = true; _fbxApplyScale = EditorPrefs.GetBool(FbxApplyScaleKey, false); }
                return _fbxApplyScale;
            }
            set { _fbxApplyScale = value; _fbxApplyScaleInit = true; EditorPrefs.SetBool(FbxApplyScaleKey, value); }
        }

        void AddFbxSection(UIE.VisualElement cc)
        {
            if (!(_asset is GameObject go) || _assets.Length != 1 || !IsModelAsset(go)) return;
            _fbxScanFor = null; // cây UI vừa dựng lại (đổi asset / reimport): quét lại scale
            var sec = new UIE.VisualElement();
            sec.style.marginBottom = 8;
            sec.Add(AssetChunk(DrawFbxChunk, true));
            cc.Add(sec);
        }

        void FbxScan(GameObject go)
        {
            _fbxScanFor = go;
            _fbxScanCount = 0;
            var sb = new StringBuilder();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                var s = t.localScale;
                if (Mathf.Abs(s.x - 1f) < 1e-4f && Mathf.Abs(s.y - 1f) < 1e-4f && Mathf.Abs(s.z - 1f) < 1e-4f) continue;
                if (_fbxScanCount++ < 4) sb.Append("\n  • ").Append(t.name).Append(": ").Append(s.ToString("0.###"));
            }
            _fbxScanInfo = _fbxScanCount == 0
                ? "Mọi node đều có scale 1,1,1."
                : _fbxScanCount + " node có scale ≠ 1" + sb + (_fbxScanCount > 4 ? "\n  …" : "");
        }

        void DrawFbxApplyScaleToggle()
        {
            bool v = GUILayout.Toggle(FbxApplyScale, new GUIContent("Apply Scale (đưa scale mọi node về 1,1,1)", FbxTipApply));
            if (v != FbxApplyScale) FbxApplyScale = v;
        }

        void DrawFbxChunk()
        {
            var go = _asset as GameObject;
            if (go == null) return;
            var ev = Event.current;
            string path = AssetDatabase.GetAssetPath(go);
            if (ev.type == EventType.Layout && _fbxScanFor != go) FbxScan(go);

            if (!_fbxOpenInit) { _fbxOpenInit = true; _fbxOpen = EditorPrefs.GetBool(FbxOpenKey, true); }
            bool open;
            var oldIcon = EditorGUIUtility.GetIconSize();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                open = GUILayout.Toggle(_fbxOpen, GUIContent.none, EditorStyles.foldout, GUILayout.Width(14), GUILayout.Height(18));
                EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                GUILayout.Label(new GUIContent(" Export FBX", EditorGUIUtility.IconContent("Mesh Icon").image), EditorStyles.boldLabel, GUILayout.Height(18));
                EditorGUIUtility.SetIconSize(oldIcon);
                GUILayout.FlexibleSpace();
            }
            if (open != _fbxOpen)
            {
                _fbxOpen = open;
                EditorPrefs.SetBool(FbxOpenKey, open);
                Repaint();
            }
            if (!_fbxOpen) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins))
            {
                GUILayout.Space(2);
                GUILayout.Label(_fbxScanInfo, EditorStyles.wordWrappedMiniLabel);
                DrawFbxApplyScaleToggle();

                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                bool needRW = imp != null && !imp.isReadable;
                if (needRW)
                {
                    EditorGUILayout.HelpBox("Model chưa bật Read/Write nên không đọc được mesh để xuất.", MessageType.Warning);
                    if (GUILayout.Button("Bật Read/Write & Reimport", EditorStyles.miniButton))
                    {
                        FbxQueueEnableReadable(path);
                        GUIUtility.ExitGUI();
                    }
                }

                bool pkgOk = DrawFbxPkgNotice();
                int n = FbxStagedCount(path);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(needRW || !pkgOk))
                        if (GUILayout.Button(new GUIContent("Xuất FBX mới…", "Ghi file FBX mới (file gốc giữ nguyên). Mesh đã sửa trong Edit Mesh dùng bản sửa, mesh còn lại giữ nguyên."), GUILayout.Height(22)))
                            FbxQueueExport(path);
                    GUILayout.Label(n + " mesh đã sửa", EditorStyles.miniLabel, GUILayout.Width(80));
                }
                GUILayout.Label("Không xuất: animation, blendshape, texture.", EditorStyles.miniLabel);
            }
        }

        void FbxQueueEnableReadable(string path)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                try
                {
                    var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (imp != null) { imp.isReadable = true; imp.SaveAndReimport(); }
                }
                catch (Exception ex) { Debug.LogException(ex); return; }
                _fbxScanFor = null;
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

        // Mesh đang sửa trong Edit Mesh tự được đưa vào danh sách xuất (khi rời mesh / đóng panel / xuất). Không còn mmesh đã sửa thì gỡ khỏi danh sách.
        void FbxAutoStage()
        {
            if (_meWork == null || _meFor == null || _meMode != 1 || string.IsNullOrEmpty(_mePath)) return;
            if (_meOps > 0) FbxStage();
            else FbxUnstage(_mePath, _meFor);
        }

        static bool FbxTryGetStaged(string path, Mesh src, out FbxStaged st)
        {
            st = null;
            Dictionary<Mesh, FbxStaged> d;
            return src != null && !string.IsNullOrEmpty(path) && _fbxStage.TryGetValue(path, out d) && d.TryGetValue(src, out st) && st != null && st.mesh != null;
        }

        // FBX mới mặc định Animation Type của project (có thể là None => mất skin). Chép lại từ FBX gốc.
        static void FbxCopyRigSettings(string srcPath, string dstPath)
        {
            try
            {
                var si = AssetImporter.GetAtPath(srcPath) as ModelImporter;
                var di = AssetImporter.GetAtPath(dstPath) as ModelImporter;
                if (si == null || di == null) return;
                bool changed = false;
                if (di.animationType != si.animationType) { di.animationType = si.animationType; changed = true; }
                if (di.isReadable != si.isReadable) { di.isReadable = si.isReadable; changed = true; }
                if (changed) di.SaveAndReimport();
            }
            catch (Exception ex) { Debug.LogWarning("[GaMo FBX] Không chép được cài đặt Rig: " + ex.Message); }
        }

        static void FbxLogSkinCheck(GameObject srcRoot, string dstPath)
        {
            var nr = AssetDatabase.LoadAssetAtPath<GameObject>(dstPath);
            if (nr == null || srcRoot == null) return;
            int a = srcRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            int b = nr.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            string srcPath = AssetDatabase.GetAssetPath(srcRoot);
            var si = AssetImporter.GetAtPath(srcPath) as ModelImporter;
            var di = AssetImporter.GetAtPath(dstPath) as ModelImporter;
            Debug.Log("[GaMo FBX] Rig nguồn=" + (si != null ? si.animationType.ToString() : "?") + ", Rig FBX mới=" + (di != null ? di.animationType.ToString() : "?")
                + " | SMR mới=" + b + "/" + a + ", MeshRenderer mới=" + nr.GetComponentsInChildren<MeshRenderer>(true).Length);

            if (b >= a) { Debug.Log("[GaMo FBX] Skin OK: " + b + "/" + a + " SkinnedMeshRenderer."); FbxVerifyRest(srcRoot, nr); return; }

            // Thử Rig Generic (Rig None / Humanoid lỗi avatar khiến Unity bỏ skin). Không được thì trả lại cài đặt cũ.
            if (di != null && di.animationType != ModelImporterAnimationType.Generic)
            {
                var old = di.animationType;
                di.animationType = ModelImporterAnimationType.Generic;
                di.SaveAndReimport();
                var nr2 = AssetDatabase.LoadAssetAtPath<GameObject>(dstPath);
                int b2 = nr2 != null ? nr2.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length : 0;
                if (b2 >= a) { Debug.LogWarning("[GaMo FBX] Skin OK sau khi đổi Rig " + old + " -> Generic (" + b2 + "/" + a + ")."); return; }
                di.animationType = old;
                di.SaveAndReimport();
                Debug.LogWarning("[GaMo FBX] Đổi Rig sang Generic vẫn " + b2 + "/" + a + " SkinnedMeshRenderer: lỗi nằm ở cấu trúc skin trong file FBX, không phải Rig.");
            }
            else
                Debug.LogWarning("[GaMo FBX] FBX mới có " + b + "/" + a + " SkinnedMeshRenderer dù Rig đã là " + (di != null ? di.animationType.ToString() : "?") + ": lỗi nằm ở cấu trúc skin trong file FBX.");
        }

        // So sánh hình dạng skin ở tư thế nghỉ giữa model nguồn và FBX mới (skin bằng CPU): cho biết lệch do bindpose, vertex hay bone.
        static Vector3 FbxSkinPoint(Vector3 v, BoneWeight w, Matrix4x4[] M)
        {
            Vector3 r = Vector3.zero;
            if (w.weight0 > 0f && w.boneIndex0 < M.Length) r += w.weight0 * M[w.boneIndex0].MultiplyPoint3x4(v);
            if (w.weight1 > 0f && w.boneIndex1 < M.Length) r += w.weight1 * M[w.boneIndex1].MultiplyPoint3x4(v);
            if (w.weight2 > 0f && w.boneIndex2 < M.Length) r += w.weight2 * M[w.boneIndex2].MultiplyPoint3x4(v);
            if (w.weight3 > 0f && w.boneIndex3 < M.Length) r += w.weight3 * M[w.boneIndex3].MultiplyPoint3x4(v);
            return r;
        }

        static string FbxSkinInfo(GameObject root, SkinnedMeshRenderer smr, out Bounds skinB, out Bounds boneB, out float maxBp, out int dupW)
        {
            skinB = new Bounds(); boneB = new Bounds(); maxBp = 0f; dupW = 0;
            var mesh = smr.sharedMesh;
            if (mesh == null) return "không có mesh";
            if (!mesh.isReadable) return "mesh chưa bật Read/Write";
            var rinv = root.transform.worldToLocalMatrix;
            var bones = smr.bones; var bp = mesh.bindposes;
            var M = new Matrix4x4[bones.Length];
            bool bInit = false;
            for (int i = 0; i < bones.Length; i++)
            {
                M[i] = (bones[i] != null && i < bp.Length) ? rinv * bones[i].localToWorldMatrix * bp[i] : Matrix4x4.identity;
                if (bones[i] != null)
                {
                    var pos = rinv.MultiplyPoint3x4(bones[i].position);
                    if (!bInit) { boneB = new Bounds(pos, Vector3.zero); bInit = true; } else boneB.Encapsulate(pos);
                }
                if (i < bp.Length) maxBp = Mathf.Max(maxBp, Mathf.Abs(bp[i].m03), Mathf.Abs(bp[i].m13), Mathf.Abs(bp[i].m23));
            }
            var v = mesh.vertices; var w = mesh.boneWeights;
            if (w.Length != v.Length) return "boneWeights " + w.Length + " ≠ vertex " + v.Length;
            for (int i = 0; i < v.Length; i++)
            {
                var pt = FbxSkinPoint(v[i], w[i], M);
                if (i == 0) skinB = new Bounds(pt, Vector3.zero); else skinB.Encapsulate(pt);
            }
            return null;
        }

        static void FbxVerifyRest(GameObject src, GameObject dst)
        {
            try
            {
                var da = dst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var s in src.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    SkinnedMeshRenderer d = null;
                    foreach (var x in da) if (x.name == s.name) { d = x; break; }
                    if (d == null) { Debug.LogWarning("[GaMo FBX] Verify: không thấy '" + s.name + "' trong FBX mới."); continue; }
                    Bounds s1, s2, d1, d2; float sbp, dbp; int z;
                    var e1 = FbxSkinInfo(src, s, out s1, out s2, out sbp, out z);
                    var e2 = FbxSkinInfo(dst, d, out d1, out d2, out dbp, out z);
                    if (e1 != null || e2 != null) { Debug.LogWarning("[GaMo FBX] Verify '" + s.name + "': " + (e1 ?? e2)); continue; }
                    Debug.Log("[GaMo FBX] Verify '" + s.name + "' (tư thế nghỉ, so với root)"
                        + "\n  skin bounds  nguồn: tâm " + s1.center.ToString("0.###") + " cỡ " + s1.size.ToString("0.###")
                        + "\n  skin bounds  mới  : tâm " + d1.center.ToString("0.###") + " cỡ " + d1.size.ToString("0.###")
                        + "\n  bone bounds  nguồn: tâm " + s2.center.ToString("0.###") + " cỡ " + s2.size.ToString("0.###")
                        + "\n  bone bounds  mới  : tâm " + d2.center.ToString("0.###") + " cỡ " + d2.size.ToString("0.###")
                        + "\n  |bindpose.translation| lớn nhất  nguồn " + sbp.ToString("0.###") + " / mới " + dbp.ToString("0.###")
                        + "\n  node scale mesh nguồn " + s.transform.lossyScale.ToString("0.###") + " / mới " + d.transform.lossyScale.ToString("0.###"));
                }
            }
            catch (Exception ex) { Debug.LogWarning("[GaMo FBX] Verify lỗi: " + ex.Message); }
        }

        void FbxQueueExport(string srcPath)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                if (!FbxPkgAvailable)
                {
                    EditorUtility.DisplayDialog("Xuất FBX", "Chưa cài package FBX Exporter (" + FbxPkgId + ").\nBấm nút Import trong mục Export FBX để cài.", "OK");
                    return;
                }
                string dir = Path.GetDirectoryName(srcPath);
                dir = string.IsNullOrEmpty(dir) ? "Assets" : dir.Replace('\\', '/');
                string p = EditorUtility.SaveFilePanelInProject("Xuất FBX mới", Path.GetFileNameWithoutExtension(srcPath) + "_edited", "fbx",
                    "Ghi file FBX mới (file gốc giữ nguyên)", dir);
                if (string.IsNullOrEmpty(p)) return;
                if (string.Equals(p, srcPath, StringComparison.OrdinalIgnoreCase))
                {
                    EditorUtility.DisplayDialog("Xuất FBX", "Không ghi đè file FBX gốc. Hãy chọn tên khác.", "OK");
                    return;
                }
                try
                {
                    var root = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
                    if (root == null) throw new InvalidOperationException("Không đọc được model: " + srcPath);
                    FbxAutoStage(); // mesh đang sửa dở trong Edit Mesh cũng được đưa vào FBX mới
                    Dictionary<Mesh, FbxStaged> map;
                    _fbxStage.TryGetValue(srcPath, out map);
                    var warn = new List<string>();
                    var mats = FbxPkgExport(root, Path.GetFullPath(p), map, warn);
                    foreach (var w in warn) Debug.LogWarning("[GaMo FBX] " + w);
                    AssetDatabase.ImportAsset(p);
                    FbxPkgLogImported(p);
                    FbxCopyRigSettings(srcPath, p);
                    FbxLogSkinCheck(root, p);
                    FbxPkgVerifyLayout(root, p);

                    // FBX mới chỉ mang tên + màu material => gán lại (remap) đúng material gốc đang dùng trên model nguồn.
                    // Lấy TÊN THẬT mà Unity đặt cho material của FBX mới (sub-asset) để khỏi lệch tên.
                    var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                    if (imp == null) Debug.LogWarning("[GaMo FBX] Không lấy được ModelImporter của " + p + ": bỏ qua remap material.");
                    else if (mats == null || mats.Count == 0) Debug.LogWarning("[GaMo FBX] Model nguồn không có material nào để remap.");
                    else
                    {
                        var names = new List<string>();
                        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(p))
                        {
                            var mm = sub as Material;
                            if (mm != null) names.Add(mm.name);
                        }
                        if (names.Count == 0) names.AddRange(mats.Keys); // đã remap từ lần xuất trước: dùng tên đã ghi

                        int applied = 0;
                        var sb = new StringBuilder();
                        foreach (var nm in names)
                        {
                            Material target = null;
                            if (!mats.TryGetValue(nm, out target) || target == null)
                            {
                                target = null;
                                foreach (var kv in mats)
                                    if (string.Equals(kv.Key, nm, StringComparison.OrdinalIgnoreCase)) { target = kv.Value; break; }
                            }
                            if (target == null) { sb.Append("\n  - KHÔNG tìm thấy material gốc cho '").Append(nm).Append('\''); continue; }
                            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), nm), target);
                            applied++;
                            sb.Append("\n  - ").Append(nm).Append(" -> ").Append(AssetDatabase.GetAssetPath(target)).Append(" / ").Append(target.name);
                        }
                        imp.SaveAndReimport();
                        var imp2 = AssetImporter.GetAtPath(p) as ModelImporter;
                        int stored = imp2 != null ? imp2.GetExternalObjectMap().Count : -1;
                        Debug.Log("[GaMo FBX] Remap material: " + applied + "/" + names.Count + " (importer lưu " + stored + " mục)" + sb);
                    }
                    Debug.Log("[GaMo FBX] Đã xuất: " + p + (warn.Count > 0 ? " (" + warn.Count + " cảnh báo, xem Console)" : ""));
                }
                catch (Exception ex)
                {
                    Debug.LogError("[GaMo FBX] Xuất thất bại: " + ex.Message);
                    EditorUtility.DisplayDialog("Xuất FBX thất bại", ex.Message, "OK");
                }
            };
        }
    }
}
#endif