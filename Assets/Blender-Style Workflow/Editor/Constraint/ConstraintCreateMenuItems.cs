#if UNITY_EDITOR
using UnityEditor;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Đăng ký các lệnh "GameObject > GaMo ToolKit > Blender Work Stale > Dynamic Bone > ..." để
    /// tạo nhanh 1 GameObject có sẵn DynamicBone / DynamicBoneCollider, thay vì phải tự Create Empty
    /// rồi kéo component bằng tay. Logic tạo GameObject dùng chung với các tool khác qua
    /// BlenderWorkStateMenu — xem file đó nếu cần thêm tool mới vào cùng nhóm menu.
    /// </summary>
    public static class ConstraintCreateMenuItems
    {
        [MenuItem("GameObject/GaMo ToolKit/Blender Work Stale/Dynamic Bone/Dynamic Bone", false, BlenderWorkStateMenu.MENU_PRIORITY)]
        private static void CreateDynamicBone(MenuCommand menuCommand)
        {
            BlenderWorkStateMenu.CreateGameObjectWithComponent<DynamicBone>(menuCommand, "Dynamic Bone");
        }

        [MenuItem("GameObject/GaMo ToolKit/Blender Work Stale/Dynamic Bone/Dynamic Collider", false, BlenderWorkStateMenu.MENU_PRIORITY)]
        private static void CreateDynamicBoneCollider(MenuCommand menuCommand)
        {
            BlenderWorkStateMenu.CreateGameObjectWithComponent<DynamicBoneCollider>(menuCommand, "Dynamic Bone Collider");
        }
        
        [MenuItem("GameObject/GaMo ToolKit/Blender Work Stale/Dynamic Bone/Dynamic Bone Manager", false, BlenderWorkStateMenu.MENU_PRIORITY)]
        private static void CreateDynamicBoneManager(MenuCommand menuCommand)
        {
            BlenderWorkStateMenu.CreateGameObjectWithComponent<DynamicBoneManager>(menuCommand, "Dynamic Bone Manager");
        }
        
        [MenuItem("GameObject/GaMo ToolKit/Blender Work Stale/IK Constraint", false, BlenderWorkStateMenu.MENU_PRIORITY)]
        private static void CreateIKConstraint(MenuCommand menuCommand)
        {
            BlenderWorkStateMenu.CreateGameObjectWithComponent<IKConstraint>(menuCommand, "IK Constraint");
        }
        
        [MenuItem("GameObject/GaMo ToolKit/Blender Work Stale/Copy Transform Constraint", false, BlenderWorkStateMenu.MENU_PRIORITY)]
        private static void CreateCopyTransformConstraint(MenuCommand menuCommand)
        {
            BlenderWorkStateMenu.CreateGameObjectWithComponent<TransformConstraint>(menuCommand, "Copy Transform Constraint");
        }

        [MenuItem("GameObject/GaMo ToolKit/Blender Work Stale/Bone Render", false, BlenderWorkStateMenu.MENU_PRIORITY)]
        private static void CreateBoneRender(MenuCommand menuCommand)
        {
            BlenderWorkStateMenu.CreateGameObjectWithComponent<SimpleBoneRenderer>(menuCommand, "Bone Render");
        }
    }
}
#endif
