using System;
using System.Collections.Generic;
using UnityEngine;
using Game.UI;

/// <summary>
/// Noi UI panel Settings <-> SettingData (qua SettingManager).
/// - Tim control theo path duoi _panelRoot, dang ky binding (get/set toi SettingData).
/// - On-change: cap nhat SettingData + Apply LIVE (xem ngay) + debounce Save (tranh spam PlayFab).
/// - OnEnable (mo panel): refresh moi control tu SettingData.
/// Keybind KHONG o day (KeybindRow tu goi TIS). Chi wire audio/mouse/graphics/crosshair/privacy.
/// </summary>
public class SettingsBinder : MonoBehaviour
{
    [Tooltip("Goc panel Settings (chua cac tab). De trong = dung chinh object nay.")]
    [SerializeField] private Transform _panelRoot;
    [Tooltip("Giay cho sau thay doi cuoi moi luu (debounce).")]
    [SerializeField] private float _saveDebounce = 0.8f;

    private readonly List<Action> _refreshers = new List<Action>();
    private bool _dirty;
    private float _saveAt;
    private bool _built;
    private Game.UI.UISelectionGroup _navGroupRef; // [RESET-TAB2] nav tab group
    private int _lastNavIndex = 0; // [RESET-TAB2] tab cuoi cung duoc chon (0..5)

    private SettingData Data { get { return SettingManager.Instance != null ? SettingManager.Instance.SettingData : null; } }

    private void Awake()
    {
        if (_panelRoot == null) _panelRoot = transform;
        Build();
    }

    private void OnEnable()
    {
        // [Aemy 2026-10-09] doi thiet bi khi panel dang mo -> slider do nhay hien dung slot PC/tay cam
        if (SettingManager.Instance != null) SettingManager.Instance.OnPlatformChanged += OnPlatformChangedRefresh;
        if (!_built) return;
        RefreshAll();
    }

    private void OnDisable()
    {
        if (SettingManager.Instance != null) SettingManager.Instance.OnPlatformChanged -= OnPlatformChangedRefresh;
    }

    private void OnPlatformChangedRefresh(GaMo.TrueInputSystem.AimAssistPlatform platform)
    {
        if (_built) RefreshAll();
    }

    private void OnDestroy()
    {
        if (_navGroupRef != null) _navGroupRef.SelectionChanged -= OnNavSelectionChanged;
    }

    private void Update()
    {
        if (_dirty && Time.unscaledTime >= _saveAt)
        {
            _dirty = false;
            if (SettingManager.Instance != null) SettingManager.Instance.Save();
        }
    }

    // Goi khi 1 control doi gia tri: apply live + hen gio luu.
    private void MarkChanged()
    {
        if (SettingManager.Instance != null) SettingManager.Instance.Apply();
        _dirty = true;
        _saveAt = Time.unscaledTime + _saveDebounce;
    }

    private void RefreshAll()
    {
        if (Data == null) return;
        foreach (var r in _refreshers) { if (r != null) r(); }
    }

