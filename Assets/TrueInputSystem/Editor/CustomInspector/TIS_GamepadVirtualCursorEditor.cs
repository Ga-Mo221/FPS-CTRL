#if UNITY_EDITOR
namespace TrueInputSystem
{
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(TISGamepadVirtualCursor))]
    public class TISGamepadVirtualCursorEditor : UnityEditor.Editor
    {
        private void OnEnable()
        {
            SetComponentIcon();
        }

        private void SetComponentIcon()
        {
            // Tìm file icon theo tên, không cần biết nằm ở folder nào
            string[] guids = AssetDatabase.FindAssets("gamepadmouse t:Texture2D");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Lọc đúng icon của package TrueInputSystem
                if (path.Contains("TrueInputSystem") && path.Contains("Editor/Icon"))
                {
                    var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (icon != null)
                    {
                        EditorGUIUtility.SetIconForObject(target, icon);
                        break;
                    }
                }
            }
        }
    }
}
#endif