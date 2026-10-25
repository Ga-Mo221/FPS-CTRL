// GaMoMeshAtlasCombiner.cs  -  by Gà Mờ  (v2: hỗ trợ LODGroup)
// Đặt file này vào thư mục Assets/**/Editor/
// Menu: Tools > GaMo > Mesh & Atlas Combiner
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

namespace GaMo.BlenderEditorTools
{
    public partial class GaMoMeshAtlasCombiner : EditorWindow
    {
        [MenuItem("Tools/GaMo ToolKit/Blender Style Workflow/Mesh & Atlas Combiner", priority = 2)]
        static void Open() => GetWindow<GaMoMeshAtlasCombiner>("GaMo Combiner");

        // ---------- Settings ----------
        string outputFolder = "Assets/GaMoCombined";
        string outputName = "Combined";
        int maxAtlasSize = 2048;
        int padding = 4;
        bool includeInactive = false;
        bool disableOriginals = true;
        bool compactVertex = true;   // nén vertex: normal/tangent SNorm16, UV UNorm16, color UNorm8
        bool keepReadable = false;   // Read/Write: bật = giữ thêm 1 bản copy mesh trên RAM khi chạy game
        bool forceTangents = false;  // giữ tangent kể cả khi không material nào dùng normal map
        int mode = 0;                // 0 = Atlas (gộp tất cả), 1 = Grid (theo ô + material, xem .Grid.cs)
        string result = "";

        static readonly int[] AtlasSizes = { 512, 1024, 2048, 4096, 8192 };
        static readonly string[] AtlasLabels = { "512", "1024", "2048", "4096", "8192" };

        class Piece
        {
            public MeshFilter mf;
            public MeshRenderer mr;
            public Mesh mesh;
            public int sub;
            public Material mat;
            public int level;   // -1 = không thuộc LODGroup, 0.. = mức LOD
            public int tile;    // index tile trong atlas
            public Rect uvRange; // vùng UV (đã làm tròn số nguyên) mà tile bao phủ, VD [0,2]x[0,1]
        }

        class ShaderAtlas
        {
            public Shader shader;
            public List<Piece> pieces = new List<Piece>();
            public Rect[] rects;
            public Material material;
            public int tileCount;
        }

        void OnGUI()
        {
            GUILayout.Label("GaMo Mesh & Atlas Combiner", EditorStyles.boldLabel);
            mode = GUILayout.Toolbar(mode, new[] { "Atlas (gộp tất cả)", "Grid (theo ô + material)" });
            if (mode == 0)
                EditorGUILayout.HelpBox("Chọn 1 hoặc nhiều GameObject (tool tự lấy cả con).\n" +
                                        "Có LODGroup: gộp theo từng mức LOD, dùng chung 1 atlas, tạo LODGroup mới.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Chia map thành các ô. Trong mỗi ô, các mesh Static cùng material được gộp thành 1 mesh " +
                                        "(giữ nguyên material gốc). Mỗi ô là renderer riêng nên culling vẫn hoạt động.", MessageType.Info);

            outputFolder = EditorGUILayout.TextField("Thư mục lưu", outputFolder);
            outputName = EditorGUILayout.TextField("Tên output", outputName);
            if (mode == 0)
            {
                int idx = System.Array.IndexOf(AtlasSizes, maxAtlasSize);
                if (idx < 0) idx = 2;
                idx = EditorGUILayout.Popup("Atlas tối đa", idx, AtlasLabels);
                maxAtlasSize = AtlasSizes[idx];
                padding = EditorGUILayout.IntSlider("Padding (px)", padding, 0, 16);
            }
            else DrawGridSettings();
            includeInactive = EditorGUILayout.Toggle("Gồm object đang tắt", includeInactive);
            disableOriginals = EditorGUILayout.Toggle(mode == 0 ? "Tắt object gốc (SetActive)" : "Tắt renderer gốc", disableOriginals);

            EditorGUILayout.Space();
            GUILayout.Label("Dung lượng mesh", EditorStyles.boldLabel);
            compactVertex = EditorGUILayout.Toggle(new GUIContent("Nén vertex",
                "Normal/Tangent dùng SNorm16, UV dùng UNorm16, Color dùng UNorm8. Giảm ~40-50% dung lượng, sai số không nhìn thấy."), compactVertex);
            keepReadable = EditorGUILayout.Toggle(new GUIContent("Read/Write",
                "Chỉ bật nếu script cần đọc mesh lúc chạy game. Tắt = không giữ bản copy trên RAM (giảm 1/2 bộ nhớ)."), keepReadable);
            forceTangents = EditorGUILayout.Toggle(new GUIContent("Luôn giữ tangent",
                "Mặc định tangent chỉ được giữ khi có material dùng normal map."), forceTangents);

            EditorGUILayout.Space();
            bool canRun = Selection.gameObjects.Length > 0 || (mode == 1 && gridWholeScene);
            using (new EditorGUI.DisabledScope(!canRun))
            {
                if (GUILayout.Button(mode == 0 ? "Gộp Mesh + Atlas" : "Gộp theo Grid", GUILayout.Height(32)))
                {
                    if (mode == 0) Combine();
                    else CombineGrid();
                }
            }

            if (!string.IsNullOrEmpty(result))
                EditorGUILayout.HelpBox(result, MessageType.None);
        }

