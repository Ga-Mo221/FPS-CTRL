#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Vẽ icon riêng của từng component GaMo Blender-Style Workflow ngay cạnh (bên PHẢI) tên
    /// GameObject trong cửa sổ Hierarchy — y hệt cách Magica Cloth 2 làm
    /// (MagicaCloth2.DrawIconInHierarchy).
    ///
    /// LƯU Ý QUAN TRỌNG: Unity KHÔNG tự động vẽ badge kiểu này cho MonoBehaviour có custom icon.
    /// - MonoImporter.SetIcon() (xem ScriptIconSetter.cs) chỉ đổi icon THAY THẾ cho GameObject ở
    ///   BÊN TRÁI tên (khi component đó là component "đại diện" duy nhất/nổi bật của GameObject),
    ///   KHÔNG thêm badge ở bên phải.
    /// - EditorGUIUtility.SetIconForObject() (xem ComponentIconApplier.ApplyToObject) chỉ đổi icon
    ///   hiển thị trong header Inspector + gizmo Scene view của CHÍNH instance đó, cũng KHÔNG liên
    ///   quan gì đến Hierarchy.
    /// Nên để có nhiều badge xếp cạnh nhau bên phải tên (1 badge / component) như Magica Cloth 2,
    /// bắt buộc phải tự vẽ tay qua EditorApplication.hierarchyWindowItemOnGUI như dưới đây.
    /// </summary>
    [InitializeOnLoad]
    public static class BlenderWorkStaleHierarchyIconDrawer
    {
        private const int IconSize = 16;
        private const int IconPadding = 2;

        // Mỗi entry: (loại component cần hiện badge, từ khóa icon tương ứng).
        // Dùng CHUNG từ khóa với ComponentIconApplier/ScriptIconSetter để icon luôn đồng bộ giữa
        // Inspector, Script Icon và badge Hierarchy — chỉ cần thêm 1 dòng ở đây khi có component
        // mới muốn hiện badge, không cần sửa gì thêm.
        private static readonly (Type componentType, string iconSearchTerm)[] _entries =
        {
            (typeof(DynamicBone), "spring"),
            (typeof(DynamicBoneCollider), "DynamicBoneCollider"),
            (typeof(TransformConstraint), "CopyTransform"),
            (typeof(IKConstraint), "IKConstraint"),
            (typeof(SimpleBoneRenderer), "BoneRenderer"),
            (typeof(DynamicBoneManager), "dynamicmanager"),
            // KHÔNG thêm MotionTimeEventBehaviour ở đây: nó kế thừa StateMachineBehaviour
            // (gắn vào state của Animator Controller), KHÔNG phải Component/MonoBehaviour
            // gắn trên GameObject. GameObject.GetComponent(Type) chỉ chấp nhận Component
            // hoặc interface — truyền StateMachineBehaviour vào sẽ ném ArgumentException
            // mỗi frame vẽ Hierarchy (đây là nguyên nhân lỗi "GetComponent requires that
            // the requested component 'MotionTimeEventBehaviour' derives from
            // MonoBehaviour or Component or is an interface").
        };

        static BlenderWorkStaleHierarchyIconDrawer()
        {
            // -= trước += để tránh đăng ký trùng nếu static constructor lỡ chạy nhiều lần
            // (domain reload, script recompile...).
            EditorApplication.hierarchyWindowItemOnGUI -= DrawIcons;
            EditorApplication.hierarchyWindowItemOnGUI += DrawIcons;
        }

        private static void DrawIcons(int instanceId, Rect selectionRect)
        {
#if UNITY_6000_3_OR_NEWER
            GameObject obj = EditorUtility.EntityIdToObject(instanceId) as GameObject;
#else
            GameObject obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
#endif
            if (obj == null) return;

            Rect rect = selectionRect;
            rect.width = IconSize;
            rect.y += -1;
            // selectionRect.x bắt đầu từ trước icon mặc định của GameObject (icon loại đối
            // tượng, VD hình lập phương) — cộng thêm bề rộng icon đó (IconSize + padding) rồi
            // cộng bề rộng chữ tên GameObject để badge đầu tiên nằm ngay sau ký tự cuối của tên.
            rect.x += IconSize + IconPadding + EditorStyles.label.CalcSize(new GUIContent(obj.name)).x;

            foreach (var (componentType, iconSearchTerm) in _entries)
            {
                // Chỉ gọi GetComponent với type thực sự là Component (hoặc interface),
                // tránh ArgumentException nếu lỡ có type không phải Component (VD
                // StateMachineBehaviour) bị thêm nhầm vào _entries phía trên.
                if (!typeof(Component).IsAssignableFrom(componentType) && !componentType.IsInterface)
                    continue;

                if (obj.GetComponent(componentType) == null) continue;

                Texture2D icon = ComponentIconApplier.FindIcon(iconSearchTerm);
                if (icon == null) continue;

                GUI.Label(rect, icon);
                rect.x += IconSize;
            }
        }
    }
}
#endif