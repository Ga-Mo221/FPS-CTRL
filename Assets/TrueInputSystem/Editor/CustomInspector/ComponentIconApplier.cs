#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem
{
    /// <summary>
    /// Tiện ích dùng chung: tìm icon theo tên + set icon cho object, cache lại để không
    /// quét AssetDatabase nhiều lần. Dùng cho mọi Custom Editor cần gán icon riêng
    /// (TransformConstraintEditor, IKConstraintEditor, DynamicBoneEditor, SimpleBoneRendererEditor...).
    /// </summary>
    public static class ComponentIconApplier
    {
        // Cache theo TỪ KHÓA tìm kiếm, vì mỗi editor tìm icon tên khác nhau
        // ("CopyTransform", "IKConstraint", "spring", "BoneRenderer"...).
        private static readonly System.Collections.Generic.Dictionary<string, Texture2D> _cache
            = new System.Collections.Generic.Dictionary<string, Texture2D>();

        public static Texture2D FindIcon(string searchTerm)
        {
            if (_cache.TryGetValue(searchTerm, out var cached))
                return cached; // đã tìm rồi (kể cả null = "đã tìm nhưng không có")

            Texture2D found = null;
            string[] guids = AssetDatabase.FindAssets($"{searchTerm} t:Texture2D");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("TrueInputSystem") && path.Contains("Editor/Icon"))
                {
                    found = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (found != null) break;
                }
            }

            _cache[searchTerm] = found;
            return found;
        }

        public static void ApplyToObject(Object target, string searchTerm)
        {
            if (target == null) return;
            Texture2D icon = FindIcon(searchTerm);
            if (icon == null) return;

            // Chỉ gán khi khác, tránh ghi thừa mỗi lần gọi.
            if (EditorGUIUtility.GetIconForObject(target) != icon)
                EditorGUIUtility.SetIconForObject(target, icon);
        }

        public static void ApplyToAll<T>(string searchTerm) where T : Object
        {
            Texture2D icon = FindIcon(searchTerm);
            if (icon == null) return;

            // FindObjectsByType quét TOÀN BỘ scene đang mở (không thấy Prefab Mode riêng,
            // nhưng đủ dùng cho việc set icon lúc khởi động — khi vào Prefab Mode và chọn
            // object, OnEnable() của Editor vẫn tự set icon bình thường).
            var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var obj in all)
            {
                if (obj is Object o && EditorGUIUtility.GetIconForObject(o) != icon)
                    EditorGUIUtility.SetIconForObject(o, icon);
            }
        }
    }
}
#endif