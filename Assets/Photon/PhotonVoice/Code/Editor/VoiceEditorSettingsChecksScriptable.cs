using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


#if UNITY_EDITOR
using UnityEditor;

public static class EditorSettingsBootstrapGuard
{
    public static bool IsParamInCli(string param)
    {
        var args = Environment.GetCommandLineArgs();
        if (args != null)
        {
            foreach(var a in args)
            {
                if(a == param)
                {
                    return true;
                }
            }
        }
        return false;
    }
    public static bool IsPackageExport => IsParamInCli("-exportPackage");

    // Broader: skip for any headless run
    public static bool IsHeadless => IsParamInCli("-batchmode");

    static bool IsToolingLaunchChecked = false;
    static bool IsToolingLaunchResult = false;

    public static bool IsToolingLaunch()
    {
        if (IsToolingLaunchChecked != true)
        {
            IsToolingLaunchResult = false;

#if UNITY_EDITOR
            IsToolingLaunchResult = IsPackageExport || IsHeadless || Application.isBatchMode;
#endif
            IsToolingLaunchChecked = true;
        }

        return IsToolingLaunchResult;
    }
}
#endif

public class VoiceEditorSettingsChecksScriptable : ScriptableObject
{
    [Header("Enforced options")]
    [Tooltip("Force Run in background to be set to true")] 
    public bool enforceRunInBackground = true;

    [Header("Settings check logs")]
    [Tooltip("If set to false, some log/warning won't appear")]
    public bool logPotentialIssuesWithSetttings = true;
    [Tooltip("If set to false, some log/warning won't appear, if the result where positive")]
    public bool logSuccessfulSetttingsAnalysis = false;

    const string PHOTON_VOICE_FOLDER_GUID = "d3a9df3027b4a45679a2a3e978dde78e";
    const string VOICE_EDITOR_SETTINGS_CHECK_FILENAME = "VoiceEditorSettingsChecks";

    private void OnValidate()
    {
#if UNITY_EDITOR
        CheckSettings();
#endif
    }

    void CheckSettings() { 
        // Delayed to when the SDK is fully set up, to avoid "Calls to “AssetDatabase.ImportAsset” are restricted during asset importing" errors in Unity 6.3+. See https://discussions.unity.com/t/changes-to-assetdatabase-apis-when-called-during-import/1689358
#if (PHOTON_VOICE_R4 || PHOTON_VOICE_R5)
        EditorApplication.delayCall += PhotonVoiceEditorSettingsCheck.RunChecks;
#endif
    }

    /// <summary>
    /// Find the default check list configuration scriptable
    /// </summary>
    public static VoiceEditorSettingsChecksScriptable FindCheckList()
    {
        var voiceEditorSettingsChecks = (VoiceEditorSettingsChecksScriptable)Resources.Load(VOICE_EDITOR_SETTINGS_CHECK_FILENAME, typeof(VoiceEditorSettingsChecksScriptable));
        if(voiceEditorSettingsChecks == null)
        {
            voiceEditorSettingsChecks = CreateDefaultScriptable();
        }
        return voiceEditorSettingsChecks;
    }

    /// <summary>
    /// Create the default check list configuration scriptable
    /// </summary>
    public static VoiceEditorSettingsChecksScriptable CreateDefaultScriptable()
    {
        var voiceEditorSettingsChecks = (VoiceEditorSettingsChecksScriptable)ScriptableObject.CreateInstance(typeof(VoiceEditorSettingsChecksScriptable));
#if UNITY_EDITOR
        if (EditorSettingsBootstrapGuard.IsToolingLaunch())
        {
            // We don't want to save the file
            return voiceEditorSettingsChecks;
        }
#endif
        if (voiceEditorSettingsChecks == null)
        {
            Debug.LogError("Failed to create VoiceEditorSettingsChecksScriptable.");
            return null;
        }
        var voicePath = UnityEditor.AssetDatabase.GUIDToAssetPath(PHOTON_VOICE_FOLDER_GUID);
        if (voicePath == null || voicePath == "" || voicePath.Contains("Packages"))
        {
            voicePath = "Assets/Photon/PhotonVoice";
        }

        string path = Path.Combine(voicePath, "Resources", VOICE_EDITOR_SETTINGS_CHECK_FILENAME + ".asset");
        string dir = Path.GetDirectoryName(path);

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            UnityEditor.AssetDatabase.ImportAsset(dir);
        }

        if (!File.Exists(path))
        {
            UnityEditor.AssetDatabase.CreateAsset(voiceEditorSettingsChecks, path);
        }
        UnityEditor.AssetDatabase.SaveAssets();

        return voiceEditorSettingsChecks;
    }
}
