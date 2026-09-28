#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    [InitializeOnLoad]
    public static class ScriptIconSetter
    {
        static ScriptIconSetter()
        {
            EditorApplication.delayCall += ApplyAllIfNeeded;
        }

        [MenuItem("Tools/GaMo ToolKit/Blender Style Workflow/Set Script Icons")]
        private static void ApplyAllIfNeeded()
        {
            bool anyChanged = false;
            anyChanged |= SetIconIfNeeded<DynamicBone>("spring");
            anyChanged |= SetIconIfNeeded<TransformConstraint>("CopyTransform");
            anyChanged |= SetIconIfNeeded<IKConstraint>("IKConstraint");
            anyChanged |= SetIconIfNeeded<SimpleBoneRenderer>("BoneRenderer");
            anyChanged |= SetIconIfNeeded<MotionTimeEventBehaviour>("Event");
            anyChanged |= SetIconIfNeeded<DynamicBoneCollider>("DynamicBoneCollider");

            // Chỉ SaveAssets khi thực sự có thay đổi — tránh ghi đĩa vô ích mỗi lần mở Unity.
            if (anyChanged)
            {
                AssetDatabase.SaveAssets();
                // MonoImporter.SetIcon() đôi khi không khiến Hierarchy tự repaint ngay lập tức
                // (icon bị cache theo frame) — ép vẽ lại để thấy kết quả tức thì thay vì phải
                // click chọn lại object hoặc đợi Unity tự repaint ở sự kiện khác.
                EditorApplication.RepaintHierarchyWindow();
            }
        }

        private static bool SetIconIfNeeded<T>(string iconSearchTerm) where T : Object
        {
            Texture2D icon = ComponentIconApplier.FindIcon(iconSearchTerm);
            if (icon == null)
            {
                Debug.LogWarning($"[ScriptIconSetter] Không tìm thấy icon texture cho từ khóa " +
                                  $"'{iconSearchTerm}' (dùng cho {typeof(T).Name}). Kiểm tra lại tên " +
                                  $"file texture và đường dẫn (phải nằm trong thư mục chứa " +
                                  $"'Editor/Icon' hoặc 'Blender-Style Workflow').");
                return false;
            }

            string scriptPath = FindScriptPath<T>();
            if (string.IsNullOrEmpty(scriptPath))
            {
                Debug.LogWarning($"[ScriptIconSetter] Không tìm thấy file .cs định nghĩa class " +
                                  $"{typeof(T).FullName} (tên file MonoScript phải trùng tên class). " +
                                  $"Không thể set icon Hierarchy cho type này.");
                return false;
            }

            var importer = AssetImporter.GetAtPath(scriptPath) as MonoImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[ScriptIconSetter] '{scriptPath}' không phải MonoImporter hợp lệ.");
                return false;
            }

            Texture currentIcon = AssetDatabase.GetCachedIcon(scriptPath);
            if (currentIcon == icon)
                return false; // đã đúng icon từ trước, không cần set lại

            importer.SetIcon(icon);
            importer.SaveAndReimport();
            Debug.Log($"[ScriptIconSetter] Đã set icon '{iconSearchTerm}' cho {typeof(T).Name} " +
                       $"tại '{scriptPath}'.");
            return true;
        }

        /// <summary>
        /// Tìm đường dẫn file .cs định nghĩa type T. Ưu tiên tìm nhanh theo TÊN CLASS trước
        /// (AssetDatabase.FindAssets hỗ trợ lọc theo tên file, rất nhanh dù project lớn); chỉ
        /// fallback sang quét TOÀN BỘ MonoScript trong project (chậm hơn, load từng asset để so
        /// sánh GetClass()) nếu cách nhanh không ra kết quả — dùng cho trường hợp hiếm khi tên
        /// file không trùng tên class (VD do người dùng đổi tên file thủ công sau khi tạo).
        /// </summary>
        private static string FindScriptPath<T>()
        {
            string typeName = typeof(T).Name;

            // Bước 1: tìm nhanh theo tên file trùng tên class.
            string[] fastGuids = AssetDatabase.FindAssets($"{typeName} t:MonoScript");
            foreach (var guid in fastGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // FindAssets lọc theo TỪ KHÓA trong tên, không phải khớp CHÍNH XÁC — vẫn cần
                // load MonoScript và so GetClass() để chắc chắn đúng type (tránh nhầm VD
                // "DynamicBone" khớp luôn cả "DynamicBoneCollider", "DynamicBoneEditor"...).
                MonoScript ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms != null && ms.GetClass() == typeof(T))
                    return path;
            }

            // Bước 2: fallback quét toàn bộ (chỉ chạy nếu bước 1 không tìm thấy).
            string[] allGuids = AssetDatabase.FindAssets("t:MonoScript");
            foreach (var guid in allGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms != null && ms.GetClass() == typeof(T))
                    return path;
            }

            return null;
        }
    }
}
#endif