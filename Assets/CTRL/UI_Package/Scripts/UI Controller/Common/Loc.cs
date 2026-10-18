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

        // [Aemy 2026-10-18] BO CUC TIENG VIET LA CHUAN.
        // Khi sang English, moi text duoc "dong bang" tai dung khung (vi tri + kich thuoc) cua ban tieng Viet:
        //  - ContentSizeFitter ngang -> tat (khung giu rong cua chu Viet). Truoc day khung co gian theo chu Anh nen
        //    chinh nhan va CA CAC OBJECT CON gan vao nhan (switch, slider, cycler, nut LOW/MED..., divider) bi xe dich.
        //    Rong khung luon = rong CHU VIET (tinh lai moi khi code gan chu Viet moi) -> y het ban Viet.
        //  - Text nam trong LayoutGroup -> them LayoutElement giu dung rong ban Viet de cac anh em khong bi day.
        //  - Cot gia tri (mep phai thang hang voi object khac) -> can phai de giu mep phai nhu ban Viet.
        //  - Giu co chu ban Viet; chu Anh dai duoc tran ra khoang trong ben canh, chi thu nho khi khong du cho.
        //  - Chi xu ly text DANG HIEN (sau khi layout frame do da chay voi chu Viet); text an se duoc xu ly
        //    khi no hien len (TMP ban TEXT_CHANGED khi bat lai) -> khong bao gio dong bang theo khung cu/chua layout.
        // Doi lai Tieng Viet -> tra moi thu ve nguyen trang.
        private class Freeze
        {
            public bool hadCsf; public UnityEngine.UI.ContentSizeFitter.FitMode csfFit;
            public HorizontalAlignmentOptions align;
            public UnityEngine.UI.LayoutElement addedLE;
            public bool wrap, autoSize; public float fontSize, min, max;
            public TextOverflowModes overflow;
            public float baseSize = -1f, room = -1f;
        }
        private readonly Dictionary<TMP_Text, Freeze> _freeze = new Dictionary<TMP_Text, Freeze>();

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

        // Khong dich ngay luc load scene: layout chua chay. Frame dau moi text deu ban TEXT_CHANGED (sau layout) -> Flush xu ly.
        private void OnSceneLoaded(Scene s, LoadSceneMode m) { }

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
            foreach (var t in _buffer)
            {
                // Flush chay SAU khi Canvas da rebuild trong frame nay -> dung lai mesh ngay de khong lo 1 frame chu Viet
                // (truoc day panel vua mo se nhay chu Viet 1 frame roi moi sang English).
                if (Process(t) && t != null && t.isActiveAndEnabled) t.ForceMeshUpdate();
            }
        }

        public static void RetranslateAll()
        {
            if (s_instance == null) return;
            var all = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in all) s_instance.Process(t);
            if (Loc.Lang == Loc.Vietnamese) { s_instance._original.Clear(); s_instance._applied.Clear(); s_instance._freeze.Clear(); }
        }

        private bool Process(TMP_Text t)
        {
            if (t == null) return false;
            if (IsUserInput(t)) return false;
            string cur = t.text;
            string mine;
            if (Loc.Lang == Loc.English)
            {
                if (_applied.TryGetValue(t, out mine) && mine == cur) return false; // chu do minh vua dich
                if (!t.isActiveAndEnabled) return false;   // dang an: doi luc hien (khung luc nay chua tin duoc)
                string en = Loc.Translate(cur);
                FreezeLayout(t, cur, en);   // ca text khong dich (so, ten) cung dong bang de khong keo object khac
                ApplyVnWidth(t);            // chu Viet moi (vd code doi trang thai) -> rong khung = rong chu Viet do
                if (en == null || en == cur) return false;
                _original[t] = cur; _applied[t] = en;
                FitSize(t, en);
                t.text = en;
                return true;
            }
            else
            {
                string vi;
                bool changed = false;
                if (_applied.TryGetValue(t, out mine) && mine == cur && _original.TryGetValue(t, out vi)) { t.text = vi; changed = true; }
                _applied.Remove(t); _original.Remove(t);
                Unfreeze(t);
                return changed;
            }
        }

        /// <summary>Giu khung ban tieng Viet (lan dau thay text o che do English).</summary>
        private void FreezeLayout(TMP_Text t, string vi, string en)
        {
            if (_freeze.ContainsKey(t)) return;
            var rt = t.rectTransform;
            var f = new Freeze
            {
                align = t.horizontalAlignment, wrap = t.enableWordWrapping, autoSize = t.enableAutoSizing,
                fontSize = t.fontSize, min = t.fontSizeMin, max = t.fontSizeMax, overflow = t.overflowMode
            };
            var csf = t.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (csf != null && csf.horizontalFit != UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained)
            {
                f.hadCsf = true; f.csfFit = csf.horizontalFit;
                csf.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained; // rong giu nguyen = rong chu Viet
            }
            var parent = rt.parent;
            var lg = parent != null ? parent.GetComponent<UnityEngine.UI.HorizontalOrVerticalLayoutGroup>() : null;
            if (lg != null && t.GetComponent<UnityEngine.UI.LayoutElement>() == null && rt.rect.width > 1f)
            {
                var le = t.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                le.preferredWidth = t.preferredWidth; le.layoutPriority = 2;
                if (lg is UnityEngine.UI.VerticalLayoutGroup) le.preferredWidth = -1f; // doc: chi giu chieu cao tu nhien
                f.addedLE = le;
            }
            _freeze[t] = f;

            bool singleLine = !string.IsNullOrEmpty(vi) && vi.IndexOf('\n') < 0 && (f.hadCsf || !f.wrap || vi.Trim().Length <= 24);
            if (singleLine && (f.hadCsf || vi.Trim().Length <= 24))
            {
                t.enableWordWrapping = false;
                if (t.overflowMode == TextOverflowModes.Truncate || t.overflowMode == TextOverflowModes.Ellipsis) { }
                else t.overflowMode = TextOverflowModes.Overflow;
            }
            // Cot gia tri: chu can trai co CSF ma mep PHAI thang hang voi object khac (divider, gia tri khac...)
            // con mep trai khong thang hang gi -> ban Viet dang "dinh" mep phai -> can phai de giu dung mep do.
            if (f.hadCsf && f.align == HorizontalAlignmentOptions.Left && IsRightFlush(rt))
                t.horizontalAlignment = HorizontalAlignmentOptions.Right;
        }

        /// <summary>
        /// Dat rong khung = rong ma ban Viet se co voi chu Viet HIEN TAI (thay cho ContentSizeFitter / layout group).
        /// Goi khi text dang mang chu Viet (truoc khi gan chu Anh).
        /// </summary>
        private void ApplyVnWidth(TMP_Text t)
        {
            Freeze f;
            if (!_freeze.TryGetValue(t, out f)) return;
            var rt = t.rectTransform;
            if (f.addedLE != null)
            {
                if (f.addedLE.preferredWidth >= 0f) f.addedLE.preferredWidth = t.preferredWidth;
                return;
            }
            if (!f.hadCsf) return;
            float w = f.csfFit == UnityEngine.UI.ContentSizeFitter.FitMode.MinSize
                ? UnityEngine.UI.LayoutUtility.GetMinWidth(rt)
                : UnityEngine.UI.LayoutUtility.GetPreferredWidth(rt);
            if (Mathf.Abs(rt.rect.width - w) > 0.01f) rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        }

        private static Transform FindBound(RectTransform rt)
        {
            for (var p = rt.parent; p != null; p = p.parent)
            {
                if (p.GetComponent<Canvas>() != null || p.GetComponent<UnityEngine.UI.RectMask2D>() != null || p.GetComponent<UnityEngine.UI.Mask>() != null) return p;
                var g = p.GetComponent<UnityEngine.UI.Graphic>();
                if (g != null && g.enabled && g.color.a > 0.05f && !(g is TMP_Text)) return p;
            }
            return null;
        }

        private static bool IsRightFlush(RectTransform rt)
        {
            var bound = FindBound(rt);
            if (bound == null) return false;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float x0 = c[0].x, x1 = c[2].x;
            float tol = 3f * Mathf.Max(1e-3f, Mathf.Abs(rt.lossyScale.x));
            bool r = false, l = false;
            foreach (var g in bound.GetComponentsInChildren<UnityEngine.UI.Graphic>(false))
            {
                var gt = g.transform;
                if (gt == rt || rt.IsChildOf(gt) || gt.IsChildOf(rt) || !g.enabled) continue;
                g.rectTransform.GetWorldCorners(c);
                if (Mathf.Abs(c[2].x - x1) < tol) r = true;
                if (Mathf.Abs(c[0].x - x0) < tol) l = true;
                if (l) return false;
            }
            return r;
        }

        /// <summary>
        /// Giu co chu ban Viet. Khung text dung yen (vi tri ban Viet); chu Anh dai hon duoc phep tran ra
        /// khoang trong ben canh (khong de len object khac, khong vuot nen chua no). Chi thu nho khi khoang
        /// trong do van khong du.
        /// </summary>
        private void FitSize(TMP_Text t, string en)
        {
            Freeze f;
            if (!_freeze.TryGetValue(t, out f)) return;
            float w = t.rectTransform.rect.width;
            if (w < 2f || t.enableWordWrapping) return;
            float baseSize = f.autoSize ? Mathf.Max(t.fontSize, 1f) : f.fontSize; // co dang hien thi o ban Viet
            if (f.baseSize <= 0f) f.baseSize = baseSize; else baseSize = f.baseSize;
            t.enableAutoSizing = false; t.fontSize = baseSize;
            float pw = t.GetPreferredValues(en, 99999f, 99999f).x;
            if (pw <= w + 0.5f) return;
            if (f.room < 0f) f.room = FreeRoom(t);
            float room = Mathf.Max(w, f.room);
            if (pw <= room) return;                       // tran ra khoang trong, giu nguyen co chu
            float size = baseSize * room / pw;
            t.fontSize = Mathf.Max(Mathf.Max(8f, baseSize * 0.6f), Mathf.Floor(size * 10f) / 10f);
        }

        /// <summary>Do rong toi da chu duoc phep chiem (don vi local cua text), tinh tu khung ban Viet.</summary>
        private static float FreeRoom(TMP_Text t)
        {
            var rt = t.rectTransform;
            if (t.overflowMode != TextOverflowModes.Overflow) return rt.rect.width; // cat/ellipsis: khong tran duoc
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float x0 = c[0].x, x1 = c[2].x, y0 = c[0].y, y1 = c[2].y;
            float scale = Mathf.Abs(rt.lossyScale.x) > 1e-5f ? Mathf.Abs(rt.lossyScale.x) : 1f;
            float pad = 6f * scale;

            // Gioi han: to tien gan nhat co nen hien thi / mask / canvas.
            Transform bound = FindBound(rt);
            if (bound == null || !(bound is RectTransform)) return rt.rect.width;
            ((RectTransform)bound).GetWorldCorners(c);
            float left = c[0].x + pad, right = c[2].x - pad;

            // Vat can: graphic hien thi khac trong cung hang (ke ca object con cua chinh text, vd switch).
            float h = Mathf.Max(1e-3f, y1 - y0);
            foreach (var g in bound.GetComponentsInChildren<UnityEngine.UI.Graphic>(false))
            {
                if (g == t || !g.enabled || g.color.a <= 0.05f) continue;
                var gt = g.transform;
                if (rt.IsChildOf(gt)) continue;                      // to tien (nen cua chinh no)
                var gtmp = g as TMP_Text;
                if (gtmp != null && string.IsNullOrEmpty(gtmp.text)) continue;
                g.rectTransform.GetWorldCorners(c);
                float gy0 = c[0].y, gy1 = c[2].y, gx0 = c[0].x, gx1 = c[2].x;
                if (Mathf.Min(y1, gy1) - Mathf.Max(y0, gy0) < h * 0.3f) continue; // khong cung hang
                if (gx0 >= x1 - 1f * scale) right = Mathf.Min(right, gx0 - pad);
                else if (gx1 <= x0 + 1f * scale) left = Mathf.Max(left, gx1 + pad);
            }

            float room;
            switch (t.horizontalAlignment)
            {
                case HorizontalAlignmentOptions.Right: room = x1 - left; break;
                case HorizontalAlignmentOptions.Center:
                case HorizontalAlignmentOptions.Geometry:
                    float cx = (x0 + x1) * 0.5f; room = 2f * Mathf.Min(cx - left, right - cx); break;
                default: room = right - x0; break;
            }
            return Mathf.Max(rt.rect.width, room / scale);
        }

        private void Unfreeze(TMP_Text t)
        {
            Freeze f;
            if (!_freeze.TryGetValue(t, out f)) return;
            _freeze.Remove(t);
            var csf = t.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (f.hadCsf && csf != null) csf.horizontalFit = f.csfFit;
            if (f.addedLE != null) Destroy(f.addedLE);
            t.horizontalAlignment = f.align;
            t.enableWordWrapping = f.wrap; t.overflowMode = f.overflow;
            t.fontSizeMin = f.min; t.fontSizeMax = f.max; t.enableAutoSizing = f.autoSize;
            if (!f.autoSize) t.fontSize = f.fontSize;
        }

        // Khong dich chu nguoi choi go (o nhap ma phong, ten...).
        private static bool IsUserInput(TMP_Text t)
        {
            var input = t.GetComponentInParent<TMP_InputField>(true);
            return input != null && input.textComponent == t;
        }
    }
}
