# GaMo Inspector — đã chia nhỏ từ Customs_Inspector.cs

Gốc: 1 file 5649 dòng → 28 file `partial class CustomsEditor` + 2 class độc lập, mỗi file
một mảng tính năng. Đặt **cả 28 file vào cùng 1 thư mục `Editor/`** (y hệt chỗ file gốc đang
nằm) — `partial class` bắt buộc chung namespace + cùng assembly, nhưng không bắt buộc cùng
file nên không có file nào thiếu gì cả, bạn không cần sửa gì thêm.

Đã kiểm tra: không trùng dòng, không sót dòng, ngoặc `{}` cân bằng từng file (bỏ qua chuỗi/
comment khi đếm), giữ nguyên 100% code gốc — chỉ thêm `using` + khung `namespace` +
`partial class CustomsEditor { }` bao quanh mỗi phần.

## 2 file "quản lý" theo đúng yêu cầu
- **GaMoInspector.ComponentButtons.cs** — quản lý nút component (tab): click chọn/thêm tab,
  thứ tự, `DrawComponentBar`, `CycleTab`.
- **GaMoInspector.Shortcuts.cs** — quản lý phím tắt: `OnRootKeyDown`/`HandleShortcuts`
  (Ctrl+[ ], Ctrl+F, Alt+←/→...), lịch sử chọn Back/Forward.

## Danh sách file còn lại (theo thứ tự code gốc)
| File | Nội dung |
|---|---|
| GaMoInspector.Core.cs | Khai báo class, màu/const, GUIStyle dùng chung |
| GaMoInspector.Lifecycle.cs | Mở cửa sổ, OnEnable/OnDisable, PreviewPump |
| GaMoInspector.SelectionEvents.cs | Theo dõi Selection/ObjectChangeEvents |
| GaMoInspector.Persistence.cs | Lưu/khôi phục trạng thái qua recompile, GlobalObjectId |
| GaMoInspector.SceneHandles.cs | Ẩn Gizmos/Handles trùng của Inspector gốc |
| GaMoInspector.Binding.cs | Gán GameObject/Asset đang chọn |
| GaMoInspector.AssetPanel.cs | Thân Inspector cho Asset, Preview, Apply/Revert |
| GaMoInspector.ComponentSelectionState.cs | Lưu/khôi phục component đang chọn |
| GaMoInspector.UIToolkitShell.cs | CreateGUI, cuộn chuột, kéo-thả script vào cửa sổ |
| GaMoInspector.ChunkRendering.cs | Hạ tầng vẽ IMGUI theo "chunk", đồng bộ UI |
| GaMoInspector.DrawHelpers.cs | Vẽ nền bo tròn |
| GaMoInspector.StaticFlags.cs | Dropdown Static |
| GaMoInspector.TopBarButtons.cs | Thanh trên: Add Component + Lock + zoom |
| GaMoInspector.HeaderGameObject.cs | Header GameObject (tên/tag/layer/static) |
| GaMoInspector.ComponentBody.cs | Thân các component đang mở |
| GaMoInspector.MaterialPanel.cs | Khung Material dưới Renderer |
| GaMoInspector.UitkEditors.cs | Nhận diện Editor chỉ dùng UI Toolkit |
| GaMoInspector.DragReorder.cs | Hover, culling, kéo-thả đổi thứ tự component |
| GaMoInspector.PrefabOverrides.cs | Theo dõi override của Prefab instance |
| GaMoInspector.HeaderButtons.cs | Nút "?", Presets, Normal/Debug |
| GaMoInspector.PrefabBar.cs | Thanh Prefab, menu Overrides |
| GaMoInspector.ContextMenus.cs | Menu chuột phải component, menu Missing Script |
| GaMoInspector.IconSelectorAndEnabled.cs | Chọn icon GameObject, bật/tắt component |
| GaMoInspector.AddComponentPopup.cs | Popup "Add Component" tự vẽ |
| GaMoNativePreview.cs | Tách Preview thành PreviewWindow gốc (class độc lập) |
| GaMoHelpers.cs | Clipboard component, phiên Play, recent components, pending script (4 class độc lập) |

Namespace, tên class `CustomsEditor`, menu `Tools > GaMo > Inspector` — giữ nguyên như bản gốc.
