# TIS Tutorials — Scene hướng dẫn tương tác

Ba scene hướng dẫn cách setup và dùng True Input System, mỗi scene cho một kiểu điều khiển. Mỗi bài có:

- **Mô tả**: bài dạy gì, áp dụng vào game ra sao.
- **Cách setup**: các bước trong Setup Tool / Inspector / profile.
- **Code mẫu**: tô màu cú pháp, có nút *Sao chép*.
- **API đang chạy**: giá trị API cập nhật theo thời gian thực.
- **Thử ngay**: nhiệm vụ trên sân chơi 3D, tự tích khi bạn làm được. Tiến độ được lưu lại.

## Bắt đầu

1. Mở một scene trong `Tutorials/Scenes/`:

   | Scene | Nội dung | Số bài |
   |---|---|---|
   | `TIS_Tut_01_ChuotBanPhim` | Chuột + bàn phím | 15 |
   | `TIS_Tut_02_TayCam` | Tay cầm (Xbox, PlayStation, Switch) | 12 |
   | `TIS_Tut_03_Mobile` | Cảm ứng (joystick ảo, nút kỹ năng, MOBA) | 11 |

2. Nhấn **Play**.
3. Chuyển bài:
   - bấm danh sách bên trái hoặc **Bài trước / Bài tiếp theo**;
   - bàn phím: **PageUp / PageDown**;
   - tay cầm: **VIEW + LB / RB** (**VIEW + Y** để xem code).
4. Ba tab ở thanh trên chuyển giữa 3 scene. Trong Editor, việc chuyển scene không cần Build Settings. Ở bản build, bạn phải thêm cả 3 scene vào Build Settings.

## Danh sách bài

**Chuột + Bàn phím:**
1. Chào mừng
2. Di chuyển (`MoveDirection`)
3. Xoay camera (`LookDelta`)
4. Zoom (`ZoomDelta`)
5. Nhảy, chạy, tích lực (`IsSkillDown` / `IsSkillHeld` / `IsSkillReleased`)
6. 3 chế độ đọc nút (`GetSkillAction` + `TISButtonReadMode`)
7. Input buffer (`TISInputBuffer`)
8. Tổ hợp phím Ctrl + S (PC Modifier)
9. Đọc action bất kỳ (`GetVector2Action` / `GetAxisAction` / `TISActionHandle`)
10. Con trỏ & chọn vật (Object Tracker, `GetPointer*`)
11. Action Map (tạm dừng game)
12. Ghi đè input (`OverrideMoveInput`)
13. Đổi profile (đi bộ ↔ lái xe, `SwapProfile`)
14. Đổi phím (`TISRebindingSystem`)
15. Mini-game săn sao

**Tay cầm:**
1. Chào mừng
2. Nhận diện tay cầm (`ActiveDevice`)
3. Cần analog (deadzone, curve, `ApplyDeadzone`)
4. Nút, cò & tổ hợp (trigger như trục / nút, Gamepad Modifier)
5. Rung (`TryRumble` / `StopRumble`)
6. Aim assist (`AimAssistProcess` + `TISTargetScanner`)
7. Điều hướng menu (`TISGamepadUIButton`, Context, Group)
8. Con trỏ ảo (`TISGamepadVirtualCursor`)
9. Gyro (+ Gyro, `Motion`, bật/tắt, hiệu chỉnh)
10. Touchpad
11. Đổi nút tay cầm
12. Mini-game có rung

**Mobile:**
1. Chào mừng
2. Bật điều khiển cảm ứng (`TISMobileInputSetup`)
3. Joystick ảo
4. Vuốt xoay camera
5. Nút kỹ năng (`TISMobileSkillButton`)
6. Hồi chiêu & khoá nút
7. Kỹ năng định hướng MOBA (`TISDirectionalSkillButton`)
8. Chạm chọn vật
9. Cử chỉ nhiều ngón
10. Tay cầm Bluetooth trên điện thoại
11. Mini-game

