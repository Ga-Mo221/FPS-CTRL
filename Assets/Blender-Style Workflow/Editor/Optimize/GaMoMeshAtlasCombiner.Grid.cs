// GaMoMeshAtlasCombiner.Grid.cs  -  by Gà Mờ
// Chế độ Grid: chia map thành các ô, trong mỗi ô gộp các mesh STATIC có CÙNG material
// thành 1 mesh. Không tạo atlas -> giữ nguyên material gốc (normal map, tiling... đúng 100%),
// và mỗi ô vẫn là 1 renderer riêng nên frustum/occlusion culling vẫn hoạt động.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GaMo.BlenderEditorTools
{
    public partial class GaMoMeshAtlasCombiner
    {
        // ---------- Grid settings ----------
        bool gridByCount = true;      // true = chia vùng chứa object thành cellCount ô; false = theo cellSize
        Vector3Int cellCount = new Vector3Int(8, 1, 8);
        Vector3 cellSize = new Vector3(20, 20, 20);
        Vector3 gridOrigin = Vector3.zero;

        // Grid thực tế dùng khi gộp / xem trước (tính trong PrepareGrid)
        Vector3 effOrigin, effSize = Vector3.one;
        Vector3Int effCount;
        bool gridReady;
        bool gridUseY = false;        // false = ô cao vô hạn (chỉ chia theo X/Z)
        bool gridWholeScene = false;  // true = lấy cả scene thay vì Selection
        bool requireStatic = true;
        int minPerCell = 2;           // 1 nhóm cần >= N object mới gộp (gộp 1 cái = tốn thêm bộ nhớ vô ích)
        int mergeBelow = 3;           // ô có < N object -> nhập vào ô lân cận ít object nhất (1 = tắt)
        int mergeSearch = 2;          // tìm ô lân cận trong bán kính bao nhiêu ô
        bool showGridPreview = true;
        bool moveColliders = true;    // chép collider của object gộp sang ô gộp, tắt collider gốc

        const string GridRootSuffix = "_GridRoot";

        class GridUnit
        {
            public GameObject owner;              // object gốc (LODGroup hoặc MeshRenderer)
            public LODGroup lod;                  // null = object thường
            public List<MeshRenderer>[] levels;   // object thường: levels[0] = { renderer }
            public Bounds bounds;
        }

        // 1 nhóm ô sau khi nhập ô thưa: key = ô chính, members = các ô lưới đã nhập vào
        class CellGroup
        {
            public Vector3Int key;
            public List<GridUnit> units = new List<GridUnit>();
            public List<Vector3Int> members = new List<Vector3Int>();
        }

        struct PreviewCell { public Bounds bounds; public int units; public Color color; }
        List<PreviewCell> previewCells = new List<PreviewCell>();

        void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
        void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

        void DrawGridSettings()
        {
            EditorGUI.BeginChangeCheck();
            gridWholeScene = EditorGUILayout.Toggle(new GUIContent("Cả scene",
                "Bật: gộp mọi object trong scene đang mở. Tắt: chỉ các object đang chọn (gồm con)."), gridWholeScene);
            gridUseY = EditorGUILayout.Toggle(new GUIContent("Chia theo trục Y",
                "Tắt: ô cao vô hạn (hợp với map phẳng). Bật: chia cả theo chiều cao (nhà nhiều tầng)."), gridUseY);
            gridByCount = EditorGUILayout.Popup("Chia theo", gridByCount ? 0 : 1,
                new[] { "Số ô (chia đều vùng map)", "Kích thước ô (mét)" }) == 0;
            if (gridByCount)
            {
                cellCount.x = Mathf.Clamp(EditorGUILayout.IntField("Số ô X", cellCount.x), 1, 256);
                if (gridUseY) cellCount.y = Mathf.Clamp(EditorGUILayout.IntField("Số ô Y", cellCount.y), 1, 64);
                cellCount.z = Mathf.Clamp(EditorGUILayout.IntField("Số ô Z", cellCount.z), 1, 256);
                if (gridReady)
                    EditorGUILayout.HelpBox($"Mỗi ô ≈ {effSize.x:F1} x {(gridUseY ? effSize.y.ToString("F1") + " x " : "")}{effSize.z:F1} m", MessageType.None);
            }
            else
            {
                cellSize = EditorGUILayout.Vector3Field("Kích thước ô", cellSize);
                cellSize = Vector3.Max(cellSize, Vector3.one * 0.1f);
                gridOrigin = EditorGUILayout.Vector3Field("Gốc grid", gridOrigin);
                if (gridReady)
                    EditorGUILayout.HelpBox($"Vùng map ≈ {effCount.x} x {(gridUseY ? effCount.y + " x " : "")}{effCount.z} ô", MessageType.None);
            }
            requireStatic = EditorGUILayout.Toggle(new GUIContent("Chỉ object Static",
                "Chỉ gộp object có Static flag. Object động (di chuyển, bị phá...) không được gộp."), requireStatic);
            minPerCell = EditorGUILayout.IntSlider(new GUIContent("Tối thiểu / nhóm",
                "Nhóm (cùng ô + cùng material) có ít object hơn số này thì giữ nguyên object gốc."), minPerCell, 1, 10);
            mergeBelow = EditorGUILayout.IntSlider(new GUIContent("Nhập ô có < N object",
                "Ô có ít object hơn N được nhập vào ô lân cận có ít object nhất. 1 = tắt."), mergeBelow, 1, 20);
            if (mergeBelow > 1)
                mergeSearch = EditorGUILayout.IntSlider(new GUIContent("Bán kính tìm ô lân cận",
                    "Tìm ô có object trong phạm vi bao nhiêu ô xung quanh. Không tìm thấy thì giữ ô riêng."), mergeSearch, 1, 5);
            bool changed = EditorGUI.EndChangeCheck();
            moveColliders = EditorGUILayout.Toggle(new GUIContent("Chuyển collider sang ô gộp",
                "Chép mọi collider (Box/Sphere/Capsule/Mesh...) của object được gộp sang ô gộp, giữ nguyên vị trí, " +
                "layer, tag, trigger, Physic Material; rồi tắt collider gốc. Collider có script đi kèm hoặc thuộc " +
                "Rigidbody được giữ nguyên. Cần bật 'Tắt renderer gốc'."), moveColliders);
            if (GUILayout.Button(new GUIContent("Chuyển collider cho kết quả đã gộp",
                    "Dùng cho cụm *_GridRoot gộp từ trước khi có tùy chọn chuyển collider.")))
                MigrateCollidersToExistingGrid();

            EditorGUI.BeginChangeCheck();
            showGridPreview = EditorGUILayout.Toggle("Xem trước ô trong Scene", showGridPreview);
            if ((EditorGUI.EndChangeCheck() || changed) && showGridPreview) RefreshGridPreview();
            if (!showGridPreview) previewCells.Clear();
            if (showGridPreview && GUILayout.Button("Cập nhật xem trước")) RefreshGridPreview();
            if (previewGroups > 0)
                EditorGUILayout.LabelField($"{previewGroups} ô sau khi nhập" +
                                           (previewMerged > 0 ? $" (đã nhập {previewMerged} ô thưa)" : ""));
        }

        int previewGroups, previewMerged;

        // Chia unit vào ô, rồi nhập các ô thưa vào ô lân cận có ít object nhất
        List<CellGroup> AssignCells(List<GridUnit> units, out int merged)
        {
            var groups = new Dictionary<Vector3Int, CellGroup>();
            foreach (var u in units)
            {
                var key = CellOf(u.bounds.center);
                if (!groups.TryGetValue(key, out var g))
                {
                    groups[key] = g = new CellGroup { key = key };
                    g.members.Add(key);
                }
                g.units.Add(u);
            }

            merged = 0;
            if (mergeBelow <= 1) return new List<CellGroup>(groups.Values);

            // Lặp đến khi không nhập thêm được: ô ít nhất xử lý trước
            bool changed = true;
            while (changed)
            {
                changed = false;
                var sparse = new List<CellGroup>();
                foreach (var g in groups.Values) if (g.units.Count < mergeBelow) sparse.Add(g);
                sparse.Sort((a, b) => a.units.Count.CompareTo(b.units.Count));

                foreach (var g in sparse)
                {
                    if (!groups.ContainsKey(g.key)) continue; // đã bị nhập vào ô khác trong lượt này
                    var target = FindMergeTarget(g, groups);
                    if (target == null) continue;

                    target.units.AddRange(g.units);
                    target.members.AddRange(g.members);
                    groups.Remove(g.key);
                    merged++;
                    changed = true;
                }
            }
            return new List<CellGroup>(groups.Values);
        }

        // Ô lân cận gần nhất (theo vòng) có object; trong cùng vòng chọn ô ít object nhất, hoà thì chọn ô gần hơn
        CellGroup FindMergeTarget(CellGroup g, Dictionary<Vector3Int, CellGroup> groups)
        {
            Vector3 center = g.units[0].bounds.center;
            for (int r = 1; r <= mergeSearch; r++)
            {
                CellGroup best = null;
                float bestDist = float.MaxValue;
                int ry = gridUseY ? r : 0;
                for (int dx = -r; dx <= r; dx++)
                for (int dy = -ry; dy <= ry; dy++)
                for (int dz = -r; dz <= r; dz++)
                {
                    // chỉ xét viền của vòng r (vòng trong đã xét)
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy), Mathf.Abs(dz)) != r) continue;
                    if (!groups.TryGetValue(g.key + new Vector3Int(dx, dy, dz), out var c)) continue;
                    float dist = (c.units[0].bounds.center - center).sqrMagnitude;
                    if (best == null || c.units.Count < best.units.Count ||
                        (c.units.Count == best.units.Count && dist < bestDist))
                    {
                        best = c;
                        bestDist = dist;
                    }
                }
                if (best != null) return best;
            }
            return null;
        }

        IEnumerable<GameObject> GridRoots()
        {
            if (!gridWholeScene) return Selection.gameObjects;
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        }

        // Tính grid thực tế từ vùng bao của tất cả object
        void PrepareGrid(List<GridUnit> units)
        {
            gridReady = units.Count > 0;
            if (!gridReady) return;
            var b = units[0].bounds;
            foreach (var u in units) b.Encapsulate(u.bounds);

            if (gridByCount)
            {
                Vector3Int n = gridUseY ? cellCount : new Vector3Int(cellCount.x, 1, cellCount.z);
                effOrigin = b.min;
                effSize = new Vector3(Mathf.Max(b.size.x / n.x, 0.01f),
                                      Mathf.Max(b.size.y / n.y, 0.01f),
                                      Mathf.Max(b.size.z / n.z, 0.01f));
                effCount = n;
            }
            else
            {
                effOrigin = gridOrigin;
                effSize = cellSize;
                Vector3Int lo = RawCell(b.min), hi = RawCell(b.max);
                effCount = hi - lo + Vector3Int.one;
            }
        }

        Vector3Int RawCell(Vector3 p)
        {
            Vector3 d = p - effOrigin;
            return new Vector3Int(Mathf.FloorToInt(d.x / effSize.x),
                                  gridUseY ? Mathf.FloorToInt(d.y / effSize.y) : 0,
                                  Mathf.FloorToInt(d.z / effSize.z));
        }

        Vector3Int CellOf(Vector3 p)
        {
            var c = RawCell(p);
            // Theo số ô: object nằm đúng mép max vẫn thuộc ô cuối
            if (gridByCount) c = Vector3Int.Min(Vector3Int.Max(c, Vector3Int.zero), effCount - Vector3Int.one);
            return c;
        }

        static bool IsStatic(GameObject go) => GameObjectUtility.GetStaticEditorFlags(go) != 0;

        static bool InsideGridOutput(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name.EndsWith(GridRootSuffix)) return true;
            return false;
        }

        static bool Usable(MeshRenderer mr)
        {
            if (mr == null) return false;
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            foreach (var m in mr.sharedMaterials) if (m == null) return false;
            return true;
        }

        // ---------- Thu thập ----------
        List<GridUnit> CollectGridUnits(out int skippedNonStatic)
        {
            skippedNonStatic = 0;
            var units = new List<GridUnit>();
            var handled = new HashSet<Renderer>();
            var roots = new List<GameObject>(GridRoots());

            // LODGroup: cả nhóm là 1 unit, nằm trong ô theo tâm bounds của nó
            var seenLod = new HashSet<LODGroup>();
            foreach (var root in roots)
            foreach (var lg in root.GetComponentsInChildren<LODGroup>(includeInactive))
            {
                if (!seenLod.Add(lg) || InsideGridOutput(lg.transform)) continue;
                var lods = lg.GetLODs();
                foreach (var l in lods) foreach (var r in l.renderers) if (r != null) handled.Add(r);
                if (!lg.enabled || lods.Length == 0) continue;
                if (requireStatic && !IsStatic(lg.gameObject)) { skippedNonStatic++; continue; }

                var levels = new List<MeshRenderer>[lods.Length];
                bool ok = true, any = false;
                var b = new Bounds();
                for (int L = 0; L < lods.Length && ok; L++)
                {
                    levels[L] = new List<MeshRenderer>();
                    foreach (var r in lods[L].renderers)
                    {
                        if (r == null) continue;
                        // Skinned / renderer lạ trong LOD -> bỏ nguyên LODGroup, tránh tắt nhầm
                        if (!(r is MeshRenderer mr) || !Usable(mr)) { ok = false; break; }
                        levels[L].Add(mr);
                        if (!any) { b = mr.bounds; any = true; } else b.Encapsulate(mr.bounds);
                    }
                }
                if (ok && any) units.Add(new GridUnit { owner = lg.gameObject, lod = lg, levels = levels, bounds = b });
            }

            // Renderer thường (không thuộc LODGroup)
            var seen = new HashSet<MeshRenderer>();
            foreach (var root in roots)
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(includeInactive))
            {
                if (handled.Contains(mr) || !seen.Add(mr) || !mr.enabled || !Usable(mr)) continue;
                if (InsideGridOutput(mr.transform)) continue;
                if (requireStatic && !IsStatic(mr.gameObject)) { skippedNonStatic++; continue; }
                units.Add(new GridUnit
                {
                    owner = mr.gameObject,
                    levels = new[] { new List<MeshRenderer> { mr } },
                    bounds = mr.bounds
                });
            }
            return units;
        }

        void RefreshGridPreview()
        {
            previewCells.Clear();
            var units = CollectGridUnits(out _);
            PrepareGrid(units);
            var groups = AssignCells(units, out previewMerged);
            previewGroups = groups.Count;

            foreach (var g in groups)
            {
                // Chiều cao khung theo nội dung nhóm nếu không chia Y
                var content = g.units[0].bounds;
                foreach (var u in g.units) content.Encapsulate(u.bounds);
                // Mỗi nhóm 1 màu -> nhìn ra ô nào đã nhập vào ô nào
                var color = Color.HSVToRGB(Mathf.Abs(g.key.GetHashCode() * 0.618034f) % 1f, 0.7f, 1f);

                foreach (var k in g.members)
                {
                    Vector3 min = effOrigin + Vector3.Scale(new Vector3(k.x, k.y, k.z), effSize);
                    Vector3 size = effSize;
                    if (!gridUseY) { min.y = content.min.y; size.y = Mathf.Max(content.size.y, 0.1f); }
                    // Chỉ ô chính hiện số object của cả nhóm
                    previewCells.Add(new PreviewCell
                    {
                        bounds = new Bounds(min + size * 0.5f, size),
                        units = k == g.key ? g.units.Count : 0,
                        color = color
                    });
                }
            }
            SceneView.RepaintAll();
        }

        void OnSceneGUI(SceneView sv)
        {
            if (!showGridPreview || mode != 1 || previewCells.Count == 0) return;
            foreach (var c in previewCells)
            {
                Handles.color = c.color;
                Handles.DrawWireCube(c.bounds.center, c.bounds.size);
                if (c.units > 0) Handles.Label(c.bounds.center, c.units.ToString(), EditorStyles.whiteBoldLabel);
            }
        }

        // ---------- Gộp ----------
        static string BucketKey(MeshRenderer mr, Material mat) =>
            mat.GetInstanceID() + "_" + (int)mr.shadowCastingMode + "_" + mr.receiveShadows + "_" +
            mr.gameObject.layer + "_" + mr.renderingLayerMask;

        // Gom piece theo (material + cài đặt renderer), mỗi bucket = 1 mesh gộp
        static Dictionary<string, List<Piece>> Bucket(IEnumerable<MeshRenderer> renderers, int level)
        {
            var buckets = new Dictionary<string, List<Piece>>();
            foreach (var mr in renderers)
            {
                var mf = mr.GetComponent<MeshFilter>();
                var mats = mr.sharedMaterials;
                var mesh = mf.sharedMesh;
                for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                {
                    string key = BucketKey(mr, mats[s]);
                    if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<Piece>();
                    list.Add(new Piece { mf = mf, mr = mr, mesh = mesh, sub = s, mat = mats[s], level = level });
                }
            }
            return buckets;
        }

        void CombineGrid()
        {
            var units = CollectGridUnits(out int skippedNonStatic);
            if (units.Count == 0)
            {
                result = "Không có object hợp lệ" + (requireStatic ? " (cần Static flag)." : ".");
                return;
            }
            PrepareGrid(units);

            // Chia unit vào ô (+ nhập ô thưa vào ô lân cận)
            var cells = AssignCells(units, out int mergedCells);

            string folder = $"{outputFolder}/{Sanitize(outputName)}_Grid";
            Directory.CreateDirectory(folder);
            var log = new System.Text.StringBuilder();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            var root = new GameObject(outputName + GridRootSuffix);
            Undo.RegisterCreatedObjectUndo(root, "GaMo Grid Combine");

            int rendBefore = 0, rendAfter = 0, cellsUsed = 0, meshCount = 0, lodClamped = 0;
            var colStats = new ColliderStats();
            long vertexBytes = 0;
            var toDisable = new List<GridUnit>();

            try
            {
                AssetDatabase.StartAssetEditing();
                int ci = 0;
                foreach (var group in cells)
                {
                    EditorUtility.DisplayProgressBar("GaMo Grid", $"Ô {group.key}", (float)ci++ / cells.Count);
                    var k = group.key;
                    var cellUnits = group.units;

                    var plain = cellUnits.FindAll(u => u.lod == null);
                    var lodUnits = cellUnits.FindAll(u => u.lod != null);

                    // Object thường: bỏ những object mà mọi bucket của nó đều quá ít thành viên
                    var plainBuckets = Bucket(plain.ConvertAll(u => u.levels[0][0]), -1);
                    plain = plain.FindAll(u => WorthCombining(u.levels[0][0], plainBuckets));
                    plainBuckets = Bucket(plain.ConvertAll(u => u.levels[0][0]), -1);
                    if (lodUnits.Count < minPerCell) lodUnits.Clear();
                    if (plain.Count == 0 && lodUnits.Count == 0) continue;

                    // Pivot = tâm nội dung ô (vertex local nhỏ -> chính xác hơn, bounds gọn)
                    var b = new Bounds();
                    bool first = true;
                    foreach (var u in plain) { if (first) { b = u.bounds; first = false; } else b.Encapsulate(u.bounds); }
                    foreach (var u in lodUnits) { if (first) { b = u.bounds; first = false; } else b.Encapsulate(u.bounds); }
                    Vector3 pivot = b.center;
                    Matrix4x4 toLocal = Matrix4x4.Translate(-pivot);

                    var cellGo = new GameObject($"Cell_{k.x}_{k.y}_{k.z}");
                    cellGo.transform.SetParent(root.transform, false);
                    cellGo.transform.position = pivot;
                    Undo.RegisterCreatedObjectUndo(cellGo, "GaMo Grid Combine");
                    cellsUsed++;
                    string prefix = $"{outputName}_C{k.x}_{k.y}_{k.z}";

                    // --- Object thường ---
                    foreach (var bucket in plainBuckets.Values)
                    {
                        MakeCombined(bucket, cellGo.transform, $"{prefix}_{bucket[0].mat.name}", toLocal, folder, ref vertexBytes);
                        rendAfter++; meshCount++;
                    }
                    rendBefore += plain.Count;
                    toDisable.AddRange(plain);

                    // --- LODGroup: 1 LODGroup mới cho cả ô ---
                    if (lodUnits.Count > 0)
                    {
                        int levelCount = 0;
                        LODGroup refGroup = null;
                        foreach (var u in lodUnits)
                            if (u.levels.Length > levelCount) { levelCount = u.levels.Length; refGroup = u.lod; }

                        var lodGo = new GameObject("LOD");
                        lodGo.transform.SetParent(cellGo.transform, false);
                        Undo.RegisterCreatedObjectUndo(lodGo, "GaMo Grid Combine");

                        var levelRenderers = new List<Renderer>[levelCount];
                        for (int L = 0; L < levelCount; L++)
                        {
                            levelRenderers[L] = new List<Renderer>();
                            var rs = new List<MeshRenderer>();
                            // Group ít LOD hơn thì dùng lại mức cuối của nó
                            foreach (var u in lodUnits) rs.AddRange(u.levels[Mathf.Min(L, u.levels.Length - 1)]);
                            foreach (var bucket in Bucket(rs, L).Values)
                            {
                                var mr = MakeCombined(bucket, lodGo.transform, $"{prefix}_{bucket[0].mat.name}_LOD{L}", toLocal, folder, ref vertexBytes);
                                levelRenderers[L].Add(mr);
                                meshCount++;
                                if (L == 0) rendAfter++;
                            }
                        }
                        if (BuildGridLodGroup(lodGo, refGroup, lodUnits, levelRenderers)) lodClamped++;
                        foreach (var u in lodUnits) rendBefore += u.levels[0].Count;
                        toDisable.AddRange(lodUnits);
                    }

                    // Static flag cho object cha = phần chung của các object gốc
                    var flags = CommonFlags(plain, lodUnits);
                    GameObjectUtility.SetStaticEditorFlags(cellGo, flags);
                    foreach (Transform t in cellGo.transform)
                        if (t.name == "LOD") GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);

                    if (moveColliders && disableOriginals)
                    {
                        var owners = plain.ConvertAll(u => u.owner);
                        owners.AddRange(lodUnits.ConvertAll(u => u.owner));
                        MoveColliders(cellGo.transform, owners, colStats);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            if (disableOriginals)
            {
                // Chỉ tắt renderer (và LODGroup), KHÔNG tắt GameObject: collider, script... vẫn chạy
                foreach (var u in toDisable)
                {
                    if (u.lod != null) { Undo.RecordObject(u.lod, "GaMo Grid Combine"); u.lod.enabled = false; }
                    foreach (var lvl in u.levels)
                        foreach (var mr in lvl)
                        {
                            Undo.RecordObject(mr, "GaMo Grid Combine");
                            mr.enabled = false;
                        }
                }
            }
            Undo.CollapseUndoOperations(undoGroup);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = root;
            if (showGridPreview) RefreshGridPreview();

            log.AppendLine($"- Grid {effCount.x} x {effCount.y} x {effCount.z}, mỗi ô ≈ {effSize.x:F1} x {effSize.z:F1} m.");
            if (mergedCells > 0)
                log.AppendLine($"- Nhập {mergedCells} ô có < {mergeBelow} object vào ô lân cận -> còn {cells.Count} ô.");
            log.AppendLine($"- {cellsUsed} ô được gộp, {meshCount} mesh mới ({vertexBytes / 1024} KB vertex data).");
            log.AppendLine($"- Renderer (LOD0): {rendBefore} -> {rendAfter}.");
            if (colStats.moved + colStats.kept > 0) log.AppendLine("- " + colStats);
            log.AppendLine($"- Giữ nguyên: {units.Count - toDisable.Count} object (nhóm < {minPerCell}).");
            if (skippedNonStatic > 0) log.AppendLine($"- Bỏ qua {skippedNonStatic} object không Static.");
            if (lodClamped > 0)
                log.AppendLine($"! {lodClamped} ô có LODGroup to hơn nhiều so với object gốc: LOD cao sẽ hiện gần hơn bình thường. " +
                               "Giảm kích thước ô nếu muốn chuyển LOD giống hệt bản gốc.");
            if ((CommonFlagsAll(toDisable) & StaticEditorFlags.ContributeGI) != 0)
                log.AppendLine("! Object gốc có Contribute GI (lightmap). Mesh gộp không có lightmap UV nên được đặt nhận sáng " +
                               "bằng Light Probe: nên đặt Light Probe Group cho map rồi bake lại.");
            result = log.ToString();
            Debug.Log("[GaMo Grid]\n" + result);
        }

        bool WorthCombining(MeshRenderer mr, Dictionary<string, List<Piece>> buckets)
        {
            foreach (var m in mr.sharedMaterials)
            {
                var list = buckets[BucketKey(mr, m)];
                var owners = new HashSet<MeshRenderer>();
                foreach (var p in list) owners.Add(p.mr);
                if (owners.Count >= minPerCell) return true;
            }
            return false;
        }

        MeshRenderer MakeCombined(List<Piece> bucket, Transform parent, string name, Matrix4x4 toLocal,
                                  string folder, ref long vertexBytes)
        {
            name = Sanitize(name);
            var mesh = BuildMesh(bucket, null, name, out _, toLocal, folder);
            vertexBytes += (long)mesh.vertexCount * mesh.GetVertexBufferStride(0);

            var src = bucket[0].mr;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = src.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = bucket[0].mat;
            mr.shadowCastingMode = src.shadowCastingMode;
            mr.receiveShadows = src.receiveShadows;
            mr.renderingLayerMask = src.renderingLayerMask;
            mr.lightProbeUsage = src.lightProbeUsage;
            mr.reflectionProbeUsage = src.reflectionProbeUsage;
            mr.staticShadowCaster = src.staticShadowCaster;

            var flags = (StaticEditorFlags)~0;
            foreach (var p in bucket) flags &= GameObjectUtility.GetStaticEditorFlags(p.mr.gameObject);
            GameObjectUtility.SetStaticEditorFlags(go, flags & ~StripFlags);

            // Không lightmap (không có UV2) -> nhận sáng gián tiếp từ Light Probe / ambient
            mr.receiveGI = ReceiveGI.LightProbes;
            if (mr.lightProbeUsage == LightProbeUsage.Off) mr.lightProbeUsage = LightProbeUsage.BlendProbes;

            Undo.RegisterCreatedObjectUndo(go, "GaMo Grid Combine");
            return mr;
        }

        // Cờ phải bỏ trên mesh gộp:
        // - BatchingStatic: mesh đã gộp sẵn; Static Batching khi Play không hỗ trợ vertex nén (SNorm16)
        //   -> "Cannot combine instance ... unsupported vertex normal format" và vẽ ra đen thui.
        // - ContributeGI: mesh gộp không có lightmap UV (UV2), bake lightmap sẽ sai.
        const StaticEditorFlags StripFlags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI;

        static StaticEditorFlags CommonFlags(List<GridUnit> a, List<GridUnit> b)
        {
            var all = new List<GridUnit>(a);
            all.AddRange(b);
            return CommonFlagsAll(all) & ~StripFlags;
        }

        static StaticEditorFlags CommonFlagsAll(List<GridUnit> units)
        {
            if (units.Count == 0) return 0;
            var flags = (StaticEditorFlags)~0;
            foreach (var u in units) flags &= GameObjectUtility.GetStaticEditorFlags(u.owner);
            return flags;
        }

        // LODGroup của ô to hơn nhiều so với từng object gốc. Ngưỡng LOD là % chiều cao màn hình
        // tính theo size của LODGroup, nên nếu copy nguyên ngưỡng thì ô to sẽ giữ LOD0 xa hơn nhiều.
        // Nhân ngưỡng theo tỉ lệ size mới / size gốc để chuyển LOD ở đúng khoảng cách như cũ.
        static bool BuildGridLodGroup(GameObject go, LODGroup refGroup, List<GridUnit> units, List<Renderer>[] levelRenderers)
        {
            var lg = go.AddComponent<LODGroup>();
            var refLods = refGroup.GetLODs();
            int count = levelRenderers.Length;
            var lods = new LOD[count];
            for (int L = 0; L < count; L++)
                lods[L] = new LOD(refLods[L].screenRelativeTransitionHeight, levelRenderers[L].ToArray())
                { fadeTransitionWidth = refLods[L].fadeTransitionWidth };
            lg.SetLODs(lods);
            lg.fadeMode = refGroup.fadeMode;
            lg.animateCrossFading = refGroup.animateCrossFading;
            lg.RecalculateBounds();

            float origSize = 0;
            foreach (var u in units) origSize += WorldSize(u.lod);
            origSize /= units.Count;
            float ratio = origSize > 0 ? WorldSize(lg) / origSize : 1f;

            // Ngưỡng > 1 không biểu diễn được -> ép xuống dưới 1. Chỉ làm LOD cao hiện gần hơn (đẹp hơn),
            // các mức thấp + khoảng cách cull vẫn đúng. Bị ép nhiều = ô quá to so với object.
            float prev = 1f;
            bool clamped = false;
            for (int L = 0; L < count; L++)
            {
                float want = lods[L].screenRelativeTransitionHeight * ratio;
                float h = Mathf.Min(want, prev * 0.98f);
                clamped |= h < want - 0.001f;
                lods[L].screenRelativeTransitionHeight = h;
                prev = h;
            }
            lg.SetLODs(lods);
            return clamped;
        }

        static float WorldSize(LODGroup lg)
        {
            Vector3 s = lg.transform.lossyScale;
            return lg.size * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
    }
}
#endif
