// GaMoInspector.FbxPackage.cs  -  part of CustomsEditor (GaMo Inspector)
// Xuat FBX bang package chinh thuc cua Unity "FBX Exporter" (com.unity.formats.fbx).
// - Kiem tra package truoc khi xuat; chua co thi hien thong bao + nut "Import" tu cai (UnityEditor.PackageManager.Client.Add).
// - Apply Scale lam tren ban sao model truoc khi xuat (FbxPkgApplyScale).
// - Goi ModelExporter bang REFLECTION nen file nay bien dich duoc ca khi chua cai package (khong can asmdef reference).
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UObject = UnityEngine.Object;
using PkgInfo = UnityEditor.PackageManager.PackageInfo;

namespace GaMo.BlenderEditorTools
{
    public partial class CustomsEditor
    {
        const string FbxPkgId = "com.unity.formats.fbx";
        const string FbxPkgTypeName = "UnityEditor.Formats.Fbx.Exporter.ModelExporter, Unity.Formats.Fbx.Editor";

        static Type _fbxPkgType;
        static double _fbxPkgCheckAt = -10;
        static AddRequest _fbxAddReq;
        static string _fbxAddError;
        static double _fbxAddRepaintAt;
        static readonly List<string> _fbxPkgExpect = new List<string>(); // "tên|tâm|cỡ" của mesh đã sửa lúc xuất, để đối chiếu sau import

        // Package da cai VA da bien dich xong (ModelExporter tim thay)
        static bool FbxPkgAvailable
        {
            get
            {
                if (_fbxPkgType != null) return true;
                double now = EditorApplication.timeSinceStartup;
                if (now - _fbxPkgCheckAt < 1.0) return false; // khong tra cuu moi lan OnGUI
                _fbxPkgCheckAt = now;
                _fbxPkgType = Type.GetType(FbxPkgTypeName);
                return _fbxPkgType != null;
            }
        }

        static bool FbxPkgInstalledNotCompiled()
        {
            try { return PkgInfo.FindForAssetPath("Packages/" + FbxPkgId) != null; }
            catch { return false; }
        }

        static bool FbxPkgInstalling { get { return _fbxAddReq != null && !_fbxAddReq.IsCompleted; } }

        static void FbxPkgInstall()
        {
            if (FbxPkgInstalling) return;
            _fbxAddError = null;
            _fbxAddReq = Client.Add(FbxPkgId);
            EditorApplication.update -= FbxPkgPoll;
            EditorApplication.update += FbxPkgPoll;
            Debug.Log("[GaMo FBX] Đang cài package " + FbxPkgId + " ...");
        }

        static void FbxPkgPoll()
        {
            if (_fbxAddReq == null) { EditorApplication.update -= FbxPkgPoll; return; }
            double now = EditorApplication.timeSinceStartup;
            if (!_fbxAddReq.IsCompleted)
            {
                if (now - _fbxAddRepaintAt > 0.3)
                {
                    _fbxAddRepaintAt = now;
                    foreach (var w in _instances) if (w != null) w.Repaint();
                }
                return;
            }
            EditorApplication.update -= FbxPkgPoll;
            if (_fbxAddReq.Status == StatusCode.Success)
                Debug.Log("[GaMo FBX] Đã cài " + _fbxAddReq.Result.displayName + " " + _fbxAddReq.Result.version + ". Unity sẽ biên dịch lại, chờ một lát.");
            else
            {
                _fbxAddError = _fbxAddReq.Error != null ? _fbxAddReq.Error.message : "Không rõ lỗi";
                Debug.LogError("[GaMo FBX] Cài package thất bại: " + _fbxAddError);
            }
            _fbxPkgCheckAt = -10;
            foreach (var w in _instances) if (w != null) w.Repaint();
        }