        // ---------- Main ----------
        void Combine()
        {
            var selected = Selection.gameObjects;

            // ----- 1. Thu thập LODGroup -----
            var lodGroups = new List<LODGroup>();
            int skinnedSkipped = 0;
            foreach (var go in selected)
            {
                foreach (var lg in go.GetComponentsInChildren<LODGroup>(includeInactive))
                    if (!lodGroups.Contains(lg)) lodGroups.Add(lg);
                skinnedSkipped += go.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive).Length;
            }

            int levelCount = 0;
            LODGroup refGroup = null;
            foreach (var lg in lodGroups)
                if (lg.lodCount > levelCount) { levelCount = lg.lodCount; refGroup = lg; }

            // ----- 2. Thu thập pieces (mesh, submesh, material, level) -----
            var pieces = new List<Piece>();
            var lodRenderers = new HashSet<Renderer>();

            foreach (var lg in lodGroups)
            {
                var lods = lg.GetLODs();
                if (lods.Length == 0) continue;
                foreach (var l in lods)
                    foreach (var r in l.renderers)
                        if (r != null) lodRenderers.Add(r);

                for (int L = 0; L < levelCount; L++)
                {
                    // Group ít LOD hơn thì dùng lại mức cuối của nó
                    var lod = lods[Mathf.Min(L, lods.Length - 1)];
                    foreach (var r in lod.renderers)
                        AddPieces(r as MeshRenderer, L, pieces);
                }
            }

            var seen = new HashSet<MeshRenderer>();
            foreach (var go in selected)
                foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(includeInactive))
                    if (!lodRenderers.Contains(mr) && seen.Add(mr))
                        AddPieces(mr, -1, pieces);

            if (pieces.Count == 0)
            {
                result = "Không tìm thấy MeshFilter + MeshRenderer hợp lệ.";
                return;
            }

            Directory.CreateDirectory(outputFolder);
            var log = new System.Text.StringBuilder();
            var parent = new GameObject(outputName + "_Root");
            Undo.RegisterCreatedObjectUndo(parent, "GaMo Combine");

