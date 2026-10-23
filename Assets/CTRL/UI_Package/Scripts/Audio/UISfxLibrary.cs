
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace Game.Audio
{
    public enum UISfxType { None, Hover, Click, Confirm, Back, Tab, WeaponSelect }

    [CreateAssetMenu(menuName = "Eclipse Strike/UI Sfx Library", fileName = "UISfxLibrary")]
    public class UISfxLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public UISfxType type;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.8f;
            [Tooltip("Dao pitch ngau nhien +- (0 = co dinh).")]
            [Range(0f, 0.2f)] public float pitchJitter = 0.03f;
        }

        [Serializable]
        public class NameRule
        {
            [Tooltip("Ten GameObject cua nut CHUA chuoi nay (khong phan biet hoa thuong).")]
            public string keyword;
            public UISfxType type;
        }

        [Header("Mixer (de slider UI & Announcer / Nhac trong Cai dat dieu khien duoc)")]
        public AudioMixerGroup uiGroup;
        public AudioMixerGroup musicGroup;

        [Header("Am thanh UI")]
        public List<Entry> sounds = new List<Entry>();
        [Tooltip("Am bam mac dinh theo ten nut (luat dau tien khop se dung). Khong khop -> Click.")]
        public List<NameRule> clickRules = new List<NameRule>();
        [Tooltip("Nut nam duoi cac GameObject ten nay -> am Tab.")]
        public List<string> tabParents = new List<string> { "TopBar" };
        [Tooltip("Khoang cach toi thieu giua 2 am (tranh chong tieng).")]
        public float minInterval = 0.04f;

        [Header("Nhac nen menu")]
        public AudioClip menuMusic;
        [Range(0f, 1f)] public float musicVolume = 0.5f;
        [Tooltip("Cac scene co nhac nen menu. Scene khac (vao tran) -> nhac tat dan.")]
        public List<string> musicScenes = new List<string> { "Login_Register", "Loading", "MenuShell" };
        public float musicFadeIn = 2f, musicFadeOut = 1.2f;

        public Entry Get(UISfxType type)
        {
            foreach (var e in sounds) if (e != null && e.type == type && e.clip != null) return e;
            return null;
        }

        /// <summary>Loai am khi BAM nut, suy tu ten nut / cha.</summary>
        public UISfxType ClassifyClick(Selectable s)
        {
            if (s == null) return UISfxType.Click;
            for (var t = s.transform.parent; t != null; t = t.parent)
                foreach (var p in tabParents) if (!string.IsNullOrEmpty(p) && t.name == p) return UISfxType.Tab;
            string n = s.gameObject.name;
            foreach (var r in clickRules)
                if (r != null && !string.IsNullOrEmpty(r.keyword) && n.IndexOf(r.keyword, StringComparison.OrdinalIgnoreCase) >= 0) return r.type;
            return UISfxType.Click;
        }

        private static UISfxLibrary _cached;
        public static UISfxLibrary Load()
        {
            if (_cached == null) _cached = Resources.Load<UISfxLibrary>("UISfxLibrary");
            return _cached;
        }
    }
}
