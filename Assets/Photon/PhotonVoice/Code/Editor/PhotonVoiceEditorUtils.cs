using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_2021_3_OR_NEWER
using UnityEditor.Build;
#endif

namespace Photon.Voice.Unity.Editor
{
    [InitializeOnLoad] // calls static constructor when script is recompiled
    public static class PhotonVoiceEditorUtils
    {
        public const string PHOTON_VOICE_DEFINE_SYMBOL = "PHOTON_VOICE_R4";
        public const string PHOTON_VIDEO_DISABLED_DEFINE_SYMBOL = "PHOTON_VOICE_VIDEO_DISABLED";
        public const string PHOTON_VIDEO_DEFINE_SYMBOL = "PHOTON_VOICE_VIDEO_ENABLE";
        public const string PHOTON_VIDEO_AVAILABLE_DEFINE_SYMBOL = "PHOTON_VOICE_VIDEO_AVAILABLE";
        public const string PHOTON_VIDEO_FOLDER_GUID = "156c3c771fe98924892e2efd06aa060a";

        static PhotonVoiceEditorUtils()
        {
            AddScriptingDefineSymbolToAllBuildTargetGroups(PHOTON_VOICE_DEFINE_SYMBOL);
            if (HasVideo)
            {
#if !PHOTON_VOICE_VIDEO_AVAILABLE
                Debug.Log("Photon Video is available");
                AddScriptingDefineSymbolToAllBuildTargetGroups(PHOTON_VIDEO_AVAILABLE_DEFINE_SYMBOL);
                AddScriptingDefineSymbolToAllBuildTargetGroups(PHOTON_VIDEO_DEFINE_SYMBOL);
                TriggerRecompile();
#endif
#if PHOTON_VOICE_VIDEO_AVAILABLE && !PHOTON_VOICE_VIDEO_ENABLE && !PHOTON_VOICE_VIDEO_DISABLED
                Debug.Log("Automatically enabling Photon Video");
                AddScriptingDefineSymbolToAllBuildTargetGroups(PHOTON_VIDEO_DEFINE_SYMBOL);
                TriggerRecompile();
#endif
            }
            else
            {
#if PHOTON_VOICE_VIDEO_AVAILABLE
                RemoveScriptingDefineSymbolToAllBuildTargetGroups(PHOTON_VIDEO_AVAILABLE_DEFINE_SYMBOL);
                TriggerRecompile();
#endif
            }
        }

        // triggers this calss recompilation after define symbols change
        private static void TriggerRecompile()
        {
            AssetDatabase.ImportAsset("Assets/Photon/PhotonVoice/Code/Editor/PhotonVoiceEditorUtils.cs");
        }

        /// <summary>True if the ChatClient of the Photon Chat API is available. If so, the editor may (e.g.) show additional options in settings.</summary>
        public static bool HasChat
        {
            get
            {
                return Type.GetType("Photon.Chat.ChatClient, Assembly-CSharp") != null || Type.GetType("Photon.Chat.ChatClient, Assembly-CSharp-firstpass") != null || Type.GetType("Photon.Chat.ChatClient, PhotonChat") != null;
            }
        }

        /// <summary>True if the PhotonNetwork of the PUN is available. If so, the editor may (e.g.) show additional options in settings.</summary>
        public static bool HasPun
        {
            get
            {
                return Type.GetType("Photon.Pun.PhotonNetwork, Assembly-CSharp") != null || Type.GetType("Photon.Pun.PhotonNetwork, Assembly-CSharp-firstpass") != null || Type.GetType("Photon.Pun.PhotonNetwork, PhotonUnityNetworking") != null;
            }
        }

        public static bool HasVideo
        {
            get
            {
                var videoPath = UnityEditor.AssetDatabase.GUIDToAssetPath(PHOTON_VIDEO_FOLDER_GUID);
                if(videoPath != null && videoPath != "" && Directory.Exists(videoPath))
                {
                    return true;
                }
                return Directory.Exists("Assets/Photon/PhotonVideo");
            }
        }

