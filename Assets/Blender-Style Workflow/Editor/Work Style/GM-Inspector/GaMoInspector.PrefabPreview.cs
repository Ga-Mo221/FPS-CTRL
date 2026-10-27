// GaMoInspector.PrefabPreview.cs  -  part of CustomsEditor (GaMo Inspector)
// Preview model cho Prefab o che do GameObject: prefab asset chon trong Project va prefab instance trong Scene.
// Dung GameObjectInspector goc cua Unity (tao editor cho prefab ASSET) => dung lai khung Preview o day cua so
// (keo doi chieu cao, tach ra Preview Window goc, xoay / zoom bang chuot nhu Inspector goc).
#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UEditor = UnityEditor.Editor;
using UObject = UnityEngine.Object;

namespace GaMo.BlenderEditorTools
{
    public partial class CustomsEditor
    {
        [NonSerialized] UEditor _pfPrevEditor;            // GameObjectInspector của prefab asset nguồn (chỉ dùng để vẽ Preview)
        [NonSerialized] GameObject _pfPrevGo;             // object đang chọn lúc tính nguồn lần cuối (cache, tránh gọi PrefabUtility mỗi nhịp)
        [NonSerialized] int _pfPrevVer = -1;              // _structVer lúc tính (unpack / đổi hierarchy => tính lại)

        // Prefab asset cần preview cho object đang chọn, null nếu không có / không có Renderer.
        // Asset trong Project: chính file prefab của nó (kiểm tra TRƯỚC instance: root của prefab Variant cũng là "instance" của prefab gốc).
        // Instance trong Scene: prefab của root instance gần nhất (chọn object con vẫn thấy cả model).
        static GameObject PrefabPreviewSource(GameObject go)
        {
            if (go == null) return null;
            string path = null;
            if (EditorUtility.IsPersistent(go)) path = AssetDatabase.GetAssetPath(go);
            else if (PrefabUtility.IsPartOfPrefabInstance(go)) path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (string.IsNullOrEmpty(path)) return null;
            var src = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
            if (src == null || src.GetComponentInChildren<Renderer>(true) == null) return null;
            return src;
        }

        // Gọi ở SyncUI (chế độ GameObject): dựng / hủy editor preview theo object đang chọn
        void EnsurePrefabPreview()
        {
            if (_reloading) return;
            if (_pfPrevGo == _go && _pfPrevVer == _structVer && (_pfPrevEditor == null || _pfPrevEditor.target != null)) return;
            _pfPrevGo = _go;
            _pfPrevVer = _structVer;

            var src = _gos.Count == 1 ? PrefabPreviewSource(_go) : null; // chọn nhiều object: không preview
            if (_pfPrevEditor != null && (_pfPrevEditor.target == null || _pfPrevEditor.target != src)) DestroyPrefabPreviewEditor();
            if (src != null && _pfPrevEditor == null) _pfPrevEditor = UEditor.CreateEditor(src);
        }

        void DestroyPrefabPreview()
        {
            DestroyPrefabPreviewEditor();
            _pfPrevGo = null;
            _pfPrevVer = -1;
        }

        void DestroyPrefabPreviewEditor()
        {
            var ed = _pfPrevEditor;
            _pfPrevEditor = null;
            if (ed != null) DestroyImmediate(ed);
        }
    }
}
#endif
