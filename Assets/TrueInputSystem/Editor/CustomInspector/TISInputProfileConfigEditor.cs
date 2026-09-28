#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem
{
    [CustomEditor(typeof(TISInputProfileConfig))]
    [InitializeOnLoad]
    public class TISInputProfileConfigEditor : UnityEditor.Editor
    {
        
        static TISInputProfileConfigEditor()
        {
            EditorApplication.delayCall += ApplyIconToAll;
            EditorApplication.hierarchyChanged -= ApplyIconToAll;
            EditorApplication.hierarchyChanged += ApplyIconToAll;
        }
        private static void ApplyIconToAll()
            => ComponentIconApplier.ApplyToAll<TISInputProfileConfig>("Profile");

        
        private void OnEnable()
        {
            SetComponentIcon();
        }

        private void SetComponentIcon() => ComponentIconApplier.ApplyToObject(target, "Profile");

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
}
#endif