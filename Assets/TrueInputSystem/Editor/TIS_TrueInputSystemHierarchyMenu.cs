using UnityEngine;
using UnityEditor;

namespace TrueInputSystem
{
    public static class TISHierarchyMenu
    {
        // Priority dưới 10 để hiện trong nhóm đầu của context menu
        private const int MENU_PRIORITY = 10;

        [MenuItem("GameObject/True Input System/TIS Setup", false, MENU_PRIORITY)]
        public static void CreateTISSetup(MenuCommand menuCommand)
        {
            // Tạo GameObject rỗng
            GameObject go = new GameObject("TIS Setup");

            // Thêm component TISInputBridge
            go.AddComponent<TISInputBridge>();

            // Đăng ký với Undo system
            Undo.RegisterCreatedObjectUndo(go, "Create TIS Setup");

            // Gắn vào parent nếu có (khi right-click vào object trong Hierarchy)
            GameObject parent = menuCommand.context as GameObject;
            if (parent != null)
            {
                GameObjectUtility.SetParentAndAlign(go, parent);
            }

            // Select object vừa tạo
            Selection.activeObject = go;
        }

        [MenuItem("GameObject/True Input System/Mobile Input Setup", false, MENU_PRIORITY)]
        public static void CreateMobileInputSetup(MenuCommand menuCommand)
        {
            // Tạo GameObject rỗng
            GameObject go = new GameObject("Mobile Input Setup");

            // Thêm component TISMobileInputSetup
            go.AddComponent<TISMobileInputSetup>();

            // Đăng ký với Undo system
            Undo.RegisterCreatedObjectUndo(go, "Create Mobile Input Setup");

            // Gắn vào parent nếu có
            GameObject parent = menuCommand.context as GameObject;
            if (parent != null)
            {
                GameObjectUtility.SetParentAndAlign(go, parent);
            }

            // Select object vừa tạo
            Selection.activeObject = go;
        }

        // Validation - chỉ hiện menu khi đang ở Edit mode (tuỳ chọn)
        [MenuItem("GameObject/True Input System/TIS Setup", true)]
        public static bool ValidateCreateTISSetup()
        {
            return !Application.isPlaying;
        }

        [MenuItem("GameObject/True Input System/Mobile Input Setup", true)]
        public static bool ValidateCreateMobileInputSetup()
        {
            return !Application.isPlaying;
        }
    }
}
