// GaMoCompactMeshGuard.cs  -  by Gà Mờ
// Mesh gộp của GaMo Combiner dùng vertex nén (SNorm16/UNorm16). Static Batching của Unity không đọc được
// format này -> "Cannot combine instance ... unsupported vertex normal format" và vẽ ra đen thui.
// Mỗi khi 1 scene được xử lý (bấm Play, load scene khi đang Play, Build) -> bỏ cờ Batching Static trên
// các renderer dùng mesh nén, trước khi Static Batching chạy. Không sửa file scene gốc.
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GaMo.BlenderEditorTools
{
    class GaMoCompactMeshGuard : IProcessSceneWithReport
    {
        public int callbackOrder => -1000; // chạy sớm

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            int fixedCount = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = mf.gameObject;
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                if ((flags & StaticEditorFlags.BatchingStatic) == 0 || !IsCompact(mf.sharedMesh)) continue;
                GameObjectUtility.SetStaticEditorFlags(go, flags & ~StaticEditorFlags.BatchingStatic);
                fixedCount++;
            }
            if (fixedCount > 0)
                Debug.Log($"[GaMo] {scene.name}: bỏ Batching Static trên {fixedCount} mesh vertex nén (Static Batching không hỗ trợ).");
        }

        static bool IsCompact(Mesh m)
        {
            if (m == null) return false;
            foreach (var a in m.GetVertexAttributes())
                if ((a.attribute == VertexAttribute.Normal || a.attribute == VertexAttribute.Tangent) &&
                    a.format != VertexAttributeFormat.Float32)
                    return true;
            return false;
        }
    }
}
#endif
