#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class PhotonVoiceEditorSettingsCheck
{
    [InitializeOnLoadMethod]
    static void OnEditorReady()
    {
#if UNITY_EDITOR
        if (EditorSettingsBootstrapGuard.IsToolingLaunch())
        {
            return;
        }
#endif
        EditorApplication.delayCall += CheckInitialization;
    }

    static void CheckInitialization()
    {
        RunChecks();
        EditorApplication.update += OnEditorUpdate;
    }

    static bool LastRunInBackground = false;

    static void OnEditorUpdate()
    {
        bool checkRequired = false;
        checkRequired = checkRequired || LastRunInBackground != PlayerSettings.runInBackground;
        if (checkRequired)
        {
            RunChecks();
        }
    }

    static void UpdateCachedValues()
    {
        LastRunInBackground = PlayerSettings.runInBackground;
    }

    public static void RunChecks()
    {
#if UNITY_EDITOR
        if (EditorSettingsBootstrapGuard.IsToolingLaunch())
        {
            return;
        }
#endif
        VoiceEditorSettingsChecksScriptable checksList = VoiceEditorSettingsChecksScriptable.FindCheckList();

        EnforceRunInBackground(checksList);
        UpdateCachedValues();
    }


    static void EnforceRunInBackground(VoiceEditorSettingsChecksScriptable checksList)
    {   
        if (PlayerSettings.runInBackground == false)
        {
            if (checksList != null && checksList.enforceRunInBackground == false)
            {
                if (checksList == null || checksList.logPotentialIssuesWithSetttings)
                    Debug.LogWarning($"[PhotonVoice] Based on {checksList.name} configuration, not setting {nameof(PlayerSettings)}.{nameof(PlayerSettings.runInBackground)} to true: may lead to issues.\nYou can remove this message by disabling logPotentialIssuesWithSetttings in VoiceEditorSettingsChecks.");

                return;
            }

            Debug.Log($"[PhotonVoice] Setting {nameof(PlayerSettings)}.{nameof(PlayerSettings.runInBackground)} to true.\n You can prevent this automatic change by disabling enforceRunInBackground in VoiceEditorSettingsChecks.");
            PlayerSettings.runInBackground = true;
        }
        else if(checksList != null && checksList.logSuccessfulSetttingsAnalysis)
        {
            Debug.Log($"[PhotonVoice] Correct setting detected: {nameof(PlayerSettings)}.{nameof(PlayerSettings.runInBackground)} is true.\nYou can remove this message by disabling logSuccessfulSetttingsAnalysis in VoiceEditorSettingsChecks.");
        }
    }
}
#endif
