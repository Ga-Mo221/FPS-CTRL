// ============================================================
//  TrueInputSystem — TISCreateDefaultProfileMenu.cs
//  Menu shortcuts cho Tools → True Input System
// ============================================================

#if UNITY_EDITOR
using UnityEditor;

namespace TrueInputSystem.Editor
{
    public static class TISCreateDefaultProfileMenu
    {
        [MenuItem("Tools/True Input System/Setup", priority = 1)]
        public static void OpenSetupTool()
        {
            TISSetupTool.OpenWindow();
        }

        [MenuItem("Tools/True Input System/Documentation", priority = 15)]
        public static void OpenDocumentation()
        {
            TISDocWindow.OpenWindow();
        }
    }
}
#endif