        // Ve thong bao + nut Import khi thieu package. Tra ve true neu da san sang de xuat.
        bool DrawFbxPkgNotice()
        {
            if (FbxPkgAvailable) return true;

            if (FbxPkgInstalling)
            {
                EditorGUILayout.HelpBox("Đang cài package FBX Exporter (" + FbxPkgId + ")... vui lòng chờ, Unity sẽ biên dịch lại sau khi cài xong.", MessageType.Info);
                return false;
            }
            if (_fbxAddReq != null && _fbxAddReq.IsCompleted && _fbxAddReq.Status == StatusCode.Success || FbxPkgInstalledNotCompiled())
            {
                EditorGUILayout.HelpBox("Package FBX Exporter đã được cài, đang chờ Unity biên dịch xong...", MessageType.Info);
                return false;
            }

            EditorGUILayout.HelpBox(
                "Chưa cài package \"FBX Exporter\" (" + FbxPkgId + ") của Unity. Cần package này để xuất FBX.\n" +
                "Bấm Import để cài tự động.", MessageType.Warning);
            if (!string.IsNullOrEmpty(_fbxAddError))
                EditorGUILayout.HelpBox("Lần cài trước thất bại: " + _fbxAddError, MessageType.Error);
            if (GUILayout.Button(new GUIContent("Import FBX Exporter package", "Chạy Package Manager: Add " + FbxPkgId), GUILayout.Height(22)))
            {
                FbxPkgInstall();
                GUIUtility.ExitGUI();
            }
            return false;
        }

