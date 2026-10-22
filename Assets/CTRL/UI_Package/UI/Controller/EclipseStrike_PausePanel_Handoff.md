# Eclipse Strike — Bàn giao Pause Panel (Settings) cho Lead

Tài liệu hướng dẫn nối **Pause panel + mục Cài đặt (Settings)** vào interface game thật (FFA & TDM/DM).

---

## 1. Tổng quan

Pause Settings **tái dùng nguyên hệ setting của MenuShell** (không dựng lại), nên chỉnh trong Pause = chỉnh trong Menu (chung 1 dữ liệu, chung pipeline apply/save).

- Dữ liệu: `SettingData` (ScriptableObject) — asset `Assets/Scripts/Settings/PlayerSettingData.asset`.
- Quản lý: `SettingManager` (singleton, `DontDestroyOnLoad`) — `Load()`/`Save()` (JSON local + PlayFab) và `Apply()` áp xuống engine (audio, post-processing, quality) + bắn event `OnSettingsApplied` (crosshair) & `OnSensitivityChanged` (camera).
- Nối UI ↔ data: `SettingsBinder` (tự tìm control theo path, ghi vào `SettingData` + gọi `Apply()`).

Đã dựng sẵn trong scene `Assets/Scenes/test/SampleScene.unity`:
- `Game_FFA_interface / Pause_ panel / Settings`
- `Game_DM_interface / Pause_ panel / Settings` (bản sao y hệt FFA)

---

## 2. Cấu trúc mỗi Pause panel

```
Pause_ panel            (+ PausePanelReset: _settings, _menu)
├─ Dim
├─ Header
├─ Menu                 (Item_Resume / Item_Settings / Item_Leave)
├─ Settings             (+ SettingsPanelController: _navGroup=Nav, _panels[5])
│   ├─ Backdrop
│   ├─ Nav              (UISelectionGroup: 5 tab Đồ họa/Âm thanh/Phím/Chuột/Tâm ngắm)
│   └─ SettingsContent  (+ SettingsBinder: panelRoot = chính nó)
│       ├─ Đồ họa Scroll   (ScrollRect + Content: HIỂN THỊ / CHẤT LƯỢNG / HÌNH ẢNH)
│       ├─ Âm thanh
│       ├─ Phím bấm        (KeybindViewSwitcher: đổi view PC ↔ Tay cầm)
│       ├─ Chuột
│       └─ Tâm Ngắm        (có Preview crosshair + CrosshairColorPicker)
└─ Decor                (2 divider line)
```

- **Tab điều khiển:** `SettingsPanelController` nghe `Nav` (UISelectionGroup) → hiện 1 mục, ẩn còn lại. Thứ tự `_panels` khớp thứ tự tab: [Đồ họa, Âm thanh, Phím, Chuột, Tâm ngắm].
- **Nút Cài đặt:** `Menu/Item_Settings/Row` (Button) `onClick`:
  1. `GameXHudController.RequestSettings`
  2. `Settings.SetActive(true)`
  3. `Menu.SetActive(true)`
- **Reset:** `PausePanelReset.OnEnable` → `Settings.SetActive(false)` + `Menu.SetActive(true)` (mỗi lần mở Pause về Menu, không dính mục Cài đặt cũ).

---

## 3. Checklist nối vào interface game thật (QUAN TRỌNG)

Khi merge 2 interface (FFA/DM) này vào scene game thật, Lead cần đảm bảo:

**a. Có `SettingManager` trong luồng game.** Bình thường nó đã tồn tại từ scene `MenuShell` và `DontDestroyOnLoad` sang scene game. Nếu vào thẳng scene game (không qua Menu), phải có 1 `SettingManager` trong scene, gán:
- `_settingData` = `PlayerSettingData.asset`
- `_postVolume` = Global Volume của scene game (URP)
- (tuỳ chọn) `_mixer` + param cho nhạc/SFX.
> Trong SampleScene em đã đặt sẵn 1 object `SettingManager (Scene)` để test; scene game thật nên dùng cái persist từ Menu hoặc tự đặt 1 cái tương tự.

**b. Global Volume của scene game** phải có các override: `ColorAdjustments` (độ sáng/tương phản/bão hòa), `Bloom`, `Vignette`, `MotionBlur`. Thiếu `ColorAdjustments` thì 3 slider hình ảnh không ăn (em đã thêm vào `SampleSceneProfile.asset`).

