namespace Photon.Voice.Editor
{
    using UnityEngine;
    using UnityEditor;

    /// <summary>
    /// Custom inspector for <see cref="StreamTransportClient"/>.
    /// Mirrors the UnityVoiceClient behaviour: a "Use Voice App Settings" toggle plus a button to
    /// create/open the global VoiceAppSettings (PhotonAppSettings) asset. The local Transport Client
    /// Settings are only shown when the toggle is off.
    /// </summary>
    [CustomEditor(typeof(StreamTransportClient), true)]
    public class StreamTransportClientEditor : UnityEditor.Editor
    {
        private SerializedProperty useSharedAppSettingsSp;
        private SerializedProperty transportClientSettingsSp;

        protected virtual void OnEnable()
        {
            this.useSharedAppSettingsSp = this.serializedObject.FindProperty("UseSharedAppSettings");
            this.transportClientSettingsSp = this.serializedObject.FindProperty("transportClientSettings");
        }

        public override void OnInspectorGUI()
        {
            this.serializedObject.UpdateIfRequiredOrScript();

            this.DisplayAppSettings();

            // Draw every other serialized field, excluding the ones handled manually above.
            DrawPropertiesExcluding(this.serializedObject, "m_Script", "UseSharedAppSettings", "transportClientSettings");

            this.serializedObject.ApplyModifiedProperties();
        }

        protected virtual void DisplayAppSettings()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(this.useSharedAppSettingsSp, new GUIContent("Use Shared App Settings", "Use App Settings from the global VoiceAppSettings (PhotonAppSettings) instead of the local Transport Client Settings"));
            if (GUILayout.Button("Shared App Settings", EditorStyles.miniButton, GUILayout.Width(250)))
            {
                Selection.objects = new Object[] { global::Photon.Voice.PhotonAppSettings.Instance };
                EditorGUIUtility.PingObject(global::Photon.Voice.PhotonAppSettings.Instance);
            }
            EditorGUILayout.EndHorizontal();

            // Only expose the local settings when the global VoiceAppSettings are not used.
            if (this.useSharedAppSettingsSp.boolValue == false)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(this.transportClientSettingsSp, true);
                EditorGUI.indentLevel--;
            }
        }
    }
}