        // Dung 1 ban sao cua model, thay mesh bang ban da sua, roi giao cho ModelExporter cua package.
        // Tra ve: ten material trong FBX moi -> material asset goc (de remap sau khi import), giong GaMoFbxWriter.Write.
        static Dictionary<string, Material> FbxPkgExport(GameObject srcRoot, string absPath, Dictionary<Mesh, FbxStaged> replace, List<string> warn)
        {
            if (!FbxPkgAvailable) throw new InvalidOperationException("Chưa cài package FBX Exporter (" + FbxPkgId + ").");

            var mats = new Dictionary<string, Material>();
            var temp = new List<Mesh>(); // mesh bản sao tạo khi Apply Scale, xóa sau khi xuất
            GameObject inst = null;
            try
            {
                inst = UObject.Instantiate(srcRoot);
                inst.name = srcRoot.name;
                // Instantiate trong Editor giữ liên kết Prefab/Model: gỡ liên kết để việc đổi mesh không bị coi là override của model gốc
                try { if (PrefabUtility.IsPartOfPrefabInstance(inst)) PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); }
                catch (Exception ex) { warn.Add("Không gỡ được liên kết Prefab của bản sao: " + ex.Message); }

                // Tra mesh đã sửa theo tham chiếu; không khớp (mesh gốc bị reimport => object mới) thì khớp theo TÊN mesh
                var byName = new Dictionary<string, FbxStaged>();
                int stagedTotal = 0;
                if (replace != null)
                    foreach (var kv in replace)
                        if (kv.Value != null && kv.Value.mesh != null) { byName[kv.Value.mesh.name] = kv.Value; stagedTotal++; }
                int replaced = 0;
                _fbxPkgExpect.Clear();

                // 1) Thay mesh đã sửa + bù transform node cho phần đã bake (chỉ với mesh không skin)
                foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                {
                    var smr = t.GetComponent<SkinnedMeshRenderer>();
                    var mf = t.GetComponent<MeshFilter>();
                    Mesh src = smr != null ? smr.sharedMesh : (mf != null ? mf.sharedMesh : null);
                    FbxStaged st = null;
                    bool hit = src != null && replace != null
                        && ((replace.TryGetValue(src, out st) && st != null && st.mesh != null) || byName.TryGetValue(src.name, out st));
                    if (hit && st != null && st.mesh != null)
                    {
                        replaced++;
                        st.mesh.RecalculateBounds();
                        src.RecalculateBounds();
                        _fbxPkgExpect.Add(src.name + "|" + st.mesh.bounds.center.ToString("0.###") + "|" + st.mesh.bounds.size.ToString("0.###"));
                        Debug.Log("[GaMo FBX] Thay mesh '" + src.name + "' bằng bản đã sửa: bounds gốc tâm " + src.bounds.center.ToString("0.###") + " cỡ " + src.bounds.size.ToString("0.###")
                            + " -> sửa tâm " + st.mesh.bounds.center.ToString("0.###") + " cỡ " + st.mesh.bounds.size.ToString("0.###")
                            + (st.xform != Matrix4x4.identity ? " | đã bake: dịch " + ((Vector3)st.xform.GetColumn(3)).ToString("0.###") : ""));
                        st.mesh.name = src.name;
                        if (smr != null) smr.sharedMesh = st.mesh; // skin: hình dạng do bone quyết định, Edit Mesh chỉ cho dịch pivot (bindpose đã bù)
                        else
                        {
                            mf.sharedMesh = st.mesh;
                            // Bù node để mesh KHÔNG đổi chỗ / hướng / cỡ trong scene: vertex mới = A * vertex cũ  =>  N' * A = N  =>  N' = N * A^-1.
                            // (pivot dịch => vị trí node đổi; bake rotation => rotation node trừ đi phần đã bake, vd -90 -> 0; bake scale => scale node chia lại)
                            if (st.xform != Matrix4x4.identity && Mathf.Abs(st.xform.determinant) > 1e-12f)
                            {
                                var N = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
                                var N2 = N * st.xform.inverse;
                                Vector3 pos, scl; Quaternion rot;
                                FbxDecompose(N2, out pos, out rot, out scl);
                                t.localPosition = pos; t.localRotation = rot; t.localScale = scl;
                            }
                        }
                    }
                    var r = t.GetComponent<Renderer>();
                    if (r != null)
                        foreach (var m in r.sharedMaterials)
                            if (m != null && !mats.ContainsKey(m.name)) mats[m.name] = m;
                }

                if (stagedTotal > 0 && replaced == 0)
                    warn.Add("Có " + stagedTotal + " mesh đã sửa nhưng KHÔNG mesh nào khớp với model khi xuất: bản xuất sẽ giống file gốc.");
                else if (replaced < stagedTotal)
                    warn.Add("Chỉ thay được " + replaced + "/" + stagedTotal + " mesh đã sửa (mesh còn lại không thấy trong model).");

                // 2) Apply Scale: nướng scale mọi node vào vertex / vị trí bone rồi đặt scale về 1 (làm trên bản sao, không đụng asset)
                if (FbxApplyScale) FbxPkgApplyScale(inst, temp, warn);

                // 3) Xuất CẢ cây từ root (không tách node con ra làm node gốc): package chỉ ghi transform GLOBAL cho node gốc xuất,
                //    còn mọi node bên dưới ghi LOCAL => giữ đúng bố cục / vị trí / xoay / scale giữa các mesh như model gốc.
                //    (Tách các node con ra từng cái làm mất quan hệ cha-con với root và làm vị trí bị lộn xộn.)
                //    Đổi lại Unity import sẽ có thêm 1 node cha mang tên model.
                string dirOut = System.IO.Path.GetDirectoryName(absPath);
                if (!string.IsNullOrEmpty(dirOut)) System.IO.Directory.CreateDirectory(dirOut);
                FbxPkgInvoke(absPath, new UObject[] { inst }, false);
            }
            finally
            {
                if (inst != null) UObject.DestroyImmediate(inst);
                foreach (var m in temp) if (m != null) UObject.DestroyImmediate(m);
            }
            return mats;
        }

