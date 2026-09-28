#if UNITY_EDITOR
namespace GaMo.TrueInputSystem
{
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(TISGamepadVirtualCursor))]
    [InitializeOnLoad]
    public class TISGamepadVirtualCursorEditor : UnityEditor.Editor
    {
        
        static TISGamepadVirtualCursorEditor()
        {
            EditorApplication.delayCall += ApplyIconToAll;
            EditorApplication.hierarchyChanged -= ApplyIconToAll;
            EditorApplication.hierarchyChanged += ApplyIconToAll;
        }
        private static void ApplyIconToAll()
            => ComponentIconApplier.ApplyToAll<TISGamepadVirtualCursor>("gamepadmouse");

        // private void OnEnable()
        // {
        //     SetComponentIcon();
        // }
        //
        // private void SetComponentIcon() => ComponentIconApplier.ApplyToObject(target, "gamepadmouse");
    }
}
#endif