        [MenuItem("Window/Photon Voice/Remove PUN", true, 1)]
        private static bool RemovePunValidate()
        {
#if PHOTON_UNITY_NETWORKING
            return true;
#else
            return HasPun;
#endif
        }

        [MenuItem("Window/Photon Voice/Remove PUN", false, 1)]
        private static void RemovePun()
        {
            DeleteDirectory("Assets/Photon/PhotonVoice/Demos/DemoProximityVoiceChat");
            DeleteDirectory("Assets/Photon/PhotonVoice/Demos/DemoVoicePun");
            DeleteDirectory("Assets/Photon/PhotonVoice/Code/PUN");
            DeleteDirectory("Assets/Photon/PhotonUnityNetworking");
            CleanUpPunDefineSymbols();
        }

        [MenuItem("Window/Photon Voice/Remove Photon Chat", true, 2)]
        private static bool RemovePhotonChatValidate()
        {
            return HasChat;
        }

        [MenuItem("Window/Photon Voice/Remove Photon Chat", false, 2)]
        private static void RemovePhotonChat()
        {
            DeleteDirectory("Assets/Photon/PhotonChat");
        }

        [MenuItem("Window/Photon Voice/Leave a review", false, 3)]
        private static void OpenAssetStorePage()
        {
            Application.OpenURL("https://assetstore.unity.com/packages/tools/audio/photon-voice-2-130518");
        }

#if PHOTON_VOICE_VIDEO_AVAILABLE

#if PHOTON_VOICE_VIDEO_ENABLE
        [MenuItem("Window/Photon Video/Disable Video", false, 4)]
        private static void DisableVideo()
        {
            UnityEngine.Debug.Log("Disabling Photon Video (setting define '" + PHOTON_VIDEO_DISABLED_DEFINE_SYMBOL + "' and removing '" + PHOTON_VIDEO_DEFINE_SYMBOL  + "').");
            AddScriptingDefineSymbolToAllBuildTargetGroups(PHOTON_VIDEO_DISABLED_DEFINE_SYMBOL);
            RemoveScriptingDefineSymbolToAllBuildTargetGroups(PHOTON_VIDEO_DEFINE_SYMBOL);
            TriggerRecompile();
        }
#else
        [MenuItem("Window/Photon Video/Enable Video", false, 4)]
        private static void EnableVideo()
        {
            UnityEngine.Debug.Log("Enabling Photon Video (setting define '" + PHOTON_VIDEO_DEFINE_SYMBOL + "' and removing '" + PHOTON_VIDEO_DISABLED_DEFINE_SYMBOL  + "').");

            AddScriptingDefineSymbolToAllBuildTargetGroups(PHOTON_VIDEO_DEFINE_SYMBOL);
            RemoveScriptingDefineSymbolToAllBuildTargetGroups(PHOTON_VIDEO_DISABLED_DEFINE_SYMBOL);
            TriggerRecompile();
        }
#endif

#endif

