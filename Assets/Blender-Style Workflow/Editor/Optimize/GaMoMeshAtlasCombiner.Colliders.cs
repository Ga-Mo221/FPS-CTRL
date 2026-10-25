// GaMoMeshAtlasCombiner.Colliders.cs  -  by Gà Mờ
// Chế độ Grid: chép collider của các object đã gộp sang ô gộp (child "Colliders"), rồi tắt collider gốc.
// Giữ mọi loại collider (Box/Sphere/Capsule/Mesh...) với đầy đủ thông số nhờ CopyComponent/PasteComponentAsNew.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public partial class GaMoMeshAtlasCombiner
    {
        class ColliderStats
        {
            public int moved, kept;
            public readonly Dictionary<string, int> byType = new Dictionary<string, int>();
            public readonly HashSet<Collider> seen = new HashSet<Collider>(); // owner lồng nhau -> không chép 2 lần

            public override string ToString()
            {
                var parts = new List<string>();
                foreach (var kv in byType) parts.Add($"{kv.Key} {kv.Value}");
                return $"Collider: chuyển {moved} ({string.Join(", ", parts)})" +
                       (kept > 0 ? $", giữ nguyên {kept} (có script / Rigidbody)." : ".");
            }
        }

        // Collider chuyển được: collider tĩnh thuần tuý. Có script (trigger, cửa, nhặt đồ...) hoặc Rigidbody
        // thì giữ nguyên ở object gốc vì logic game có thể đang dùng chính collider đó.
        static bool CanMoveCollider(Collider c)
        {
            if (c is CharacterController || c is TerrainCollider || c is WheelCollider) return false;
            if (c.GetComponentInParent<Rigidbody>(true) != null) return false;
            if (c.GetComponentInParent<ArticulationBody>(true) != null) return false;
            return c.GetComponent<MonoBehaviour>() == null;
        }

        void MoveColliders(Transform cell, IEnumerable<GameObject> owners, ColliderStats stats)
        {
            Transform holderRoot = cell.Find("Colliders");
            var holderOf = new Dictionary<GameObject, GameObject>();

            foreach (var owner in owners)
            foreach (var col in owner.GetComponentsInChildren<Collider>(false))
            {
                if (!col.enabled || !stats.seen.Add(col)) continue;
                if (!CanMoveCollider(col)) { stats.kept++; continue; }

                if (holderRoot == null)
                {
                    holderRoot = new GameObject("Colliders").transform;
                    holderRoot.SetParent(cell, false);
                    Undo.RegisterCreatedObjectUndo(holderRoot.gameObject, "GaMo Grid Colliders");
                }

                // 1 holder / 1 GameObject gốc: giữ đúng world transform để center/size/radius của
                // Box/Sphere/Capsule (định nghĩa theo local space) vẫn đúng hình dạng & vị trí.
                var src = col.gameObject;
                if (!holderOf.TryGetValue(src, out var holder))
                {
                    holder = new GameObject(src.name + "_Col");
                    holder.transform.SetParent(holderRoot, false);
                    holder.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
                    Vector3 ps = holderRoot.lossyScale, s = src.transform.lossyScale;
                    holder.transform.localScale = new Vector3(s.x / ps.x, s.y / ps.y, s.z / ps.z);
                    holder.layer = src.layer;   // layer quyết định va chạm / raycast
                    holder.tag = src.tag;
                    GameObjectUtility.SetStaticEditorFlags(holder, GameObjectUtility.GetStaticEditorFlags(src) & ~StripFlags);
                    Undo.RegisterCreatedObjectUndo(holder, "GaMo Grid Colliders");
                    holderOf[src] = holder;
                }

                ComponentUtility.CopyComponent(col);
                ComponentUtility.PasteComponentAsNew(holder);

                Undo.RecordObject(col, "GaMo Grid Colliders");
                col.enabled = false;

                stats.moved++;
                string type = col.GetType().Name.Replace("Collider", "");
                stats.byType[type] = stats.byType.TryGetValue(type, out int n) ? n + 1 : 1;
            }
        }

        // Cho cụm *_GridRoot đã gộp từ trước: tìm object gốc đã bị tắt renderer / LODGroup,
        // gán vào ô gộp chứa nó (theo bounds), rồi chuyển collider như khi gộp.
        void MigrateCollidersToExistingGrid()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var cells = new List<(Transform t, Bounds b)>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.name.EndsWith(GridRootSuffix)) continue;
                foreach (Transform cell in root.transform)
                {
                    var rs = cell.GetComponentsInChildren<MeshRenderer>(true);
                    if (rs.Length == 0) continue;
                    var b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    cells.Add((cell, b));
                }
            }
            if (cells.Count == 0) { result = "Không tìm thấy cụm *_GridRoot nào trong scene."; return; }

            // Object gốc đã gộp = LODGroup bị tắt, hoặc MeshRenderer bị tắt (không thuộc LODGroup)
            var owners = new List<(GameObject go, Bounds b)>();
            var handled = new HashSet<Renderer>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name.EndsWith(GridRootSuffix)) continue;
                foreach (var lg in root.GetComponentsInChildren<LODGroup>(false))
                {
                    var rs = new List<Renderer>();
                    foreach (var l in lg.GetLODs()) foreach (var r in l.renderers) if (r != null) { rs.Add(r); handled.Add(r); }
                    if (lg.enabled || rs.Count == 0) continue;
                    var b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    owners.Add((lg.gameObject, b));
                }
                foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(false))
                    if (!mr.enabled && !handled.Contains(mr) && mr.GetComponent<MeshFilter>() != null)
                        owners.Add((mr.gameObject, mr.bounds));
            }

            var perCell = new Dictionary<Transform, List<GameObject>>();
            foreach (var (go, b) in owners)
            {
                // Ô chứa tâm object (ô nhỏ nhất nếu chồng nhau), không có thì ô gần nhất
                Transform best = null;
                float bestScore = float.MaxValue;
                foreach (var (t, cb) in cells)
                {
                    float score = cb.Contains(b.center)
                        ? cb.size.sqrMagnitude
                        : 1e9f + (cb.center - b.center).sqrMagnitude;
                    if (score < bestScore) { bestScore = score; best = t; }
                }
                if (!perCell.TryGetValue(best, out var list)) perCell[best] = list = new List<GameObject>();
                list.Add(go);
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var stats = new ColliderStats();
            foreach (var kv in perCell) MoveColliders(kv.Key, kv.Value, stats);
            Undo.CollapseUndoOperations(group);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            result = $"Đã xử lý {owners.Count} object gốc trong {perCell.Count} ô.\n" + stats;
            Debug.Log("[GaMo Grid]\n" + result);
        }
    }
}
#endif