            try
            {
                // ----- 3. Atlas dùng chung cho mọi mức LOD (theo shader) -----
                var atlases = new Dictionary<Shader, ShaderAtlas>();
                foreach (var p in pieces)
                {
                    if (!atlases.TryGetValue(p.mat.shader, out var a))
                        atlases[p.mat.shader] = a = new ShaderAtlas { shader = p.mat.shader };
                    a.pieces.Add(p);
                }

                int ai = 0;
                foreach (var a in atlases.Values)
                {
                    EditorUtility.DisplayProgressBar("GaMo Combiner", "Atlas: " + a.shader.name, (float)ai++ / atlases.Count);
                    BuildAtlas(a, Sanitize(outputName + "_" + a.shader.name));
                    log.AppendLine($"- Atlas [{a.shader.name}]: {a.tileCount} tile");
                }

                // ----- 4. Gộp mesh theo từng mức -----
                var levelIds = new List<int>();
                if (pieces.Exists(p => p.level < 0)) levelIds.Add(-1);
                for (int L = 0; L < levelCount; L++) levelIds.Add(L);

                var levelRenderers = new List<Renderer>[Mathf.Max(levelCount, 0)];
                for (int L = 0; L < levelCount; L++) levelRenderers[L] = new List<Renderer>();

                int li = 0;
                foreach (int L in levelIds)
                {
                    string tag = L < 0 ? "NoLOD" : "LOD" + L;
                    EditorUtility.DisplayProgressBar("GaMo Combiner", "Gộp " + tag, (float)li++ / levelIds.Count);
                    int before = 0, after = 0;

                    foreach (var a in atlases.Values)
                    {
                        var list = a.pieces.FindAll(p => p.level == L);
                        if (list.Count == 0) continue;
                        before += list.Count;

                        string meshName = Sanitize($"{outputName}_{tag}_{a.shader.name}");
                        var mesh = BuildMesh(list, a, meshName, out int uvBad);

                        var go = new GameObject(meshName);
                        go.transform.SetParent(parent.transform, false);
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var mr = go.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = a.material;
                        Undo.RegisterCreatedObjectUndo(go, "GaMo Combine");
                        if (L >= 0) levelRenderers[L].Add(mr);
                        after++;

                        int stride = mesh.GetVertexBufferStride(0);
                        log.AppendLine($"  {tag} [{a.shader.name}]: {mesh.vertexCount} verts, {mesh.GetIndexCount(0) / 3} tris, " +
                                       $"{stride} B/vertex (~{(mesh.vertexCount * stride) / 1024} KB vertex data)");
                        if (uvBad > 0)
                            log.AppendLine($"  ! {uvBad} vertex có UV ngoài vùng tile -> đã clamp, texture có thể sai.");
                    }
                    log.AppendLine($"  => {tag}: {before} submesh -> {after} draw call");
                }

                // ----- 5. LODGroup mới -----
                if (levelCount > 0)
                    BuildLodGroup(parent, refGroup, levelRenderers, levelCount);

                // ----- 6. Collider -----
                CopyColliders(parent, selected, log);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (disableOriginals)
            {
                foreach (var go in selected)
                {
                    Undo.RecordObject(go, "GaMo Combine");
                    go.SetActive(false);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = parent;

            result = log.ToString();
            if (lodGroups.Count > 0)
                result += $"\nLOD: {lodGroups.Count} LODGroup gốc -> 1 LODGroup mới ({levelCount} mức).";
            if (skinnedSkipped > 0)
                result += $"\nBỏ qua {skinnedSkipped} SkinnedMeshRenderer (không hỗ trợ).";
            Debug.Log("[GaMo Combiner]\n" + result);
        }

        // ---------- LODGroup ----------
        void BuildLodGroup(GameObject parent, LODGroup refGroup, List<Renderer>[] levelRenderers, int levelCount)
        {
            var lg = parent.AddComponent<LODGroup>();
            var refLods = refGroup.GetLODs();
            var lods = new LOD[levelCount];
            for (int L = 0; L < levelCount; L++)
            {
                lods[L] = new LOD(refLods[L].screenRelativeTransitionHeight, levelRenderers[L].ToArray());
                lods[L].fadeTransitionWidth = refLods[L].fadeTransitionWidth;
            }
            lg.SetLODs(lods);
            lg.fadeMode = refGroup.fadeMode;
            lg.animateCrossFading = refGroup.animateCrossFading;
            lg.RecalculateBounds();
            // Không cần scale lại screenRelativeTransitionHeight theo kích thước:
            // giá trị này vốn đã là % kích thước MÀN HÌNH (projected size / screen height),
            // Unity tự chia theo bounds (size) riêng của từng LODGroup khi render, nên object
            // to hay nhỏ vẫn chuyển LOD đúng ở cùng % đó. Copy nguyên ngưỡng gốc là chuẩn nhất.
        }

        // ---------- Collider ----------
        void CopyColliders(GameObject parent, GameObject[] selected, System.Text.StringBuilder log)
        {
            var holderOf = new Dictionary<GameObject, GameObject>();
            var seenCol = new HashSet<Collider>();
            int colCount = 0;

            foreach (var go in selected)
            {
                foreach (var col in go.GetComponentsInChildren<Collider>(includeInactive))
                {
                    if (!seenCol.Add(col)) continue; // nhiều selection trùng nhánh con -> tránh copy 2 lần

                    var owner = col.gameObject;
                    if (!holderOf.TryGetValue(owner, out var holder))
                    {
                        // 1 object giữ đúng world transform của object gốc, để Box/Sphere/Capsule
                        // Collider (center/size định nghĩa theo local space) giữ nguyên hình dạng & vị trí.
                        holder = new GameObject(owner.name + "_Collider");
                        holder.transform.SetParent(parent.transform, false);
                        holder.transform.SetPositionAndRotation(owner.transform.position, owner.transform.rotation);
                        holder.transform.localScale = owner.transform.lossyScale;
                        Undo.RegisterCreatedObjectUndo(holder, "GaMo Combine");
                        holderOf[owner] = holder;
                    }

                    // CopyComponent/PasteComponentAsNew copy toàn bộ field (kể cả MeshCollider.sharedMesh,
                    // isTrigger, Physic Material...) chứ không chỉ center/size như BoxCollider.
                    ComponentUtility.CopyComponent(col);
                    ComponentUtility.PasteComponentAsNew(holder);
                    colCount++;
                }
            }

            if (colCount > 0)
                log.AppendLine($"- Collider: copy {colCount} collider ({holderOf.Count} object) sang cụm mới.");
        }

        // ---------- Pieces ----------
        void AddPieces(MeshRenderer mr, int level, List<Piece> list)
        {
            if (mr == null) return;
            if (level < 0 && !mr.enabled) return;   // renderer thuộc LOD do LODGroup tự bật/tắt nên không kiểm tra
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            var mats = mr.sharedMaterials;
            var mesh = mf.sharedMesh;
            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                var m = mats[s];
                if (m == null || m.shader == null) continue;
                list.Add(new Piece { mf = mf, mr = mr, mesh = mesh, sub = s, mat = m, level = level });
            }
        }

        // ---------- Atlas ----------
        void BuildAtlas(ShaderAtlas a, string name)
        {
            var keyToIndex = new Dictionary<string, int>();
            var keyRange = new Dictionary<string, Rect>();
            var keyFirst = new Dictionary<string, Piece>();
            var pieceKey = new Dictionary<Piece, string>();
            var uvCache = new Dictionary<Mesh, Vector2[]>();
            var tiles = new List<Texture2D>();

            // Pass 1: key + gộp vùng UV của mọi piece dùng chung 1 tile
            foreach (var p in a.pieces)
            {
                string tp = TexProp(p.mat);
                string cp = ColorProp(p.mat);
                Texture tex = tp != null ? p.mat.GetTexture(tp) : null;
                Color tint = cp != null ? p.mat.GetColor(cp) : Color.white;
                Vector2 tiling = tp != null ? p.mat.GetTextureScale(tp) : Vector2.one;
                Vector2 offset = tp != null ? p.mat.GetTextureOffset(tp) : Vector2.zero;

                // Tiling/Offset khác nhau = ảnh khác nhau -> phải nằm trong key, không được gộp chung tile
                string key = (tex != null ? tex.GetInstanceID() : 0) + "_" + ColorUtility.ToHtmlStringRGBA(tint)
                           + "_" + tiling.x.ToString("F4") + "x" + tiling.y.ToString("F4")
                           + "_" + offset.x.ToString("F4") + "x" + offset.y.ToString("F4");
                pieceKey[p] = key;

                // Tile màu trơn thì UV không quan trọng
                Rect range = tex != null ? UvRange(p, uvCache) : new Rect(0, 0, 1, 1);
                if (keyRange.TryGetValue(key, out var old))
                    range = Rect.MinMaxRect(Mathf.Min(old.xMin, range.xMin), Mathf.Min(old.yMin, range.yMin),
                                            Mathf.Max(old.xMax, range.xMax), Mathf.Max(old.yMax, range.yMax));
                else
                    keyFirst[key] = p;
                keyRange[key] = range;
            }

            // Pass 2: tạo tile (bake đúng vùng UV lặp, VD UV 0-2 -> texture lặp 2 lần trong 1 tile)
            foreach (var p in a.pieces)
            {
                string key = pieceKey[p];
                if (!keyToIndex.TryGetValue(key, out int ti))
                {
                    var f = keyFirst[key];
                    string tp = TexProp(f.mat);
                    string cp = ColorProp(f.mat);
                    Texture tex = tp != null ? f.mat.GetTexture(tp) : null;
                    Color tint = cp != null ? f.mat.GetColor(cp) : Color.white;
                    Vector2 tiling = tp != null ? f.mat.GetTextureScale(tp) : Vector2.one;
                    Vector2 offset = tp != null ? f.mat.GetTextureOffset(tp) : Vector2.zero;

                    ti = tiles.Count;
                    keyToIndex[key] = ti;
                    tiles.Add(MakeTile(tex, tint, tiling, offset, keyRange[key]));
                }
                p.tile = ti;
                p.uvRange = keyRange[key];
            }

            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            a.rects = atlas.PackTextures(tiles.ToArray(), padding, maxAtlasSize, false);
            a.tileCount = tiles.Count;

            string texPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{name}_Atlas.png");
            File.WriteAllBytes(texPath, atlas.EncodeToPNG());
            DestroyImmediate(atlas);
            foreach (var t in tiles) DestroyImmediate(t);
            AssetDatabase.ImportAsset(texPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            if (importer != null)
            {
                importer.maxTextureSize = maxAtlasSize;
                importer.SaveAndReimport();
            }
            var atlasAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            var newMat = new Material(a.pieces[0].mat) { name = name + "_Mat" };
            string tpNew = TexProp(newMat);
            string cpNew = ColorProp(newMat);
            if (tpNew != null)
            {
                newMat.SetTexture(tpNew, atlasAsset);
                newMat.SetTextureScale(tpNew, Vector2.one);
                newMat.SetTextureOffset(tpNew, Vector2.zero);
            }
            if (cpNew != null) newMat.SetColor(cpNew, Color.white); // tint đã bake vào atlas
            string matPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{name}_Mat.mat");
            AssetDatabase.CreateAsset(newMat, matPath);
            a.material = newMat;
        }

        // ---------- Mesh ----------
        // atlas == null: giữ nguyên UV gốc (chế độ Grid, gộp cùng material).
        // toLocal: đổi từ world sang local của object chứa mesh gộp (null = world, object đặt ở gốc toạ độ).
        Mesh BuildMesh(List<Piece> pieces, ShaderAtlas atlas, string name, out int uvOutOfRange,
                       Matrix4x4? toLocal = null, string folder = null)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var colors = new List<Color32>();
            var tris = new List<int>();
            bool anyNormal = false, anyTangent = false, anyColor = false;
            uvOutOfRange = 0;

            foreach (var p in pieces)
            {
                var mesh = p.mesh;
                var vs = mesh.vertices;
                var ns = mesh.normals;
                var ts = mesh.tangents;
                var uv = mesh.uv;
                var cs = mesh.colors32;

                bool hasN = ns.Length == vs.Length;
                bool hasT = ts.Length == vs.Length;
                bool hasC = cs.Length == vs.Length;
                bool hasUV = uv.Length == vs.Length;
                anyNormal |= hasN; anyTangent |= hasT; anyColor |= hasC;

                Matrix4x4 l2w = (toLocal ?? Matrix4x4.identity) * p.mf.transform.localToWorldMatrix;
                Matrix4x4 nMat = l2w.inverse.transpose;
                bool flip = l2w.determinant < 0f;

                Rect r = atlas != null ? atlas.rects[p.tile] : default;

                int[] idx = mesh.GetTriangles(p.sub);
                var map = new Dictionary<int, int>();
                var newIdx = new int[idx.Length];

                for (int k = 0; k < idx.Length; k++)
                {
                    int o = idx[k];
                    if (!map.TryGetValue(o, out int n))
                    {
                        n = verts.Count;
                        map[o] = n;
                        verts.Add(l2w.MultiplyPoint3x4(vs[o]));
                        normals.Add(hasN ? nMat.MultiplyVector(ns[o]).normalized : Vector3.up);

                        if (hasT)
                        {
                            Vector3 td = l2w.MultiplyVector(new Vector3(ts[o].x, ts[o].y, ts[o].z)).normalized;
                            tangents.Add(new Vector4(td.x, td.y, td.z, flip ? -ts[o].w : ts[o].w));
                        }
                        else tangents.Add(new Vector4(1, 0, 0, 1));

                        Vector2 u = hasUV ? uv[o] : Vector2.zero;
                        if (atlas == null)
                        {
                            // Không atlas: material giữ nguyên -> UV giữ nguyên
                            uvs.Add(u);
                        }
                        else
                        {
                            // Tiling/Offset + vùng UV lặp đã bake sẵn vào pixel của tile (xem MakeTile),
                            // nên chỉ cần remap UV gốc từ p.uvRange về 0-1 (KHÔNG clamp UV gốc, sẽ làm dẹt island).
                            u.x = (u.x - p.uvRange.x) / p.uvRange.width;
                            u.y = (u.y - p.uvRange.y) / p.uvRange.height;
                            if (u.x < -0.001f || u.x > 1.001f || u.y < -0.001f || u.y > 1.001f) uvOutOfRange++;
                            u.x = Mathf.Clamp01(u.x);
                            u.y = Mathf.Clamp01(u.y);
                            uvs.Add(new Vector2(r.x + u.x * r.width, r.y + u.y * r.height));
                        }

                        colors.Add(hasC ? cs[o] : new Color32(255, 255, 255, 255));
                    }
                    newIdx[k] = n;
                }

                if (flip)
                    for (int k = 0; k + 2 < newIdx.Length; k += 3)
                        (newIdx[k], newIdx[k + 1]) = (newIdx[k + 1], newIdx[k]);

                tris.AddRange(newIdx);
            }

            // Bỏ kênh không dùng: tangent chỉ cần cho normal map, color toàn trắng thì vô nghĩa
            bool useTangent = anyTangent && (forceTangents || pieces.Exists(p => HasNormalMap(p.mat)));
            bool useColor = anyColor && colors.Exists(c => c.r != 255 || c.g != 255 || c.b != 255 || c.a != 255);

            var combined = new Mesh { name = name };
            if (verts.Count > 65535) combined.indexFormat = IndexFormat.UInt32;

            if (!anyNormal)
            {
                // Mesh gốc không có normal -> tính trước để còn ghi vào buffer nén
                var tmp = new Mesh { indexFormat = combined.indexFormat };
                tmp.SetVertices(verts);
                tmp.SetTriangles(tris, 0);
                tmp.RecalculateNormals();
                tmp.GetNormals(normals);
                DestroyImmediate(tmp);
            }

            if (compactVertex)
                WriteCompactVertices(combined, verts, normals, useTangent ? tangents : null, uvs, useColor ? colors : null, atlas != null);
            else
            {
                combined.SetVertices(verts);
                combined.SetNormals(normals);
                if (useTangent) combined.SetTangents(tangents);
                combined.SetUVs(0, uvs);
                if (useColor) combined.SetColors(colors);
            }
            combined.SetTriangles(tris, 0);
            combined.RecalculateBounds();

            string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{folder ?? outputFolder}/{name}.asset");
            AssetDatabase.CreateAsset(combined, meshPath);

            if (!keepReadable)
            {
                // Mesh không Read/Write: build không giữ bản copy CPU -> giảm 1/2 RAM
                var so = new SerializedObject(combined);
                so.FindProperty("m_IsReadable").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return combined;
        }

        // Ghi vertex buffer 1 stream với format nhỏ. Không dùng SetNormals/SetUVs... vì chúng
        // tự đổi layout về Float32. Sai số: UV ~1e-5 (≈0.02px trên atlas 2048), normal ~3e-5.
        static void WriteCompactVertices(Mesh mesh, List<Vector3> v, List<Vector3> n, List<Vector4> t,
                                         List<Vector2> uv, List<Color32> c, bool uvIn01)
        {
            var layout = new List<VertexAttributeDescriptor>
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.SNorm16, 4),
            };
            if (t != null) layout.Add(new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.SNorm16, 4));
            if (c != null) layout.Add(new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4));
            // UV atlas luôn trong 0-1 -> UNorm16. UV gốc (tiling, có thể âm / >1) -> giữ Float32 cho chính xác.
            layout.Add(new VertexAttributeDescriptor(VertexAttribute.TexCoord0,
                uvIn01 ? VertexAttributeFormat.UNorm16 : VertexAttributeFormat.Float32, 2));

            int count = v.Count;
            mesh.SetVertexBufferParams(count, layout.ToArray());
            int stride = mesh.GetVertexBufferStride(0);
            int oN = mesh.GetVertexAttributeOffset(VertexAttribute.Normal);
            int oT = t != null ? mesh.GetVertexAttributeOffset(VertexAttribute.Tangent) : 0;
            int oC = c != null ? mesh.GetVertexAttributeOffset(VertexAttribute.Color) : 0;
            int oU = mesh.GetVertexAttributeOffset(VertexAttribute.TexCoord0);

            var buf = new byte[count * stride];
            for (int i = 0; i < count; i++)
            {
                int o = i * stride;
                PutF32(buf, o, v[i].x); PutF32(buf, o + 4, v[i].y); PutF32(buf, o + 8, v[i].z);
                PutSN16(buf, o + oN, n[i].x); PutSN16(buf, o + oN + 2, n[i].y); PutSN16(buf, o + oN + 4, n[i].z);
                if (t != null)
                {
                    PutSN16(buf, o + oT, t[i].x); PutSN16(buf, o + oT + 2, t[i].y);
                    PutSN16(buf, o + oT + 4, t[i].z); PutSN16(buf, o + oT + 6, t[i].w);
                }
                if (c != null)
                {
                    buf[o + oC] = c[i].r; buf[o + oC + 1] = c[i].g; buf[o + oC + 2] = c[i].b; buf[o + oC + 3] = c[i].a;
                }
                if (uvIn01) { PutUN16(buf, o + oU, uv[i].x); PutUN16(buf, o + oU + 2, uv[i].y); }
                else { PutF32(buf, o + oU, uv[i].x); PutF32(buf, o + oU + 4, uv[i].y); }
            }
            mesh.SetVertexBufferData(buf, 0, 0, buf.Length);
        }

        static void PutF32(byte[] b, int o, float f)
        {
            int x = System.BitConverter.SingleToInt32Bits(f);
            b[o] = (byte)x; b[o + 1] = (byte)(x >> 8); b[o + 2] = (byte)(x >> 16); b[o + 3] = (byte)(x >> 24);
        }

        static void PutSN16(byte[] b, int o, float f)
        {
            short x = (short)Mathf.RoundToInt(Mathf.Clamp(f, -1f, 1f) * 32767f);
            b[o] = (byte)x; b[o + 1] = (byte)(x >> 8);
        }

        static void PutUN16(byte[] b, int o, float f)
        {
            ushort x = (ushort)Mathf.RoundToInt(Mathf.Clamp01(f) * 65535f);
            b[o] = (byte)x; b[o + 1] = (byte)(x >> 8);
        }

        static bool HasNormalMap(Material m)
        {
            foreach (var p in m.GetTexturePropertyNames())
            {
                string l = p.ToLowerInvariant();
                if ((l.Contains("bump") || l.Contains("normal")) && m.GetTexture(p) != null) return true;
            }
            return false;
        }

        // ---------- Helpers ----------
        static string TexProp(Material m)
        {
            if (m.HasProperty("_BaseMap")) return "_BaseMap";
            if (m.HasProperty("_MainTex")) return "_MainTex";
            return null;
        }

        static string ColorProp(Material m)
        {
            if (m.HasProperty("_BaseColor")) return "_BaseColor";
            if (m.HasProperty("_Color")) return "_Color";
            return null;
        }

        // Vùng UV mà submesh dùng, làm tròn ra số nguyên (VD UV 0..1.8 -> [0,2]).
        static Rect UvRange(Piece p, Dictionary<Mesh, Vector2[]> cache)
        {
            if (!cache.TryGetValue(p.mesh, out var uv))
                cache[p.mesh] = uv = p.mesh.uv;
            if (uv.Length != p.mesh.vertexCount) return new Rect(0, 0, 1, 1);

            int[] idx = p.mesh.GetTriangles(p.sub);
            if (idx.Length == 0) return new Rect(0, 0, 1, 1);

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (int i in idx)
            {
                min = Vector2.Min(min, uv[i]);
                max = Vector2.Max(max, uv[i]);
            }

            const float eps = 0.001f; // tránh 1.0000001 bị làm tròn thành 2
            float x0 = Mathf.Floor(min.x + eps), x1 = Mathf.Ceil(max.x - eps);
            float y0 = Mathf.Floor(min.y + eps), y1 = Mathf.Ceil(max.y - eps);
            if (x1 <= x0) x1 = x0 + 1;
            if (y1 <= y0) y1 = y0 + 1;
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        Texture2D MakeTile(Texture src, Color tint, Vector2 tiling, Vector2 offset, Rect range)
        {
            if (src == null)
            {
                var solid = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var spx = new Color[16];
                for (int i = 0; i < spx.Length; i++) spx[i] = tint;
                solid.SetPixels(spx);
                solid.Apply();
                return solid;
            }

            // 1. Copy texture gốc (kích thước gốc) ra bản đọc được trên CPU
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            // Giữ đúng wrap mode của texture gốc (Repeat/Clamp/Mirror) như khi render mesh gốc
            copy.wrapModeU = src.wrapModeU;
            copy.wrapModeV = src.wrapModeV;
            copy.filterMode = FilterMode.Bilinear;

            // 2. Tile phủ cả vùng UV range (VD 2x1 ô) -> kích thước tăng theo số ô lặp
            int w = Mathf.Min(Mathf.RoundToInt(src.width * range.width), maxAtlasSize);
            int h = Mathf.Min(Mathf.RoundToInt(src.height * range.height), maxAtlasSize);

            // UV tile 0-1 tương ứng UV gốc range.min..range.max, sau đó áp tiling/offset của material.
            // Lấy mẫu trên CPU: Graphics.Blit(src, rt, mat) tự gán lại _MainTex nên bỏ qua
            // mainTextureScale/Offset -> texture bị kéo giãn thay vì lặp (lỗi cũ).
            Vector2 scale = new Vector2(tiling.x * range.width, tiling.y * range.height);
            Vector2 off = offset + new Vector2(tiling.x * range.x, tiling.y * range.y);

            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = off.y + (y + 0.5f) / h * scale.y;
                for (int x = 0; x < w; x++)
                {
                    float u = off.x + (x + 0.5f) / w * scale.x;
                    px[y * w + x] = copy.GetPixelBilinear(u, v) * tint;
                }
            }
            DestroyImmediate(copy);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace('/', '_').Replace(' ', '_');
        }
    }
}
#endif