    // ======================= BUILD BINDINGS =======================
    private void Build()
    {
        // ---- Am thanh ----
        BindSliderPercent("Am thanh/AM LUONG/Am luong tong", () => Data.MasterVolume, v => Data.MasterVolume = v); // [AUDIO-SCALE] slider 0-100 <-> data 0-1
        BindSliderPercent("Am thanh/AM LUONG/Hieu ung (SFX)",  () => Data.SfxVolume,    v => Data.SfxVolume = v);
        BindSliderPercent("Am thanh/AM LUONG/UI & Announcer",  () => Data.UIVolume,    v => Data.UIVolume = v);
        BindSliderPercent("Am thanh/AM LUONG/Nhac nen",         () => Data.MusicVolume,  v => Data.MusicVolume = v);
        BindSwitch("Am thanh/THIET BI & TUY CHON/Am thanh vom (3D)", () => Data.Surround3D, v => Data.Surround3D = v);

        // ---- Chuot ----
        BindSlider("Chuot/DO NHAY/Do nhay chuot",         () => Data.MouseSensitivity,  v => Data.MouseSensitivity = v);
        BindSlider("Chuot/DO NHAY/Nhay khi ngam (ADS)",   () => Data.RifleADSSensitivity, v => Data.RifleADSSensitivity = v);
        BindSlider("Chuot/DO NHAY/Nhay khi ngam sniper",  () => Data.SniperADSSensitivity, v => Data.SniperADSSensitivity = v);
        BindSwitch("Chuot/TUY CHON/Dao nguoc truc Y",     () => Data.InvertY,    v => Data.InvertY = v);
        BindSwitch("Chuot/TUY CHON/Gia toc chuot (Accel)",() => Data.MouseAccel, v => Data.MouseAccel = v);

        // ---- Do hoa (trong ScrollRect) ----
        BindSlider("Do hoa Scroll/Content/HIEN THI/Truong nhin (FOV)", () => Data.Fov, v => Data.Fov = v);
        BindSwitch("Do hoa Scroll/Content/CHAT LUONG/V-Sync", () => Data.VSync, v => Data.VSync = v);
        BindSlider("Do hoa Scroll/Content/HINH ANH/Do sang",     () => Data.Brightness, v => Data.Brightness = v);
        BindSlider("Do hoa Scroll/Content/HINH ANH/Tuong phan",  () => Data.Contrast,   v => Data.Contrast = v);
        BindSlider("Do hoa Scroll/Content/HINH ANH/Do bao hoa",  () => Data.Saturation, v => Data.Saturation = v);
        BindSwitch("Do hoa Scroll/Content/HINH ANH/Bloom",       () => Data.BloomOn,    v => Data.BloomOn = v);
        BindSlider("Do hoa Scroll/Content/HINH ANH/Cuong do Bloom", () => Data.BloomIntensity, v => Data.BloomIntensity = v);
        BindSwitch("Do hoa Scroll/Content/HINH ANH/Vignette",    () => Data.VignetteOn, v => Data.VignetteOn = v);
        BindSwitch("Do hoa Scroll/Content/HINH ANH/Motion Blur", () => Data.MotionBlurOn, v => Data.MotionBlurOn = v);
        BindQuality("Do hoa Scroll/Content/CHAT LUONG/Chat luong tong the", () => Data.QualityLevel, v => Data.QualityLevel = v);

        // ---- Tam ngam ----
        BindColor("Tam Ngam/HINH DANG/Mau sac", () => Data.CrosshairColorIndex, v => Data.CrosshairColorIndex = v);
        BindSlider("Tam Ngam/HINH DANG/Do day",      () => Data.CrosshairThickness, v => Data.CrosshairThickness = v);
        BindSlider("Tam Ngam/HINH DANG/Chieu dai",   () => Data.CrosshairSize,      v => Data.CrosshairSize = v);
        BindSlider("Tam Ngam/HINH DANG/Khoang cach", () => Data.CrosshairGap,       v => Data.CrosshairGap = v);
        BindSlider("Tam Ngam/HINH DANG/Do mo",       () => Data.CrosshairOpacity,   v => Data.CrosshairOpacity = v);
        BindSwitch("Tam Ngam/TUY CHON/Switch",       () => Data.CrosshairCenterDot, v => Data.CrosshairCenterDot = v);
        BindSwitch("Tam Ngam/TUY CHON/Vien den",     () => Data.CrosshairOutline,   v => Data.CrosshairOutline = v);

        // ---- Quyen rieng tu ----
        BindSwitch("Tai khoan/QUYEN RIENG TU/Cho phep loi moi ket ban", () => Data.AllowFriendInvites, v => Data.AllowFriendInvites = v);
        BindSwitch("Tai khoan/QUYEN RIENG TU/Hien trang thai online",   () => Data.ShowOnlineStatus,   v => Data.ShowOnlineStatus = v);

        // ---- Khoi phuc mac dinh ----
        BindReset("Chuc nang/bt_khoi phuc");
        // [RESET-TAB2] Nut khoi phuc co UISelectable -> bam se bo chon tab. Nho tab cuoi de reset DUNG nhom.
        var navRail = Find("Chuc nang");
        if (navRail != null)
        {
            _navGroupRef = navRail.GetComponent<Game.UI.UISelectionGroup>();
            if (_navGroupRef != null)
            {
                _navGroupRef.SelectionChanged += OnNavSelectionChanged;
                var sel0 = _navGroupRef.Selected;
                if (sel0 != null && sel0.Count > 0) _lastNavIndex = sel0[0].Index;
            }
        }

        EnsureQuitButton(); // [QUIT 2026-09-29 #10]
        _built = true;
    }

