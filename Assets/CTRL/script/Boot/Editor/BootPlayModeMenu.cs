// [Aemy 2026-10-26] FILE MOI.
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Game.Boot.EditorTools
{
    /// <summary>
    /// Menu "Eclipse Strike/Play từ Boot": bat -> nhan Play luon chay tu scene Boot (giong ban build);
    /// tat -> chay scene dang mo nhu cu. Luu theo may (EditorPrefs), mac dinh TAT.
    /// </summary>
    [InitializeOnLoad]
    internal static class BootPlayModeMenu
    {
        private const string PrefKey = "EclipseStrike.PlayFromBoot";
        private const string MenuPath = "Eclipse Strike/Play từ Boot";
        private const string BootScenePath = "Assets/Scenes/Flow game/Boot.unity";

        static BootPlayModeMenu() { EditorApplication.delayCall += Apply; }

        [MenuItem(MenuPath, false, 0)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(PrefKey, !EditorPrefs.GetBool(PrefKey, false));
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            UnityEditor.Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PrefKey, false));
            return true;
        }

        private static void Apply()
        {
            var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
            if (EditorPrefs.GetBool(PrefKey, false))
                EditorSceneManager.playModeStartScene = boot;
            else if (boot != null && EditorSceneManager.playModeStartScene == boot)
                EditorSceneManager.playModeStartScene = null; // chi go neu dang la Boot, khong dung cau hinh khac
        }
    }
}