        public static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                if (!FileUtil.DeleteFileOrDirectory(path))
                {
                    Debug.LogWarningFormat("Directory \"{0}\" not deleted.", path);
                }
                DeleteFile(string.Concat(path, ".meta"));
            }
            else
            {
                Debug.LogWarningFormat("Directory \"{0}\" does not exist.", path);
            }
        }

        public static void DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                if (!FileUtil.DeleteFileOrDirectory(path))
                {
                    Debug.LogWarningFormat("File \"{0}\" not deleted.", path);
                }
            }
            else
            {
                Debug.LogWarningFormat("File \"{0}\" does not exist.", path);
            }
        }

        public static bool IsInTheSceneInPlayMode(GameObject go)
        {
            return Application.isPlaying && !IsPrefab(go);
        }

        
        private static string GetScriptingDefines(BuildTargetGroup group)
        {
            #if UNITY_2021_3_OR_NEWER
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(group);
            var defineSymbolsString = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            #else
            var defineSymbolsString = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            #endif

            return defineSymbolsString;
        }

        private static void SetScriptingDefines(BuildTargetGroup group, string defines)
        {
            #if UNITY_2021_3_OR_NEWER
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(group);
            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, defines);
            #else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, defines);
            #endif
        }
        
        /// <summary>
        /// Adds a given scripting define symbol to all build target groups
        /// You can see all scripting define symbols ( not the internal ones, only the one for this project), in the PlayerSettings inspector
        /// </summary>
        /// <param name="defineSymbol">Define symbol.</param>
        public static void AddScriptingDefineSymbolToAllBuildTargetGroups(string defineSymbol)
        {
            foreach (BuildTarget target in Enum.GetValues(typeof(BuildTarget)))
            {
                BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);

                if (group == BuildTargetGroup.Unknown)
                {
                    continue;
                }

                var defineSymbols = GetScriptingDefines(group)
                    .Split(';')
                    .Select(d => d.Trim())
                    .ToList();

                if (!defineSymbols.Contains(defineSymbol))
                {
                    defineSymbols.Add(defineSymbol);

                    try
                    {
                        SetScriptingDefines(group, string.Join(";", defineSymbols.ToArray()));
                    }
                    catch (Exception e)
                    {
                        Debug.Log("Could not set Photon " + defineSymbol + " defines for build target: " + target + " group: " + group + " " + e);
                    }
                }
            }
        }

        /// <summary>
        /// Removes a given scripting define symbol to all build target groups
        /// You can see all scripting define symbols ( not the internal ones, only the one for this project), in the PlayerSettings inspector
        /// </summary>
        /// <param name="defineSymbol">Define symbol.</param>
        public static void RemoveScriptingDefineSymbolToAllBuildTargetGroups(string defineSymbol)
        {
            foreach (BuildTarget target in Enum.GetValues(typeof(BuildTarget)))
            {
                BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);

                if (group == BuildTargetGroup.Unknown)
                {
                    continue;
                }

                var defineSymbols = GetScriptingDefines(group).Split(';').Select(d => d.Trim()).ToList();

                if (defineSymbols.Contains(defineSymbol) && defineSymbols.Remove(defineSymbol))
                {
                    try
                    {
                        SetScriptingDefines(group, string.Join(";", defineSymbols.ToArray()));
                    }
                    catch (Exception e)
                    {
                        Debug.LogErrorFormat("Could not remove \"{0}\" Scripting Define Symbol for build target: {1} group: {2} {3}", defineSymbol, target, group, e);
                    }
                }
            }
        }

        /// <summary>
        /// Check if a GameObject is a prefab asset or part of a prefab asset, as opposed to an instance in the scene hierarchy
        /// </summary>
        /// <returns><c>true</c>, if a prefab asset or part of it, <c>false</c> otherwise.</returns>
        /// <param name="go">The GameObject to check</param>
        public static bool IsPrefab(GameObject go)
        {
            #if UNITY_2021_2_OR_NEWER
            return UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(go) != null || EditorUtility.IsPersistent(go);
            #elif UNITY_2018_3_OR_NEWER
            return UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetPrefabStage(go) != null || EditorUtility.IsPersistent(go);
            #else
            return EditorUtility.IsPersistent(go);
            #endif
        }

        /// <summary>
        /// Removes PUN2's Script Define Symbols from project
        /// </summary>
        public static void CleanUpPunDefineSymbols()
        {
            foreach (BuildTarget target in Enum.GetValues(typeof(BuildTarget)))
            {
                BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);

                if (group == BuildTargetGroup.Unknown)
                {
                    continue;
                }

                var defineSymbols = GetScriptingDefines(group)
                    .Split(';')
                    .Select(d => d.Trim())
                    .ToList();

                List<string> newDefineSymbols = new List<string>();
                foreach (var symbol in defineSymbols)
                {
                    if ("PHOTON_UNITY_NETWORKING".Equals(symbol) || symbol.StartsWith("PUN_2_"))
                    {
                        continue;
                    }

                    newDefineSymbols.Add(symbol);
                }

                try
                {
                    SetScriptingDefines(group, string.Join(";", newDefineSymbols.ToArray()));
                }
                catch (Exception e)
                {
                    Debug.LogErrorFormat("Could not set clean up PUN2's define symbols for build target: {0} group: {1}, {2}", target, group, e);
                }
            }
        }
    }
}