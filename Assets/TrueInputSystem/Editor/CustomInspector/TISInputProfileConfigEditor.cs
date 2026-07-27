#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem
{
    [CustomEditor(typeof(TISInputProfileConfig))]
    public class TISInputProfileConfigEditor : UnityEditor.Editor
    {
        private void OnEnable()
        {
            SetComponentIcon();
        }

        private void SetComponentIcon()
        {
            string[] guids = AssetDatabase.FindAssets("Profile t:Texture2D");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
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

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
}
#endif