        // Tách ma trận TRS thành vị trí / xoay / scale (scale âm: đảo trục X; shear chỉ gần đúng)
        static void FbxDecompose(Matrix4x4 m, out Vector3 pos, out Quaternion rot, out Vector3 scl)
        {
            pos = new Vector3(m.m03, m.m13, m.m23);
            Vector3 x = m.GetColumn(0), y = m.GetColumn(1), z = m.GetColumn(2);
            scl = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            if (Vector3.Dot(Vector3.Cross(x, y), z) < 0f) scl.x = -scl.x;
            Vector3 zn = scl.z > 1e-9f ? z / scl.z : Vector3.forward;
            Vector3 yn = scl.y > 1e-9f ? y / scl.y : Vector3.up;
            rot = zn.sqrMagnitude > 1e-9f && yn.sqrMagnitude > 1e-9f ? Quaternion.LookRotation(zn, yn) : Quaternion.identity;
            // làm sạch sai số số học gần 0 để Inspector hiện 0 thay vì 1e-7
            var e = rot.eulerAngles;
            for (int i = 0; i < 3; i++) { float a = Mathf.Repeat(e[i] + 180f, 360f) - 180f; if (Mathf.Abs(a) < 1e-3f) e[i] = 0f; }
            if (e == Vector3.zero) rot = Quaternion.identity;
        }

