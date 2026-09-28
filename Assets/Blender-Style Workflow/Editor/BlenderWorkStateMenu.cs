#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Tiện ích DÙNG CHUNG cho mọi menu "GameObject/GaMo ToolKit/Blender Work Stale/..." của các
    /// tool khác nhau (Dynamic Bone, BoneCloth, và các tool sau này). Gom về 1 chỗ duy nhất để mọi
    /// tool nhất quán với nhau, tránh mỗi file MenuItem tự viết lại logic tạo GameObject:
    /// - RootPath   : tiền tố đường dẫn menu chung — đổi 1 chỗ là đổi tên/cấu trúc cho MỌI tool.
    /// - MENU_PRIORITY : priority chung, giữ đúng vị trí quen thuộc trong menu GameObject (ngay
    ///   dưới nhóm "Create Empty" mặc định của Unity).
    /// - CreateGameObjectWithComponent&lt;T&gt;() : hàm tạo GameObject mới, gắn sẵn 1 component,
    ///   parent/Undo/Selection y hệt hành vi chuẩn của Unity khi tạo "GameObject > 3D Object > ...".
    ///
    /// CÁCH DÙNG (xem DynamicBoneMenuItems.cs làm ví dụ): mỗi tool tự viết 1 static class riêng
    /// chứa các [MenuItem] của mình, mỗi method chỉ cần gọi
    /// BlenderWorkStateMenu.CreateGameObjectWithComponent&lt;T&gt;(menuCommand, "Tên hiển thị").
    /// </summary>
    public static class BlenderWorkStateMenu
    {
        /// <summary>
        /// Gốc chung cho mọi menu item của bộ tool này. Giữ nguyên chuỗi "Blender Work Stale" để
        /// khớp với các menu đã có — đổi ở đây sẽ đổi đường dẫn menu của TẤT CẢ tool đang dùng
        /// RootPath, nên cần đổi tên method tương ứng ở từng file MenuItem theo nếu đổi.
        /// </summary>
        public const string RootPath = "GameObject/GaMo ToolKit/Blender Work Stale/";

        /// <summary>
        /// Priority dùng chung cho mọi [MenuItem] dưới RootPath. Unity dùng khoảng 0-9 cho nhóm
        /// "Create Empty"/"Create Empty Child" mặc định — 10 đặt bộ tool này ngay sau, và cùng 1
        /// priority cho mọi entry để Unity không tự chèn separator lạc giữa các mục của cùng 1 tool.
        /// </summary>
        public const int MENU_PRIORITY = -10000;

        /// <summary>
        /// Tạo 1 GameObject mới có sẵn component kiểu T, làm con của object đang chọn trong
        /// Hierarchy (menuCommand.context — đúng object bị right-click, KHÔNG phải Selection.active,
        /// để nhất quán khi có nhiều object được chọn cùng lúc), RESET LOCAL TRANSFORM về mặc định
        /// (localPosition = zero, localRotation = identity, localScale = one — không bám theo Scene
        /// view như "Align With View" mặc định của Unity), đăng ký Undo, rồi chọn lại object mới tạo.
        /// </summary>
        /// <param name="menuCommand">Tham số nhận từ method [MenuItem] — dùng để xác định parent.</param>
        /// <param name="name">Tên GameObject mới tạo (cũng dùng làm nhãn cho Undo).</param>
        public static T CreateGameObjectWithComponent<T>(MenuCommand menuCommand, string name) where T : Component
        {
            var go = new GameObject(name);
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

            // Luôn reset về local transform mặc định (0/identity/1) — bất kể có parent hay không,
            // KHÔNG bám theo vị trí camera/Scene view như hành vi "Align With View" mặc định của
            // Unity. Cần thiết vì component này chủ yếu gắn lên bone (Transform con trong rig),
            // nên object mới phải trùng pivot với parent, không lệch theo góc nhìn Scene.
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            T component = go.AddComponent<T>();

            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Selection.activeObject = go;

            return component;
        }
    }
}
#endif