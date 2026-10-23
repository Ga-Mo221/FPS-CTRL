using System;
using System.Collections.Generic;
using UnityEngine;
using Game.UI;
using TMPro;

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
        // doi thiet bi khi panel dang mo -> slider do nhay hien dung slot PC/tay cam
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
        //  Gyro tay cam (MotionSettingsApplier ap xuong TIS). Nut "Hieu chinh" tu xu ly (GyroCalibrateButton).
        BindCycler("Chuot/GYRO/Che do gyro", MotionSettingsApplier.ModeOptions, () => Data.GyroMode, v => Data.GyroMode = v);
        BindSlider("Chuot/GYRO/Nhay gyro ngang", () => Data.GyroSensX, v => Data.GyroSensX = v);
        BindSlider("Chuot/GYRO/Nhay gyro doc",   () => Data.GyroSensY, v => Data.GyroSensY = v);
        BindSwitch("Chuot/TUY CHON GYRO/Dao truc ngang", () => Data.GyroInvertX, v => Data.GyroInvertX = v);
        BindSwitch("Chuot/TUY CHON GYRO/Dao truc doc",   () => Data.GyroInvertY, v => Data.GyroInvertY = v);

        // ---- Do hoa (trong ScrollRect) ----
        BindCycler("Do hoa Scroll/Content/HIEN THI/Ngon ngu", Game.UI.Loc.LanguageNames, () => Data.Language, v => Data.Language = v); 
        BindSlider("Do hoa Scroll/Content/HIEN THI/Truong nhin (FOV)", () => Data.Fov, v => Data.Fov = v);
        BindSwitch("Do hoa Scroll/Content/CHAT LUONG/V-Sync", () => Data.VSync, v => Data.VSync = v);
        BindSlider("Do hoa Scroll/Content/HINH ANH/Do sang",     () => Data.Brightness, v => Data.Brightness = v);
        BindSlider("Do hoa Scroll/Content/HINH ANH/Tuong phan",  () => Data.Contrast,   v => Data.Contrast = v);
        BindSlider("Do hoa Scroll/Content/HINH ANH/Do bao hoa",  () => Data.Saturation, v => Data.Saturation = v);
        BindSwitch("Do hoa Scroll/Content/HINH ANH/Bloom",       () => Data.BloomOn,    v => Data.BloomOn = v);
        BindSlider("Do hoa Scroll/Content/HINH ANH/Cuong do Bloom", () => Data.BloomIntensity, v => Data.BloomIntensity = v);
        BindSwitch("Do hoa Scroll/Content/HINH ANH/Vignette",    () => Data.VignetteOn, v => Data.VignetteOn = v);
        BindSwitch("Do hoa Scroll/Content/HINH ANH/Motion Blur", () => Data.MotionBlurOn, v => Data.MotionBlurOn = v);
        // Chong rung camera (CameraStabilizer): dong moi tao luc chay tu dong Motion Blur -> khong can sua scene/prefab.
        EnsureSwitchRowBelow("Do hoa Scroll/Content/HINH ANH/Motion Blur", "Chống rung camera");
        BindSwitch("Do hoa Scroll/Content/HINH ANH/Chong rung camera", () => Data.StableCamera, v => Data.StableCamera = v);
        // [Aemy 2026-10-22] Muc chong rung (0-100%): clone dong slider "Cuong do Bloom" dat duoi dong Chong rung camera.
        EnsureRowBelow("Do hoa Scroll/Content/HINH ANH/Cuong do Bloom", "Do hoa Scroll/Content/HINH ANH/Chong rung camera", "Mức chống rung");
        SetSliderPercentRange("Do hoa Scroll/Content/HINH ANH/Muc chong rung", 0f, 100f, true);
        BindSliderPercent("Do hoa Scroll/Content/HINH ANH/Muc chong rung", () => Data.StableCameraStrength, v => Data.StableCameraStrength = v);
        BindQuality("Do hoa Scroll/Content/CHAT LUONG/Chat luong tong the", () => Data.QualityLevel, v => Data.QualityLevel = v);
        // Ket cau + Khu rang cua: truoc chi la chu tinh, gio chon duoc va co hieu luc.
        BindCycler("Do hoa Scroll/Content/CHAT LUONG/Ket cau (Texture)", GraphicsQualityApplier.TextureOptions, () => Data.TextureQuality, v => Data.TextureQuality = v);
        BindCycler("Do hoa Scroll/Content/CHAT LUONG/Khu rang cua (AA)", GraphicsQualityApplier.AAOptions, () => Data.AntiAliasing, v => Data.AntiAliasing = v);
        // Hien thi: truoc chi la chu tinh -> nay la o chon ◄ ► (DisplaySettingsApplier ap xuong engine).
        BindCycler("Do hoa Scroll/Content/HIEN THI/Che do man hinh", DisplaySettingsApplier.ModeOptions, () => Data.DisplayMode, v => Data.DisplayMode = v);
        BindCycler("Do hoa Scroll/Content/HIEN THI/Do phan giai", DisplaySettingsApplier.ResolutionOptions(), () => DisplaySettingsApplier.ResolutionIndex(Data), v => DisplaySettingsApplier.SetResolutionIndex(Data, v));
        BindCycler("Do hoa Scroll/Content/HIEN THI/Tan so quet", DisplaySettingsApplier.RefreshOptions(), () => DisplaySettingsApplier.RefreshIndex(Data), v => DisplaySettingsApplier.SetRefreshIndex(Data, v));
        BindCycler("Do hoa Scroll/Content/HIEN THI/Gioi han FPS", DisplaySettingsApplier.FpsOptions(), () => DisplaySettingsApplier.FpsIndex(Data), v => DisplaySettingsApplier.SetFpsIndex(Data, v));

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
        //string from = "aaaaaaaaaaaaaaaaaeeeeeeeeeeeiiiiiooooooooooooooooouuuuuuuuuuuyyyyyd";
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

    private void BindCycler(string containerPath, string[] options, Func<int> get, Action<int> set)
    {
        var t = Find(containerPath); if (t == null) { Missing(containerPath); return; }
        var c = t.GetComponentInChildren<UIOptionCycler>(true); if (c == null) { Missing(containerPath + " (UIOptionCycler)"); return; }
        if (options != null) c.SetOptions(options);
        c.Changed += v => { if (Data != null) { set(v); MarkChanged(); } };
        _refreshers.Add(() => { if (Data != null) c.SetIndex(get(), false); });
    }

    // Them 1 dong switch ngay duoi dong mau (clone ca nhan + switch + TIS), khong can sua scene/prefab.
    // Cac dong dat theo vi tri tuyet doi -> tu tinh buoc = khoang cach dong mau voi dong tren no,
    // day cac muc ben duoi xuong 1 buoc va keo dai vung cuon.
    private void EnsureSwitchRowBelow(string templatePath, string label) { EnsureRowBelow(templatePath, templatePath, label); }

    // [Aemy 2026-10-22] Tong quat: clone dong `templatePath` (switch / slider...) va dat ngay duoi dong `afterPath`
    // (cung 1 muc). Vd slider "Muc chong rung" clone tu "Cuong do Bloom" nhung nam duoi "Chong rung camera".
    private void EnsureRowBelow(string templatePath, string afterPath, string label)
    {
        var src = Find(templatePath) as RectTransform; if (src == null) { Missing(templatePath); return; }
        var tpl = Find(afterPath) as RectTransform; if (tpl == null) { Missing(afterPath); return; }
        var section = tpl.parent;
        foreach (Transform c in section) if (Normalize(c.name) == Normalize(label)) return; // da co (scene da them tay)

        int idx = tpl.GetSiblingIndex();
        float step = 64f;
        if (idx > 0 && section.GetChild(idx - 1) is RectTransform above)
            step = Mathf.Abs(above.anchoredPosition.y - tpl.anchoredPosition.y);

        // Dong cung muc ben duoi dong mau + cac muc khac trong Content nam duoi muc nay -> day xuong.
        for (int i = idx + 1; i < section.childCount; i++)
            if (section.GetChild(i) is RectTransform below) below.anchoredPosition -= new Vector2(0f, step);
        var content = section.parent as RectTransform;
        if (content != null && section is RectTransform sectionRt)
        {
            foreach (Transform c in content)
                if (c != section && c is RectTransform other && other.anchoredPosition.y < sectionRt.anchoredPosition.y)
                    other.anchoredPosition -= new Vector2(0f, step);
            content.sizeDelta += new Vector2(0f, step);
        }

        var row = (RectTransform)Instantiate(src.gameObject, section, false).transform;
        row.name = label;
        row.SetSiblingIndex(idx + 1);
        InsertNavBelow(tpl, row); // [2026-10-23] noi tay cam: dong moi nam giua `afterPath` va dong ben duoi trong chuoi len/xuong
        row.anchoredPosition = new Vector2(src.anchoredPosition.x, tpl.anchoredPosition.y - step); // x theo dong mau (con giu dung cho)
        var txt = row.GetComponent<TMPro.TMP_Text>(); if (txt != null) txt.text = label;

        // Nhan can giua + ContentSizeFitter, x moi dong duoc canh tay theo do rong chu -> giu MEP TRAI nhu dong mau
        // (chu dai hon se lan sang trai bi mask cat), roi day con (switch, vach ke) ve dung cho cu.
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(row);
        float tplLeft = src.anchoredPosition.x - src.rect.width * src.pivot.x;
        float dx = tplLeft + row.rect.width * row.pivot.x - row.anchoredPosition.x;
        row.anchoredPosition += new Vector2(dx, 0f);
        foreach (Transform c in row) if (c is RectTransform crt) crt.anchoredPosition -= new Vector2(dx, 0f);

        // [Aemy 2026-10-22] Vung cuon phai chua HET dong moi: dong slider co thanh truot + vach ke nam DUOI chu,
        // ma buoc dong lay theo dong switch -> o Pause (Content sat day) thanh truot bi mask cat mat.
        if (content != null)
        {
            var corners = new Vector3[4];
            float lowest = float.MaxValue;
            foreach (var r in row.GetComponentsInChildren<RectTransform>(true))
            {
                r.GetWorldCorners(corners);
                float y = content.InverseTransformPoint(corners[0]).y;
                if (y < lowest) lowest = y;
            }
            float need = content.rect.yMax - lowest + 24f; // chua toi diem thap nhat + le duoi
            if (lowest < float.MaxValue && need > content.rect.height)
                content.sizeDelta += new Vector2(0f, need - content.rect.height);
        }

        // Tay cam: dang ky nut moi voi khung dieu huong TIS
        var ctx = row.GetComponentInParent<GaMo.TrueInputSystem.TISUINavigationContext>(true);
        if (ctx != null) ctx.ScanChildButtons();
    }

    // [2026-10-23] Dong clone giu nguyen navUp/navDown cua dong MAU, va khong dong nao tro TOI no
    // -> tay cam / phim mui ten khong bao gio vao duoc dong moi (chi chuot bam duoc).
    // Chen nut TIS cua dong moi vao chuoi len/xuong: after -> moi -> (dong truoc day nam duoi after).
    // Nhieu dong chen lien tiep (Chong rung -> Muc chong rung) tu noi dung thu tu vi moi lan chen doc lai navDown hien tai.
    private static void InsertNavBelow(Transform afterRow, Transform newRow)
    {
        var mine = newRow.GetComponentsInChildren<GaMo.TrueInputSystem.TISGamepadUIButton>(true);
        var prevs = afterRow.GetComponentsInChildren<GaMo.TrueInputSystem.TISGamepadUIButton>(true);
        if (mine.Length == 0 || prevs.Length == 0) return;
        var me = mine[0];
        var prev = prevs[0];
        var next = prev.navDown;

        me.navUp = prev;
        me.navDown = next;
        me.navLeft = null;   // truc ngang nhuong cho TISValueNav (slider/switch) nhu cac dong khac
        me.navRight = null;
        me.isFirstSelected = false;

        // Moi nut cua dong `after` (vd nhom nhieu nut) dang tro xuong `next` -> tro xuong dong moi.
        foreach (var p in prevs) if (p.navDown == next) p.navDown = me;
        // Dong ben duoi (neu dang tro len `after`) -> tro len dong moi.
        if (next != null && next.navUp == prev) next.navUp = me;
    }

    private void SetSliderPercentRange(string containerPath, float min, float max, bool whole)
    {
        var t = Find(containerPath); if (t == null) return;
        var s = t.GetComponentInChildren<UIValueSlider>(true); if (s == null) return;
        s.SetRange(min, max, whole);
        s.SetFormat("0", ""); // font khong co glyph "%" -> chi hien so 0-100 giong cac slider khac
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
        txt.textWrappingMode = TextWrappingModes.NoWrap;
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