    // ======================= HELPERS =======================
    // Tab dang mo (panel active) -> nhom reset tuong ung.
    private string CurrentCategory()
    {
        // Uu tien: tab cuoi cung duoc chon o nav group (panel order: 0 do hoa,1 am thanh,2 chuot,3 phim,4 tam ngam,5 tai khoan).
        switch (_lastNavIndex)
        {
            case 0: return "graphics";
            case 1: return "audio";
            case 2: return "mouse";
            case 3: return "keybind";
            case 4: return "crosshair";
            case 5: return "privacy";
        }
        if (IsActive("Am thanh")) return "audio";
        if (IsActive("Chuot")) return "mouse";
        if (IsActive("Do hoa Scroll") || IsActive("Do hoa")) return "graphics";
        if (IsActive("Tam Ngam")) return "crosshair";
        if (IsActive("Tai khoan")) return "privacy";
        if (IsActive("Phim bam") || IsActive("Phim Bam")) return "keybind";
        return "all";
    }

    private void OnNavSelectionChanged(System.Collections.Generic.IReadOnlyList<Game.UI.UISelectable> sel)
    {
        if (sel != null && sel.Count > 0) { int ix = sel[0].Index; if (ix >= 0 && ix <= 5) _lastNavIndex = ix; }
    }
    private bool IsActive(string path) { var t = Find(path); return t != null && t.gameObject.activeInHierarchy; }

    private Transform Find(string path)
    {
        // path dung dau '/' , nhung ten that co dau tieng Viet -> phai match theo ten that.
        // Ta duyet tung doan, so khop KHONG dau (bo dau) de tim con.
        Transform cur = _panelRoot;
        var parts = path.Split('/');
        foreach (var part in parts)
        {
            Transform next = null;
            foreach (Transform c in cur)
            {
                if (Normalize(c.name) == Normalize(part)) { next = c; break; }
            }
            if (next == null) return null;
            cur = next;
        }
        return cur;
    }