## Lưu ý

- **Mobile trong Editor:**
  - Mặc định TIS chỉ giả lập mobile khi chạy trong cửa sổ **Device Simulator**.
  - Scene Mobile có thêm `TISTutForceMobile` để chạy được cả trong Game view; bỏ chọn *Force In Game View* nếu muốn theo mặc định của TIS.
  - Chuột chỉ giả lập được **1 ngón**. Các nhiệm vụ cần 2 ngón (chụm zoom, cử chỉ nhiều ngón, vừa giữ joystick vừa vuốt) phải thử trên điện thoại thật: Unity Remote 5 hoặc bản build.
- **Gyro / touchpad không có tay cầm:** profile Tay cầm bật *Simulate In Editor*.
  - Gyro: giữ **I J K L** (thêm Right Shift để xoay nhanh).
  - Touchpad: giữ **T** + rê chuột = vuốt, **G** = bấm.
- **Rung, hiệu chỉnh gyro, vuốt touchpad thật** cần tay cầm hỗ trợ (DualShock 4 / DualSense / Switch Pro qua SDL3). Tay cầm chỉ chạy XInput (Xbox, đầu thu 2.4G) không có gyro.
- **Đổi phím** được lưu thật vào file rebinding của profile. Dùng nút *Khôi phục mặc định* trong bài để trả lại.
- **Tiến độ** được lưu bằng PlayerPrefs, key dạng `TISTut_<Mode>_...`.

## Dựng lại scene / profile (dành cho người bảo trì)

Scene, profile, font, theme, vật liệu đều đã có sẵn — người dùng **không cần** dựng lại. Code builder nằm ở `Tutorials/Editor/` và **không hiện trên menu** để tránh bấm nhầm ghi đè.

Khi cần (sửa / thêm bài học):

1. Thêm define `TIS_TUTORIAL_DEV` vào *Project Settings › Player › Scripting Define Symbols*.
2. Menu **Tools › True Input System › Tutorials** sẽ hiện các lệnh: tạo font / theme / vật liệu, tạo profile hướng dẫn, dựng từng scene hoặc cả 3.
3. Xong việc thì bỏ define đi.

> Builder **ghi đè** scene và action map của các profile `TIS_Tut_*`. Scene đang mở có thay đổi chưa lưu sẽ được bỏ qua.

## Cấu trúc

```
Tutorials/
  Scripts/Core/          Controller, lớp nền bài học, nhân vật, camera, sân chơi
  Scripts/UI/            Theme, nút chéo, phím minh hoạ, widget tay cầm, factory UI
  Scripts/KeyboardMouse/ Bài học Chuột + Bàn phím   (TISTutKbm_*)
  Scripts/Gamepad/       Bài học Tay cầm             (TISTutGp_*)
  Scripts/Mobile/        Bài học Mobile + HUD cảm ứng (TISTutMb_*)
  Editor/                Builder scene, profile, font / theme / vật liệu
  Fonts/                 Be Vietnam Pro, JetBrains Mono (giấy phép SIL OFL — xem OFL.txt)
  Profiles/ Scenes/ Materials/ Sprites/
```

## Thêm bài học mới

1. Tạo class kế thừa `TISTutLesson`.
2. Override `Title`, `Summary`, `SetupSteps`, `Code`, `Objectives`.
3. Trong `OnEnter`:
   - gọi `Watch("Tên API", () => giá trị, () => 0..1)` để thêm dòng vào bảng API;
   - tạo đồ vật trong `Stage`, UI trong `Overlay` (cả hai tự xoá khi đổi bài).
4. Trong `OnTick`, gọi `Complete(i)` khi người chơi làm xong nhiệm vụ thứ `i`.
5. Thêm kiểu bài vào `LessonsFor(mode)` trong `Editor/TISTutSceneBuilder.cs`, rồi dựng lại scene (xem mục trên).