**c. HUD Crosshair** của mỗi interface cần component `CrosshairController` (đã gắn sẵn cho FFA & DM Crosshair). Nếu tạo interface mới: add `CrosshairController`, gán `_center/_up/_down/_left/_right` = các phần của crosshair, và `_palette` = **đúng 6 màu swatch** của color picker theo thứ tự:
`#4FD6C9, #4DFF88, #FF4D4D, #F2C14E, #FF4DFF, #FFFFFF` (teal, xanh lá, đỏ, vàng, hồng, trắng). Sai thứ tự = chọn màu ra sai màu.

**d. Nút Item_Settings** phải gọi đủ 3 việc ở mục 2 (đã wire cho cả FFA & DM).

---

## 4. Các hook cần code phía game (chưa làm — cần Lead)

`SettingManager.Apply()` tự lo: âm lượng (AudioListener/mixer), post-processing, V-Sync, mức chất lượng, crosshair (qua event). **Còn thiếu 2 chỗ cần game hook:**

- **FOV (Trường nhìn):** `SettingManager` KHÔNG tự set FOV lên camera — mới chỉ ghi `SettingData.Fov`. Player camera cần đăng ký `SettingManager.OnSettingsApplied` và set `camera.fieldOfView = data.Fov` (hoặc đọc `SettingData.Fov` khi spawn).
- **Độ nhạy chuột (Sensitivity):** `SettingManager.OnSensitivityChanged(float)` bắn giá trị mới → game hook để áp vào camera/RPC (thay cho việc gọi trực tiếp NPlayer). ADS/sniper đọc thêm `SettingData.RifleADSSensitivity` / `SniperADSSensitivity` / `InvertY` / `MouseAccel`.

---

## 5. Keybind (PC & Tay cầm)

- **PC:** mục Phím dùng `KeybindRow` (TrueInputSystem). Rebind chỉ chạy khi scene game có `TISInputBridge`. Action khớp profile `Assets/Scripts/Settings/EclipseControlsProfile.asset` (Gameplay map: Jump/Crouch/Sprint/Reload/Weapon1/Weapon2/Utility1/Utility2). Phím mới hiển thị **IN HOA**.
- **Tay cầm:** `KeybindViewSwitcher` gạt sang sơ đồ `Controller View` — đã điền glyph theo keymap Xbox, line trỏ đúng nút. 4 nút cứng **Bắn / Ngắm / Di chuyển / Camera** đã khoá (không rebind).
  - Keymap: Di chuyển=L stick, Camera=R stick, Chạy=L3, Nhảy=A, Ngồi=B, Thay đạn=Y, Tương tác/Nhặt=D-Up, Đổi chế độ=D-Down, Đổi vũ khí=X, Ném bom=LB/RB, Bắn=RT, Ngắm=LT, Mở cài đặt=Start, Bảng điểm=Select.

---

## 6. Files liên quan

- Scenes: `Assets/Scenes/test/SampleScene.unity` (FFA+DM), `Assets/Scenes/MenuShell.unity` (nguồn settings).
- Data: `Assets/Scripts/Settings/PlayerSettingData.asset`, `EclipseControlsProfile.asset`, `Assets/Settings/SampleSceneProfile.asset`.
- Scripts: `SettingManager.cs`, `SettingData.cs`, `SettingsBinder.cs`, `SettingsPanelController.cs`, `PausePanelReset.cs`, `UIValueSlider.cs`, `UISwitch.cs`, `CrosshairColorPicker.cs`, `CrosshairController.cs`, `KeybindRow.cs`, `KeybindViewSwitcher.cs`.

---

## 7. Ghi chú

- Layout Pause được căn gọn cho khung Pause (rộng hơn/khác MenuShell một chút ở Âm thanh/Chuột/Đồ họa); MenuShell giữ layout gốc. Cả hai chung `SettingData` nên chỉnh nơi này đồng bộ nơi kia.
- `SettingsBinder` sẽ log warning cho `Chuc nang/bt_khoi phuc` (nút Khôi phục) và 2 switch `Tài khoản` — do Pause cố ý bỏ 2 mục này, KHÔNG phải lỗi.
- DM/TDM là bản sao y hệt FFA; sửa gì nên sửa cả 2 (hoặc tách thành prefab dùng chung — gợi ý cải tiến sau).
