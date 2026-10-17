using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Luu + AP DUNG cac gia tri cai dat (Am thanh / Chuot / Phim) trong PHIEN choi.
    /// Cac slider/toggle trong panel Settings goi truc tiep cac ham Set... ben duoi (qua OnValueChanged).
    /// Gameplay doc lai gia tri qua cac property (MasterVolume, Sensitivity, GetKey...).
    /// LUU Y: ban nay chi luu trong bo nho phien (chua dong bo PlayFab - de sau).
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        /// <summary>Instance dang hoat dong (interface nao dang bat thi la cai do). Gameplay co the doc qua day.</summary>
        public static SettingsController Instance { get; private set; }

        // ---------- Am thanh (0..1) ----------
        public float MasterVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;

        // ---------- Chuot ----------
        public float Sensitivity { get; private set; } = 1f;      // he so do nhay khi ban thuong
        public float AdsSensitivity { get; private set; } = 1f;   // he so do nhay khi ngam (ADS)
        public bool InvertY { get; private set; } = false;        // dao truc doc

        [Header("Tuy chon: AudioMixer (de trong neu chua co)")]
        [Tooltip("Neu gan mixer + ten tham so expose, Music/SFX se ap qua mixer. De trong thi chi luu gia tri.")]
        [SerializeField] private UnityEngine.Audio.AudioMixer _mixer;
        [SerializeField] private string _musicParam = "MusicVolume";
        [SerializeField] private string _sfxParam = "SfxVolume";

        [Header("Controls - Am thanh")]
        [SerializeField] private UnityEngine.UI.Slider _masterSlider;
        [SerializeField] private UnityEngine.UI.Slider _musicSlider;
        [SerializeField] private UnityEngine.UI.Slider _sfxSlider;

        [Header("Controls - Chuot")]
        [SerializeField] private UnityEngine.UI.Slider _sensitivitySlider;
        [SerializeField] private UnityEngine.UI.Slider _adsSlider;
        [SerializeField] private UnityEngine.UI.Toggle _invertYToggle;

        // ---------- Phim (keybind) ----------
        private readonly Dictionary<string, KeyCode> _keys = new Dictionary<string, KeyCode>();

        private void Awake()
        {
            // Tu dong noi cac control (slider/toggle) vao cac ham Set... + ap gia tri ban dau.
            if (_masterSlider != null) { _masterSlider.onValueChanged.AddListener(SetMaster); SetMaster(_masterSlider.value); }
            if (_musicSlider != null)  { _musicSlider.onValueChanged.AddListener(SetMusic);  SetMusic(_musicSlider.value); }
            if (_sfxSlider != null)    { _sfxSlider.onValueChanged.AddListener(SetSfx);      SetSfx(_sfxSlider.value); }
            if (_sensitivitySlider != null) { _sensitivitySlider.onValueChanged.AddListener(SetSensitivity); SetSensitivity(_sensitivitySlider.value); }
            if (_adsSlider != null)    { _adsSlider.onValueChanged.AddListener(SetAdsSensitivity); SetAdsSensitivity(_adsSlider.value); }
            if (_invertYToggle != null){ _invertYToggle.onValueChanged.AddListener(SetInvertY); SetInvertY(_invertYToggle.isOn); }
        }

        private void OnEnable()
        {
            Instance = this;                 // interface dang bat -> tro thanh cai dat hien hanh
            AudioListener.volume = MasterVolume; // dong bo lai am luong tong
        }

        // ---------- Setter cho UI (wire vao Slider/Toggle OnValueChanged) ----------

        /// <summary>Am luong tong (0..1) - ap thang vao AudioListener.</summary>
        public void SetMaster(float v)
        {
            MasterVolume = Mathf.Clamp01(v);
            AudioListener.volume = MasterVolume;
        }

        /// <summary>Am luong nhac (0..1). Ap qua mixer neu co, khong thi chi luu.</summary>
        public void SetMusic(float v)
        {
            MusicVolume = Mathf.Clamp01(v);
            ApplyMixer(_musicParam, MusicVolume);
        }

        /// <summary>Am luong hieu ung (0..1).</summary>
        public void SetSfx(float v)
        {
            SfxVolume = Mathf.Clamp01(v);
            ApplyMixer(_sfxParam, SfxVolume);
        }

        /// <summary>Do nhay chuot khi ban thuong.</summary>
        public void SetSensitivity(float v) { Sensitivity = Mathf.Max(0.01f, v); }

        /// <summary>Do nhay chuot khi ngam (ADS).</summary>
        public void SetAdsSensitivity(float v) { AdsSensitivity = Mathf.Max(0.01f, v); }

        /// <summary>Dao truc doc (Invert Y).</summary>
        public void SetInvertY(bool on) { InvertY = on; }

        // ---------- Keybind ----------

        /// <summary>Lay phim da gan cho 1 hanh dong (tra ve def neu chua gan).</summary>
        public KeyCode GetKey(string action, KeyCode def)
        {
            KeyCode k;
            return _keys.TryGetValue(action, out k) ? k : def;
        }

        /// <summary>Gan phim moi cho 1 hanh dong.</summary>
        public void SetKey(string action, KeyCode key)
        {
            if (string.IsNullOrEmpty(action)) return;
            _keys[action] = key;
        }

        // ---------- Noi bo ----------

        // Chuyen 0..1 -> decibel (-80..0) roi set vao mixer (neu co).
        private void ApplyMixer(string param, float value01)
        {
            if (_mixer == null || string.IsNullOrEmpty(param)) return;
            float db = (value01 <= 0.0001f) ? -80f : Mathf.Log10(value01) * 20f;
            _mixer.SetFloat(param, db);
        }
    }
}