    // Bo dau tieng Viet + lower, de so khop ten (vi path viet khong dau cho de doc).
    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.ToLowerInvariant().Trim();
        string from = "aaaaaaaaaaaaaaaaaeeeeeeeeeeeiiiiiooooooooooooooooouuuuuuuuuuuyyyyyd";
        // dung bang thay the don gian
        var sb = new System.Text.StringBuilder();
        foreach (char ch in s)
        {
            sb.Append(StripDiacritic(ch));
        }
        return sb.ToString();
    }
    private static char StripDiacritic(char c)
    {
        string a = "áàảãạăắằẳẵặâấầẩẫậ"; if (a.IndexOf(c) >= 0) return 'a';
        string e = "éèẻẽẹêếềểễệ";      if (e.IndexOf(c) >= 0) return 'e';
        string i = "íìỉĩị";            if (i.IndexOf(c) >= 0) return 'i';
        string o = "óòỏõọôốồổỗộơớờởỡợ"; if (o.IndexOf(c) >= 0) return 'o';
        string u = "úùủũụưứừửữự";       if (u.IndexOf(c) >= 0) return 'u';
        string y = "ýỳỷỹỵ";            if (y.IndexOf(c) >= 0) return 'y';
        if (c == 'đ') return 'd';
        return c;
    }

    private void BindSlider(string containerPath, Func<float> get, Action<float> set)
    {
        var t = Find(containerPath); if (t == null) { Missing(containerPath); return; }
        var s = t.GetComponentInChildren<UIValueSlider>(true); if (s == null) { Missing(containerPath + " (UIValueSlider)"); return; }
        s.ValueChanged += v => { if (Data != null) { set(v); MarkChanged(); } };
        _refreshers.Add(() => { if (Data != null) s.SetValue(get(), false); });
    }

    // Slider hien 0-100 (%), Data luu 0-1. Quy doi 2 chieu.
    private void BindSliderPercent(string containerPath, Func<float> get, Action<float> set)
    {
        var t = Find(containerPath); if (t == null) { Missing(containerPath); return; }
        var s = t.GetComponentInChildren<UIValueSlider>(true); if (s == null) { Missing(containerPath + " (UIValueSlider)"); return; }
        s.ValueChanged += v => { if (Data != null) { set(v / 100f); MarkChanged(); } };
        _refreshers.Add(() => { if (Data != null) s.SetValue(get() * 100f, false); });
    }

    private void BindSwitch(string containerPath, Func<bool> get, Action<bool> set)
    {
        var t = Find(containerPath); if (t == null) { Missing(containerPath); return; }
        var w = t.GetComponentInChildren<UISwitch>(true); if (w == null) { Missing(containerPath + " (UISwitch)"); return; }
        w.Changed += v => { if (Data != null) { set(v); MarkChanged(); } };
        _refreshers.Add(() => { if (Data != null) w.SetValueWithoutNotify(get()); });
    }

    private void BindColor(string containerPath, Func<int> get, Action<int> set)
    {
        var t = Find(containerPath); if (t == null) { Missing(containerPath); return; }
        var p = t.GetComponent<CrosshairColorPicker>(); if (p == null) p = t.GetComponentInChildren<CrosshairColorPicker>(true);
        if (p == null) { Missing(containerPath + " (CrosshairColorPicker)"); return; }
        p.IndexChanged += v => { if (Data != null) { set(v); MarkChanged(); } };
        _refreshers.Add(() => { if (Data != null) p.SetSelected(get(), false); });
    }

    private void BindQuality(string containerPath, Func<int> get, Action<int> set)
    {
        var t = Find(containerPath); if (t == null) { Missing(containerPath); return; }
        var g = t.GetComponent<UISelectionGroup>(); if (g == null) g = t.GetComponentInChildren<UISelectionGroup>(true);
        if (g == null) { Missing(containerPath + " (UISelectionGroup)"); return; }
        g.SelectionChanged += sel => { if (Data != null && sel != null && sel.Count > 0) { set(sel[0].Index); MarkChanged(); } };
        _refreshers.Add(() => { if (Data != null) g.SelectByIndex(Mathf.Clamp(get(), 0, Mathf.Max(0, g.ItemCount - 1)), false); });
    }

    private void BindReset(string buttonPath)
    {
        var t = Find(buttonPath); if (t == null) { Missing(buttonPath); return; }
        var btn = t.GetComponent<UnityEngine.UI.Button>(); if (btn == null) { Missing(buttonPath + " (Button)"); return; }
        btn.onClick.AddListener(() => {
            var cat = CurrentCategory(); Debug.Log("[SettingsBinder] Reset nhom = " + cat + " (navIdx=" + _lastNavIndex + ")"); // [RESET-TAB2]
            if (SettingManager.Instance != null) SettingManager.Instance.ResetCategory(cat);
            RefreshAll();
        });
    }

    // Tao nut "THOÁT GAME" o rail Chức năng (fresh, khong clone -> tranh dinh UISelectable/tab).
    private void EnsureQuitButton()
    {
        var rail = Find("Chuc nang"); if (rail == null) { Missing("Chuc nang (quit)"); return; }
        foreach (Transform ch in rail) if (ch.name == "bt_thoát game")
        {
            var eb = ch.GetComponent<UnityEngine.UI.Button>();
            if (eb != null) { eb.onClick.RemoveListener(QuitGame); eb.onClick.AddListener(QuitGame); } // noi hanh dong vao nut co san trong scene
            return;
        }

        var tmpl = Find("Chuc nang/bt_tai khoan");
        UnityEngine.UI.Image tImg = tmpl != null ? tmpl.GetComponent<UnityEngine.UI.Image>() : null;
        TMPro.TextMeshProUGUI tTxt = tmpl != null ? tmpl.GetComponentInChildren<TMPro.TextMeshProUGUI>(true) : null;

        var go = new GameObject("bt_thoát game", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        go.transform.SetParent(rail, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(229f, 54f);
        rt.anchoredPosition = new Vector2(-821f, -240.25f);

        var img = go.GetComponent<UnityEngine.UI.Image>();
        if (tImg != null) { img.sprite = tImg.sprite; img.type = tImg.type; img.pixelsPerUnitMultiplier = tImg.pixelsPerUnitMultiplier; img.color = tImg.color; } // [QUIT-FIX] nen nhu tab thuong
        else img.color = Color.white;

        var lbl = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        lbl.transform.SetParent(go.transform, false);
        var lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(42f, 0f); lrt.offsetMax = new Vector2(-8f, 0f); // [QUIT-FIX] can trai giong cac tab
        var txt = lbl.GetComponent<TMPro.TextMeshProUGUI>();
        txt.text = "THOÁT GAME";
        txt.alignment = TMPro.TextAlignmentOptions.Left;
        txt.enableWordWrapping = false;
        if (tTxt != null) { txt.font = tTxt.font; txt.fontSize = tTxt.fontSize; txt.fontStyle = tTxt.fontStyle; }
        else txt.fontSize = 20f;
        txt.color = new Color(0.92f, 0.26f, 0.26f, 1f); // [QUIT-FIX] chu MAU DO

        var btn = go.GetComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(QuitGame);
    }

    private void QuitGame()
    {
        Debug.Log("[SettingsBinder] Thoát game.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Missing(string path) { Debug.LogWarning("[SettingsBinder] Khong tim thay control: " + path); }
}