        // Sau import FBX mới: log bounds mesh để đối chiếu với bản đã sửa
        static void FbxPkgLogImported(string dstPath)
        {
            try
            {
                var seen = new HashSet<string>();
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(dstPath))
                {
                    var m = a as Mesh;
                    if (m == null || !seen.Add(m.name)) continue;
                    string exp = null;
                    foreach (var e in _fbxPkgExpect) if (e.StartsWith(m.name + "|")) { exp = e; break; }
                    Debug.Log("[GaMo FBX] FBX mới: mesh '" + m.name + "' tâm " + m.bounds.center.ToString("0.###") + " cỡ " + m.bounds.size.ToString("0.###")
                        + (exp != null ? "  (mesh đã sửa lúc xuất: " + exp.Substring(m.name.Length + 1).Replace("|", " cỡ ") + ")" : "  (mesh giữ nguyên)"));
                }
            }
            catch (Exception ex) { Debug.LogWarning("[GaMo FBX] Không đối chiếu được mesh sau import: " + ex.Message); }
        }

        // So sánh bố cục (bounds của từng mesh so với root) giữa model nguồn và FBX mới: báo node nào lệch vị trí / cỡ.
        static Bounds FbxRelBounds(GameObject root, Transform t, Mesh m)
        {
            var mat = root.transform.worldToLocalMatrix * t.localToWorldMatrix;
            var b = m.bounds; var c = b.center; var e = b.extents;
            Bounds r = default(Bounds); bool init = false;
            for (int i = 0; i < 8; i++)
            {
                var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                var w = mat.MultiplyPoint3x4(corner);
                if (!init) { r = new Bounds(w, Vector3.zero); init = true; } else r.Encapsulate(w);
            }
            return r;
        }

        static Transform FbxFindByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        static void FbxPkgVerifyLayout(GameObject src, string dstPath)
        {
            try
            {
                var dst = AssetDatabase.LoadAssetAtPath<GameObject>(dstPath);
                if (dst == null) return;
                int bad = 0, total = 0;
                var sb = new System.Text.StringBuilder();
                foreach (var t in src.GetComponentsInChildren<Transform>(true))
                {
                    var smr = t.GetComponent<SkinnedMeshRenderer>();
                    var mf = t.GetComponent<MeshFilter>();
                    Mesh m = smr != null ? smr.sharedMesh : (mf != null ? mf.sharedMesh : null);
                    if (m == null) continue;
                    var dt = FbxFindByName(dst.transform, t.name);
                    var dsmr = dt != null ? dt.GetComponent<SkinnedMeshRenderer>() : null;
                    var dmf = dt != null ? dt.GetComponent<MeshFilter>() : null;
                    Mesh dm = dsmr != null ? dsmr.sharedMesh : (dmf != null ? dmf.sharedMesh : null);
                    total++;
                    if (dm == null) { bad++; sb.Append("\n  - '").Append(t.name).Append("': không thấy trong FBX mới"); continue; }
                    // dst root có thể là root FBX hoặc node bọc thêm: lấy root FBX (dst) làm mốc cho cả hai phía
                    var b1 = FbxRelBounds(src, t, m);
                    var b2 = FbxRelBounds(dst, dt, dm);
                    float tol = Mathf.Max(1e-3f, 0.01f * b1.size.magnitude);
                    if ((b1.center - b2.center).magnitude > tol || (b1.size - b2.size).magnitude > tol)
                    {
                        bad++;
                        sb.Append("\n  - '").Append(t.name).Append("': nguồn tâm ").Append(b1.center.ToString("0.###")).Append(" cỡ ").Append(b1.size.ToString("0.###"))
                          .Append(" | mới tâm ").Append(b2.center.ToString("0.###")).Append(" cỡ ").Append(b2.size.ToString("0.###"))
                          .Append("\n      local nguồn pos ").Append(t.localPosition.ToString("0.###")).Append(" rot ").Append(t.localEulerAngles.ToString("0.#")).Append(" scale ").Append(t.localScale.ToString("0.###"))
                          .Append("\n      local mới   pos ").Append(dt.localPosition.ToString("0.###")).Append(" rot ").Append(dt.localEulerAngles.ToString("0.#")).Append(" scale ").Append(dt.localScale.ToString("0.###"));
                    }
                }
                Debug.Log("[GaMo FBX] Đối chiếu bố cục: " + (total - bad) + "/" + total + " mesh trùng nguồn"
                    + (bad > 0 ? " (mesh đã sửa / Apply Scale sẽ lệch là bình thường; các mesh còn lại lệch nghĩa là transform bị đổi khi xuất):" + sb : ""));
            }
            catch (Exception ex) { Debug.LogWarning("[GaMo FBX] Không đối chiếu được bố cục: " + ex.Message); }
        }

        // Giống Blender Apply Scale. Root của model giữ nguyên (làm mốc), mọi node con: scale -> 1,1,1.
        // Vị trí local nhân scale tích lũy của cha, vertex nhân scale tích lũy của chính node (gồm cả node đó).
        // Skin: bindpose tính lại để skin matrix (mesh-local -> world) giữ nguyên:  Bnew * bpNew * X = Bold * bpOld.
        // Scale không đều mà node có xoay xen kẽ thì chỉ gần đúng.
        static void FbxPkgApplyScale(GameObject root, List<Mesh> temp, List<string> warn)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            var cum = new Dictionary<Transform, Vector3>();
            foreach (var t in all)
                cum[t] = t == root.transform ? Vector3.one : Vector3.Scale(cum[t.parent], t.localScale); // cha luôn đứng trước con

            // Skin matrix gốc K = BoneWorldCũ * bindpose (phải lấy TRƯỚC khi đổi transform)
            var skinK = new Dictionary<SkinnedMeshRenderer, Matrix4x4[]>();
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = smr.sharedMesh;
                if (m == null) continue;
                var bones = smr.bones; var bp = m.bindposes;
                if (bones.Length == 0 || bp.Length != bones.Length) continue;
                var K = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    K[i] = bones[i] != null ? bones[i].localToWorldMatrix * bp[i] : Matrix4x4.identity;
                skinK[smr] = K;
            }

            foreach (var t in all)
            {
                if (t == root.transform) continue;
                t.localPosition = Vector3.Scale(cum[t.parent], t.localPosition);
                t.localScale = Vector3.one;
            }

            foreach (var t in all)
            {
                var smr = t.GetComponent<SkinnedMeshRenderer>();
                var mf = t.GetComponent<MeshFilter>();
                Mesh src = smr != null ? smr.sharedMesh : (mf != null ? mf.sharedMesh : null);
                if (src == null) continue;
                Vector3 s = cum[t];
                if (Mathf.Approximately(s.x, 1f) && Mathf.Approximately(s.y, 1f) && Mathf.Approximately(s.z, 1f)) continue;
                if (!src.isReadable) { warn.Add("Mesh '" + src.name + "' chưa bật Read/Write: bỏ qua Apply Scale cho mesh này."); continue; }

                var m = UObject.Instantiate(src);
                m.name = src.name;
                temp.Add(m);

                var v = m.vertices;
                for (int i = 0; i < v.Length; i++) v[i] = Vector3.Scale(s, v[i]);
                m.vertices = v;

                var nrm = m.normals;
                if (nrm.Length == v.Length)
                {
                    var inv = new Vector3(1f / s.x, 1f / s.y, 1f / s.z); // normal: nghịch đảo chuyển vị của scale
                    for (int i = 0; i < nrm.Length; i++) nrm[i] = Vector3.Scale(inv, nrm[i]).normalized;
                    m.normals = nrm;
                }
                var tan = m.tangents;
                if (tan.Length == v.Length)
                {
                    for (int i = 0; i < tan.Length; i++)
                    {
                        var d = Vector3.Scale(s, new Vector3(tan[i].x, tan[i].y, tan[i].z)).normalized;
                        tan[i] = new Vector4(d.x, d.y, d.z, tan[i].w);
                    }
                    m.tangents = tan;
                }
                if (s.x * s.y * s.z < 0f) // scale âm đảo chiều mặt => đảo thứ tự tam giác
                {
                    for (int sm = 0; sm < m.subMeshCount; sm++)
                    {
                        if (m.GetTopology(sm) != MeshTopology.Triangles) continue;
                        var tri = m.GetTriangles(sm);
                        for (int i = 0; i + 2 < tri.Length; i += 3) { int tmp = tri[i]; tri[i] = tri[i + 2]; tri[i + 2] = tmp; }
                        m.SetTriangles(tri, sm);
                    }
                }

                Matrix4x4[] K;
                if (smr != null && skinK.TryGetValue(smr, out K))
                {
                    var X = Matrix4x4.Scale(s);
                    var Xi = X.inverse;
                    var bones = smr.bones;
                    var bpNew = new Matrix4x4[bones.Length];
                    for (int i = 0; i < bones.Length; i++)
                        bpNew[i] = bones[i] != null ? bones[i].localToWorldMatrix.inverse * K[i] * Xi : Matrix4x4.identity;
                    m.bindposes = bpNew;
                }
                m.RecalculateBounds();
                if (smr != null) smr.sharedMesh = m; else mf.sharedMesh = m;
            }
        }

        // Goi ModelExporter.ExportObjects / ExportObject bang reflection (tham so phu de null)
        static void FbxPkgInvoke(string absPath, UObject[] objs, bool multi)
        {
            var type = _fbxPkgType;
            const BindingFlags F = BindingFlags.Public | BindingFlags.Static;
            MethodInfo mi = null;
            bool useArray = false;
            if (multi)
                foreach (var m in type.GetMethods(F))
                {
                    var ps = m.GetParameters();
                    if (m.Name == "ExportObjects" && ps.Length >= 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(UObject[]))
                    { mi = m; useArray = true; break; }
                }
            if (mi == null)
                foreach (var m in type.GetMethods(F))
                {
                    var ps = m.GetParameters();
                    if (m.Name == "ExportObject" && ps.Length >= 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(UObject))
                    { mi = m; break; }
                }
            if (mi == null) throw new MissingMethodException("Không tìm thấy ModelExporter.ExportObject(s) trong package FBX Exporter (phiên bản không tương thích?).");

            var pars = mi.GetParameters();
            var args = new object[pars.Length];
            args[0] = absPath;
            if (useArray) args[1] = objs;
            else
            {
                if (objs.Length != 1) throw new InvalidOperationException("Package FBX Exporter cũ không có ExportObjects (nhiều node gốc).");
                args[1] = objs[0];
            }
            // các tham số còn lại (IExportOptions...) để null = dùng Export Settings mặc định của package
            try
            {
                object ret = mi.Invoke(null, args);
                if (ret is string && string.IsNullOrEmpty((string)ret) && !System.IO.File.Exists(absPath))
                    throw new InvalidOperationException("ModelExporter không ghi được file: " + absPath);
            }
            catch (TargetInvocationException tie)
            {
                throw new InvalidOperationException("ModelExporter lỗi: " + (tie.InnerException != null ? tie.InnerException.Message : tie.Message), tie.InnerException ?? tie);
            }
        }
    }
}
#endif