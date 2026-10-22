// ============================================================
//  TrueInputSystem — TIS_MotionPrefs.cs
//
//  Lưu lựa chọn bật / tắt gyro + touchpad của NGƯỜI CHƠI (theo profile và theo action)
//  giữa các lần mở game — giống cách TISRebindingSystem lưu binding.
//
//  File: {Application.persistentDataPath}/TIS_Motion_{profileName}.json
//  Chỉ ghi khi API được gọi với save: true. TISInputBridge tự đọc lại khi load / swap profile.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GaMo.TrueInputSystem
{
    [Serializable]
    internal class TISActionMotionPref
    {
        public string actionName;
        public bool   gyro     = true;
        public bool   touchpad = true;
    }

    [Serializable]
    internal class TISMotionPrefsFile
    {
        public string profileName;
        public string savedAt;

        public bool hasProfileGyro;
        public bool profileGyro = true;
        public bool hasProfileTouchpad;
        public bool profileTouchpad = true;

        public List<TISActionMotionPref> actions = new List<TISActionMotionPref>();
    }

    internal static class TISMotionPrefs
    {
        /// <summary>Khoá profile — cùng quy tắc với TISRebindingSystem (bỏ hậu tố " (Runtime)").</summary>
        public static string ProfileKey(string profileName)
            => profileName?.Replace(" (Runtime)", "").Trim() ?? "";

        public static string GetPath(string profileKey)
        {
            string safe = profileKey;
            foreach (char c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '_');
            return Path.Combine(Application.persistentDataPath, $"TIS_Motion_{safe}.json");
        }

        public static TISMotionPrefsFile Load(string profileKey)
        {
            string path = GetPath(profileKey);
            if (!File.Exists(path)) return null;
            try
            {
                return JsonUtility.FromJson<TISMotionPrefsFile>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TISMotionPrefs] Không đọc được \"{path}\" — bỏ qua. {e.Message}");
                return null;
            }
        }

        public static void Save(string profileKey, TISMotionPrefsFile data)
        {
            data.profileName = profileKey;
            data.savedAt     = DateTime.UtcNow.ToString("o");
            string path = GetPath(profileKey);
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TISMotionPrefs] Không ghi được \"{path}\". {e.Message}");
            }
        }

        public static void Delete(string profileKey)
        {
            string path = GetPath(profileKey);
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception e) { Debug.LogWarning($"[TISMotionPrefs] Không xoá được \"{path}\". {e.Message}"); }
        }
    }
}
