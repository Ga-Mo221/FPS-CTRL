using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// The "Play" tab content: mode + map selection, party/status readouts and the
    /// ready/start actions. Presentation only — it raises events (ModeSelected,
    /// MapSelected, ReadyClicked, StartClicked) for a higher layer (matchmaking) to
    /// handle, and exposes setters to feed in data. Mirrors the old
    /// MainMenuUIController but without the top-bar concerns (those moved to
    /// <see cref="TopBarController"/>).
    /// </summary>
    public class PlayPanelController : MonoBehaviour
    {
        [Header("Mode & Map (radio = pick 1)")]
        [SerializeField] private UISelectionGroup _modeGroup;
        [SerializeField] private UISelectionGroup _mapGroup;
        [SerializeField] private TMP_Text _modeTitle;
        [SerializeField] private TMP_Text _modeDescription;

        [Header("Actions")]
        [SerializeField] private Button _readyButton;
        [SerializeField] private Button _startButton;
        // [UI-INTEGRATION 2026-09-13] Label nut Start -> doi thanh dong ho mm:ss khi tim tran. Bo trong = tu tim TMP con.
        [SerializeField] private TMP_Text _startLabel;
        private string _startDefaultText = "VÀO TRẬN";

        [Header("Status")]
        [SerializeField] private TMP_Text _onlineCount;
        [SerializeField] private TMP_Text _queueStatus;
        [SerializeField] private TMP_Text _serverName;
        [SerializeField] private TMP_Text _ping;

        public event Action<int> ModeSelected;
        public event Action<int> MapSelected;
        public event Action ReadyClicked;
        public event Action StartClicked;

        private void Awake()
        {
            if (_modeGroup != null) _modeGroup.SelectionChanged += OnModeChanged;
            if (_mapGroup != null) _mapGroup.SelectionChanged += OnMapChanged;
            if (_readyButton != null) _readyButton.onClick.AddListener(() => ReadyClicked?.Invoke());
            if (_startButton != null) _startButton.onClick.AddListener(() => StartClicked?.Invoke());
            // [UI-INTEGRATION] cache label nut Start + text mac dinh de khoi phuc sau khi tim tran.
            if (_startLabel == null && _startButton != null) _startLabel = _startButton.GetComponentInChildren<TMP_Text>(true);
            if (_startLabel != null) _startDefaultText = _startLabel.text;
        }

        private void OnDestroy()
        {
            if (_modeGroup != null) _modeGroup.SelectionChanged -= OnModeChanged;
            if (_mapGroup != null) _mapGroup.SelectionChanged -= OnMapChanged;
        }

        private void OnModeChanged(IReadOnlyList<UISelectable> s) { if (s.Count > 0) ModeSelected?.Invoke(s[0].Index); }
        private void OnMapChanged(IReadOnlyList<UISelectable> s) { if (s.Count > 0) MapSelected?.Invoke(s[0].Index); }

        // [MAP 2026-09-28 Aemy] API cau hinh o chon map (khoa 'coming soon' + chon mac dinh).
        public int MapCount => _mapGroup != null ? _mapGroup.ItemCount : 0;
        public void SetMapLocked(int index, bool locked)
        {
            var it = _mapGroup != null ? _mapGroup.GetItem(index) : null;
            if (it == null) return;
            it.SetLocked(locked);
            ApplyComingSoonTag(it, locked);
        }

        // [MAP 2026-09-28 Aemy] Phu 'COMING SOON' len o map khoa (bo qua o von da la coming-soon de tranh chu doi).
        private static void ApplyComingSoonTag(UISelectable it, bool on)
        {
            var existing = it.transform.Find("ComingSoonTag");
            if (!on) { if (existing != null) existing.gameObject.SetActive(false); return; }
            if (existing != null) { existing.gameObject.SetActive(true); existing.SetAsLastSibling(); return; }
            var labels = it.GetComponentsInChildren<TMP_Text>(true);
            TMP_Text src = (labels != null && labels.Length > 0) ? labels[0] : null;
            var go = new GameObject("ComingSoonTag", typeof(RectTransform));
            go.transform.SetParent(it.transform, false); go.transform.SetAsLastSibling();
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = go.AddComponent<Image>(); bg.color = new Color(0.11f, 0.12f, 0.14f, 0.92f); bg.raycastTarget = true; // [MAP-FIX 2026-09-29 Aemy] phu KIN giong o 4 (che ca ten + coming soon sang)
            var txtGo = new GameObject("Label", typeof(RectTransform)); txtGo.transform.SetParent(go.transform, false);
            var trt = txtGo.GetComponent<RectTransform>(); trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var tmp = txtGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "COMING SOON"; tmp.alignment = TMPro.TextAlignmentOptions.Center; tmp.fontSize = 20; tmp.color = new Color(0.60f, 0.62f, 0.66f, 1f); tmp.raycastTarget = false; tmp.fontStyle = TMPro.FontStyles.Bold; tmp.enableAutoSizing = true; tmp.fontSizeMin = 10; tmp.fontSizeMax = 22;
            if (src != null && src.font != null) tmp.font = src.font;
        }
        public void SelectMap(int index, bool notify = false) { if (_mapGroup != null) _mapGroup.SelectByIndex(index, notify); }
        // [Aemy 2026-10-17] Chon nut che do tu code (dong bo theo truong nhom).
        public void SelectMode(int index, bool notify = false) { if (_modeGroup != null) _modeGroup.SelectByIndex(index, notify); }

        // [Aemy 2026-10-17] Thanh vien to doi: khoa CHE DO + BAN DO (lam mo, khong bam duoc) + nhan "THEO TRUONG NHOM".
        private TMP_Text _lockHint;
        public void SetChoiceLocked(bool locked, string hint)
        {
            LockGroup(_modeGroup, locked);
            LockGroup(_mapGroup, locked);
            if (_lockHint == null && locked && _modeGroup != null)
            {
                var src = _modeGroup.GetComponent<TMP_Text>();
                var go = new GameObject("PartyLockHint", typeof(RectTransform));
                go.transform.SetParent(_modeGroup.transform.parent, false);
                var rt = (RectTransform)go.transform;
                var head = (RectTransform)_modeGroup.transform;
                rt.anchorMin = head.anchorMin; rt.anchorMax = head.anchorMax; rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = head.anchoredPosition + new Vector2(388f, 0f); rt.sizeDelta = new Vector2(300f, 30f);
                _lockHint = go.AddComponent<TextMeshProUGUI>();
                if (src != null) { _lockHint.font = src.font; }
                _lockHint.fontSize = 15; _lockHint.alignment = TextAlignmentOptions.MidlineRight; _lockHint.raycastTarget = false;
                _lockHint.color = new Color(0.31f, 0.84f, 0.79f, 1f); _lockHint.enableWordWrapping = false;
            }
            if (_lockHint != null) { _lockHint.gameObject.SetActive(locked); if (!string.IsNullOrEmpty(hint)) _lockHint.text = hint; }
        }
        private static void LockGroup(UISelectionGroup g, bool locked)
        {
            if (g == null) return;
            var cg = g.GetComponent<CanvasGroup>();
            if (cg == null) { if (!locked) return; cg = g.gameObject.AddComponent<CanvasGroup>(); }
            cg.interactable = !locked; cg.blocksRaycasts = !locked; cg.alpha = locked ? 0.55f : 1f;
        }

        // [MAP-SYNC 2026-10-01 Aemy] Set text ten o map (dong bo voi displayName trong code).
        public void SetMapName(int index, string name)
        {
            var it = _mapGroup != null ? _mapGroup.GetItem(index) : null;
            if (it == null || string.IsNullOrEmpty(name)) return;
            var labels = it.GetComponentsInChildren<TMP_Text>(true);
            if (labels != null && labels.Length > 0 && labels[0] != null) labels[0].text = name;
        }

        // ---- Setters ----
        public void SetModeDescription(string title, string description)
        {
            if (_modeTitle != null) _modeTitle.text = title;
            if (_modeDescription != null) _modeDescription.text = description;
        }
        public void SetOnlineCount(int count) { if (_onlineCount != null) _onlineCount.text = count.ToString("N0"); }
        public void SetQueueStatus(string status) { if (_queueStatus != null) _queueStatus.text = status; }
        public void SetServer(string serverName, int ping)
        {
            if (_serverName != null) _serverName.text = serverName;
            if (_ping != null) _ping.text = $"{ping} ms";
        }
        public void SetStartInteractable(bool value) { if (_startButton != null) _startButton.interactable = value; }

        // [UI-INTEGRATION] Doi text nut Start (dung cho dong ho mm:ss) + khoi phuc lai text mac dinh.
        public void SetStartLabel(string text) { if (_startLabel != null) _startLabel.text = text; }
        public void RestoreStartLabel() { if (_startLabel != null) _startLabel.text = _startDefaultText; }
        // [Aemy 2026-10-17] Doi chu mac dinh nut chinh (VAO TRAN / TAO PHONG) theo che do.
        public void SetDefaultStartLabel(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            bool showingDefault = _startLabel == null || _startLabel.text == _startDefaultText || _startLabel.text == Loc.T(_startDefaultText);
            _startDefaultText = text;
            if (showingDefault && _startLabel != null) _startLabel.text = text;
        }

        // ================= [Aemy 2026-10-10] ANH MAP =================
        [Header("Anh map (Aemy 2026-10-10)")]
        [Tooltip("Vung lon co nut VAO TRAN (Overlay 5). De trong = tu tim 'Overlay 5'.")]
        [SerializeField] private RectTransform _mapArtArea;
        [Tooltip("Ten map hien o vung lon. De trong = tu tim 'Tên Map' trong vung lon.")]
        [SerializeField] private TMP_Text _selectedMapName;

        /// <summary>Gan icon cho o map thu index (anh phu kin o, chua vien 3px, ten map van nam tren).</summary>
        public void SetMapIcon(int index, Sprite icon)
        {
            var it = _mapGroup != null ? _mapGroup.GetItem(index) : null;
            if (it == null) return;
            var img = EnsureFillImage((RectTransform)it.transform, "MapIcon", 3f, true);
            img.sprite = icon; img.enabled = icon != null;
        }

        /// <summary>Doi anh lon + ten map o vung VAO TRAN theo map dang chon.</summary>
        public void SetSelectedMapArt(Sprite background, string mapName)
        {
            if (_mapArtArea == null) { var t = transform.Find("Overlay 5"); if (t != null) _mapArtArea = (RectTransform)t; }
            if (_mapArtArea == null) return;
            var img = EnsureFillImage(_mapArtArea, "MapArt", 0f, true);
            img.sprite = background; img.enabled = background != null;
            if (_selectedMapName == null) { var n = _mapArtArea.Find("Tên Map"); if (n != null) _selectedMapName = n.GetComponent<TMP_Text>(); }
            if (_selectedMapName != null && !string.IsNullOrEmpty(mapName)) _selectedMapName.text = mapName;
        }

        // Tao (1 lan) khung cat RectMask2D + Image lap day kieu "envelope" (giu ti le, cat phan thua) + lop toi phia duoi cho chu de doc.
        private static Image EnsureFillImage(RectTransform parent, string name, float inset, bool shade)
        {
            var holder = parent.Find(name) as RectTransform;
            if (holder == null)
            {
                holder = new GameObject(name, typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
                holder.SetParent(parent, false);
                holder.anchorMin = Vector2.zero; holder.anchorMax = Vector2.one;
                holder.offsetMin = new Vector2(inset, inset); holder.offsetMax = new Vector2(-inset, -inset);
                var picGo = new GameObject("Pic", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
                picGo.transform.SetParent(holder, false);
                var prt = picGo.GetComponent<RectTransform>(); prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
                var fit = picGo.GetComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = 16f / 9f;
                picGo.GetComponent<Image>().raycastTarget = false;
                if (shade)
                {
                    var sh = new GameObject("Shade", typeof(RectTransform), typeof(Image));
                    sh.transform.SetParent(holder, false);
                    var srt = sh.GetComponent<RectTransform>(); srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
                    var si = sh.GetComponent<Image>(); si.color = new Color(0f, 0f, 0f, 0.35f); si.raycastTarget = false;
                }
            }
            holder.SetAsFirstSibling(); // nam duoi chu + nut
            var pic = holder.Find("Pic").GetComponent<Image>();
            return pic;
        }
    }
}
