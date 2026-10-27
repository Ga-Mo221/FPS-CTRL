using Fusion;
using Photon.Voice.Unity;
using UnityEditor;
using UnityEngine;

namespace Photon.Voice.Fusion.Editor
{
    public static class FusionVoiceSetupAssistant
    {
        [MenuItem("GameObject/Fusion/Voice/Add Voice to NetworkObject", false, 1)]
        public static void AddNetworkingToScene() {
            
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                EditorUtility.DisplayDialog("Operation Failed", "Please select a GameObject in the Hierarchy first.","OK");
                return;
            }
            
            var networkObject = selectedObject.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                EditorUtility.DisplayDialog("Operation Failed", "Selected GameObject must have a NetworkObject component.","OK");
                return;
            }
            
            GameObject voiceNetworkObject = new GameObject("VoiceNetworkObject");
            Undo.RegisterCreatedObjectUndo(voiceNetworkObject, "Create VoiceNetworkObject");
            Undo.SetTransformParent(voiceNetworkObject.transform, selectedObject.transform, "Parent");
            

            Undo.AddComponent<VoiceNetworkObject>(voiceNetworkObject);
            Undo.AddComponent<Recorder>(voiceNetworkObject);
            Undo.AddComponent<Speaker>(voiceNetworkObject);
            
            Selection.activeGameObject = voiceNetworkObject;
        }
    }
}