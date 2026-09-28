#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem
{
    [InitializeOnLoad]
    public static class ScriptIconSetter
    {
        static ScriptIconSetter()
        {
            EditorApplication.delayCall += ApplyAllIfNeeded;
        }

        [MenuItem("Tools/GaMo ToolKit/True Input System/Set Script Icons")]
        private static void ApplyAllIfNeeded()
        {
            bool anyChanged = false;
            anyChanged |= SetIconIfNeeded<TISDirectionalSkillButton>("directionbuttton");
            anyChanged |= SetIconIfNeeded<TISGamepadUIButton>("GamepadButton");
            anyChanged |= SetIconIfNeeded<TISGamepadUIButtonGroup>("GridGamepadButton");
            anyChanged |= SetIconIfNeeded<TISGamepadVirtualCursor>("gamepadmouse");
            anyChanged |= SetIconIfNeeded<TISInputBridge>("input");
            anyChanged |= SetIconIfNeeded<TISMobileInputSetup>("mobileinput");
            anyChanged |= SetIconIfNeeded<TISMobileSkillButton>("button");
            anyChanged |= SetIconIfNeeded<TISUINavigationContext>("UIbuttongamepadmanager");
            anyChanged |= SetIconIfNeeded<TISInputProfileConfig>("Profile");
            anyChanged |= SetIconIfNeeded<TISTargetScanner>("targetDetec");

            // Chỉ SaveAssets khi thực sự có thay đổi — tránh ghi đĩa vô ích mỗi lần mở Unity.
            if (anyChanged)
                AssetDatabase.SaveAssets();
        }

        private static bool SetIconIfNeeded<T>(string iconSearchTerm) where T : Object
        {
            Texture2D icon = ComponentIconApplier.FindIcon(iconSearchTerm);
            if (icon == null) return false;
            
            string[] guids = AssetDatabase.FindAssets("t:MonoScript");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms == null || ms.GetClass() != typeof(T)) continue;

                var importer = (MonoImporter)AssetImporter.GetAtPath(path);
                
                Texture currentIcon = AssetDatabase.GetCachedIcon(path);
                if (currentIcon == icon)
                    return false;

                importer.SetIcon(icon);
                importer.SaveAndReimport();
                return true;
            }

            return false;
        }
    }
}
#endif