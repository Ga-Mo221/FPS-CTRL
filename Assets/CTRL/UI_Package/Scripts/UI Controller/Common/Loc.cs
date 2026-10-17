// [Aemy 2026-10-17] FILE MOI. Ngon ngu giao dien (Tieng Viet / English) cho Menu + Cai dat + HUD.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Cach hoat dong: UI duoc dung bang tieng Viet (scene/prefab/code giu nguyen). Khi chon English,
    /// LocalizationRuntime bat su kien TMP doi chu (TMPro_EventManager.TEXT_CHANGED_EVENT) va thay bang ban dich
    /// trong LocTable (khop chinh xac hoac mau co so/ten). Doi lai Tieng Viet -> tra chu goc.
    /// => Khong phai sua tung man hinh; them chuoi moi chi can them 1 dong vao LocTable.
    /// </summary>
    public static class Loc
    {
        public const int Vietnamese = 0, English = 1;
        public static readonly string[] LanguageNames = { "Tiếng Việt", "English" };

        public static int Lang { get; private set; }
        public static event Action Changed;

        public static void SetLanguage(int lang)
        {
            lang = Mathf.Clamp(lang, 0, LanguageNames.Length - 1);
            if (lang == Lang) return;
            Lang = lang;
            LocalizationRuntime.Ensure();
            LocalizationRuntime.RetranslateAll();
            if (Changed != null) Changed();
        }

        /// <summary>Dich 1 chuoi cho code (vd tieu de ModalDialog). Khong co ban dich -> tra nguyen van.</summary>
        public static string T(string vi)
        {
            if (Lang != English || string.IsNullOrEmpty(vi)) return vi;
            string en = Translate(vi);
            return en ?? vi;
        }

        /// <summary>Ban dich English cua chuoi tieng Viet (null neu khong co).</summary>
        public static string Translate(string vi)
        {
            if (string.IsNullOrEmpty(vi)) return null;
            string trimmed = vi.Trim();
            if (trimmed.Length == 0) return null;
            string en;
            string nt = trimmed.Normalize(System.Text.NormalizationForm.FormC); // chu dung dau to hop (NFD) -> NFC
            if (LocTable.Exact.TryGetValue(nt, out en)) return Keep(vi, trimmed, en);
            if (Upper.TryGetValue(nt, out en)) return Keep(vi, trimmed, en);
            foreach (var p in LocTable.Patterns)
            {
                var m = p.Key.Match(nt);
                if (!m.Success) continue;
                string r = p.Key.Replace(nt, p.Value);
                // nhom bat duoc cung co the la chu tieng Viet (vd "Ngau nhien · <mo ta map>") -> dich tiep phan do
                for (int g = 1; g < m.Groups.Count; g++)
                {
                    string part = m.Groups[g].Value; string tp;
                    if (part.Length > 0 && LocTable.Exact.TryGetValue(part, out tp)) r = r.Replace(part, tp);
                }
                return Keep(vi, trimmed, r);
            }
            return null;
        }

        // giu khoang trang dau/cuoi cua chuoi goc
        private static string Keep(string orig, string trimmed, string en)
        {
            if (orig.Length == trimmed.Length) return en;
            int lead = orig.IndexOf(trimmed, StringComparison.Ordinal);
            return orig.Substring(0, Math.Max(0, lead)) + en + orig.Substring(Math.Max(0, lead) + trimmed.Length);
        }

        // Khop khong phan biet HOA/thuong: "Toan man hinh" vs "TOAN MAN HINH" -> dich theo dung kieu chu.
        private static Dictionary<string, string> _upper;
        private static Dictionary<string, string> Upper
        {
            get
            {
                if (_upper != null) return _upper;
                _upper = new Dictionary<string, string>();
                foreach (var kv in LocTable.Exact)
                {
                    string ku = kv.Key.ToUpperInvariant();
                    if (ku != kv.Key && !LocTable.Exact.ContainsKey(ku) && !_upper.ContainsKey(ku)) _upper[ku] = kv.Value.ToUpperInvariant();
                }
                return _upper;
            }
        }
    }

    /// <summary>Doi tuong chay ngam (tu tao, DontDestroyOnLoad) ap ban dich len moi TMP_Text.</summary>
    public class LocalizationRuntime : MonoBehaviour
    {
        private static LocalizationRuntime s_instance;
        private readonly HashSet<TMP_Text> _pending = new HashSet<TMP_Text>();
        private readonly Dictionary<TMP_Text, string> _original = new Dictionary<TMP_Text, string>(); // chu tieng Viet goc
        private readonly Dictionary<TMP_Text, string> _applied = new Dictionary<TMP_Text, string>();  // chu English da gan
        private readonly List<TMP_Text> _buffer = new List<TMP_Text>();
        // Nhan ngan 1 dong (nut, tab): tieng Anh co the dai hon -> khong xuong dong, tu thu nho chu. Luu cai dat goc de tra lai.
        private struct FitBackup { public bool wrap, autoSize; public float min, max; }
        private readonly Dictionary<TMP_Text, FitBackup> _fit = new Dictionary<TMP_Text, FitBackup>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { Ensure(); }

        public static void Ensure()
        {
            if (s_instance != null) return;
            var go = new GameObject("LocalizationRuntime");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<LocalizationRuntime>();
        }

        private void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            Canvas.willRenderCanvases += Flush;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            Canvas.willRenderCanvases -= Flush;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode m) { if (Loc.Lang == Loc.English) RetranslateAll(); }

        private void OnTextChanged(UnityEngine.Object obj)
        {
            if (Loc.Lang != Loc.English) return;
            var t = obj as TMP_Text;
            if (t != null) _pending.Add(t);
        }

        private void Flush()
        {
            if (_pending.Count == 0) return;
            _buffer.Clear(); _buffer.AddRange(_pending); _pending.Clear();
            foreach (var t in _buffer) Process(t);
        }

        public static void RetranslateAll()
        {
            if (s_instance == null) return;
            var all = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in all) s_instance.Process(t);
            if (Loc.Lang == Loc.Vietnamese) { s_instance._original.Clear(); s_instance._applied.Clear(); }
        }

        private void Process(TMP_Text t)
        {
            if (t == null) return;
            if (IsUserInput(t)) return;
            string cur = t.text;
            string mine;
            if (Loc.Lang == Loc.English)
            {
                if (_applied.TryGetValue(t, out mine) && mine == cur) return; // chu do minh vua dich
                string en = Loc.Translate(cur);
                if (en == null || en == cur) return;
                _original[t] = cur; _applied[t] = en;
                FitSingleLine(t, cur);
                t.text = en;
            }
            else
            {
                string vi;
                if (_applied.TryGetValue(t, out mine) && mine == cur && _original.TryGetValue(t, out vi)) t.text = vi;
                _applied.Remove(t); _original.Remove(t);
                FitBackup b;
                if (_fit.TryGetValue(t, out b))
                {
                    t.enableWordWrapping = b.wrap; t.enableAutoSizing = b.autoSize; t.fontSizeMin = b.min; t.fontSizeMax = b.max;
                    if (!b.autoSize) t.fontSize = b.max;
                    _fit.Remove(t);
                }
            }
        }

        private void FitSingleLine(TMP_Text t, string vi)
        {
            if (_fit.ContainsKey(t) || string.IsNullOrEmpty(vi) || vi.IndexOf('\n') >= 0 || vi.Trim().Length > 24) return;
            var b = new FitBackup { wrap = t.enableWordWrapping, autoSize = t.enableAutoSizing, min = t.fontSizeMin, max = t.enableAutoSizing ? t.fontSizeMax : t.fontSize };
            _fit[t] = b;
            t.enableWordWrapping = false;
            bool fitter = t.GetComponent<UnityEngine.UI.ContentSizeFitter>() != null;
            if (!t.enableAutoSizing && !fitter && t.rectTransform.rect.width > 1f)
            {
                float size = t.fontSize;
                t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = Mathf.Max(8f, size * 0.6f);
            }
        }

        // Khong dich chu nguoi choi go (o nhap ma phong, ten...).
        private static bool IsUserInput(TMP_Text t)
        {
            var input = t.GetComponentInParent<TMP_InputField>(true);
            return input != null && input.textComponent == t;
        }
    }
}
