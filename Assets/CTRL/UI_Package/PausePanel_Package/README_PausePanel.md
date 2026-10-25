# PausePanel_Package — Bàn giao Pause Settings

Gói này chứa phần **Cài đặt (Settings) cho Pause panel** đã dựng ở project UI, để nối vào interface trong dự án chính.

## Nội dung gói (Assets/PausePanel_Package/)
- `PauseSettings.prefab` — Prefab gồm **Settings** (Backdrop + Nav 5 tab + SettingsContent: Đồ họa/Âm thanh/Phím/Chuột/Tâm ngắm) + **Decor** (2 divider line). Đã wire sẵn: SettingsBinder, SettingsPanelController, UIValueSlider(15), UISwitch(9), CrosshairColorPicker(1), KeybindRow(8), KeybindViewSwitcher(1). 0 missing script.
- `PausePanelReset.cs` — **SCRIPT MỚI** (component đặt trên Pause panel: mở Pause là reset về Menu, ẩn Settings).
- `README_PausePanel.md` — file này.

## Cách nối vào 1 interface (FFA/TDM/…)
1. Kéo `PauseSettings.prefab` vào **con của `Pause_ panel`** (cùng cấp với Dim/Header/Menu). Có thể Unpack Prefab nếu muốn tách rời.
2. Trên `Pause_ panel` thêm component **PausePanelReset**, gán `_settings` = object Settings (trong prefab), `_menu` = object Menu của panel.
3. Nút mở Cài đặt (Menu/Item_Settings/Row → Button) `onClick` thêm 2 lệnh: `Settings.SetActive(true)` và `Menu.SetActive(true)` (giữ lệnh RequestSettings sẵn có nếu có).
4. HUD Crosshair của interface: thêm component **CrosshairController**, gán 5 phần (center/up/down/left/right = Background + 2 Vertical Divider + 2 Horizontal Divider) và `_palette` = 6 màu swatch (#4FD6C9,#4DFF88,#FF4D4D,#F2C14E,#FF4DFF,#FFFFFF) — khớp thứ tự CrosshairColorPicker.

## PHỤ THUỘC — các script/asset PHẢI có sẵn trong dự án (KHÔNG kèm trong gói để tránh đè bản lead đã sửa)
Thuộc module Settings/MenuShell của project UI:
- Scripts: `SettingManager`, `SettingData`, `SettingsBinder`, `SettingsPanelController`, `UIValueSlider`, `UISwitch`, `CrosshairColorPicker`, `CrosshairController`, `KeybindRow`, `KeybindViewSwitcher`, `UISelectionGroup`, `UISelectable`, `ModalDialog`, `CenterNotice` (nếu dùng), `SceneLoader`.
- Asset: `PlayerSettingData.asset` (ScriptableObject), AudioMixer (nếu dùng nhạc/SFX), font ChakraPetch + các sprite switch/slider/tab/crosshair mà prefab tham chiếu.
- Prefab tham chiếu các script/asset trên qua GUID — nếu dự án chính đã có (cùng nguồn) sẽ tự khớp; nếu thiếu sẽ hiện 'missing'. Khi đó import kèm module Settings/MenuShell.

## SCRIPT ĐÃ THAY ĐỔI (lead tự MERGE, gói không kèm để không đè)
- `KeybindRow.cs` — hàm `RefreshLabel()`: hiển thị phím IN HOA. Sửa dòng cuối thành:
  `_keyLabel.text = string.IsNullOrEmpty(disp) ? disp : disp.ToUpperInvariant();`
  (thêm biến `string disp = TISRebindingSystem.GetShortDisplayName(path);` trước đó.)
- (Comment-only, không bắt buộc): SettingManager.cs, SettingsPanelController.cs, CrosshairController.cs có thêm chú thích 'LEAD - cách wire'.

## Runtime cần
- Có **1 SettingManager** trong luồng game (persist từ scene đầu qua DontDestroyOnLoad), gán `_settingData` = PlayerSettingData, `_postVolume` = Global Volume scene game, `_mixer` = AudioMixer (nếu có).
- **Đổi phím** chỉ chạy khi scene có `TISInputBridge`.
- Lưu/nạp setting qua PlayFab (SettingsService) — cần đăng nhập.

## Còn cần code phía game (chưa làm)
- Áp **FOV** lên camera (đọc SettingData.Fov / OnSettingsApplied).
- Áp **độ nhạy chuột** (OnSensitivityChanged / SettingData).
