#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;

public class FBXAnimationFixerWindow : OdinEditorWindow
{
    [MenuItem("Tools/FBX Animation Fixer")]
    private static void OpenWindow()
    {
        GetWindow<FBXAnimationFixerWindow>("FBX Anim Fixer").Show();
    }

    [Title("Target Selection")]
    [Tooltip("Kéo thả các file FBX cần xử lý vào đây")]
    [AssetsOnly]
    public Object[] fbxFiles;

    [Tooltip("Hoặc chọn một thư mục trong Project để xử lý toàn bộ FBX bên trong")]
    [FolderPath]
    public string targetFolder;

    [Button("Apply Animation Settings", ButtonSizes.Large)]
    [GUIColor(0.2f, 0.8f, 0.2f)]
    private void ApplySettings()
    {
        int processedCount = 0;

        // 1. Xử lý danh sách các file FBX được kéo thả vào
        if (fbxFiles != null && fbxFiles.Length > 0)
        {
            foreach (var obj in fbxFiles)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (IsFBXFile(path))
                {
                    FixFBXAnimationSettings(path);
                    processedCount++;
                }
            }
        }

        // 2. Xử lý các file FBX nằm trong thư mục được chọn
        if (!string.IsNullOrEmpty(targetFolder) && Directory.Exists(targetFolder))
        {
            string[] files = Directory.GetFiles(targetFolder, "*.fbx", SearchOption.AllDirectories);
            foreach (string filePath in files)
            {
                FixFBXAnimationSettings(filePath);
                processedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Hoàn tất", $"Đã cập nhật thiết lập Animation cho {processedCount} file FBX!", "OK");
    }

    private bool IsFBXFile(string path)
    {
        return !string.IsNullOrEmpty(path) && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
    }

    private void FixFBXAnimationSettings(string assetPath)
    {
        ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (modelImporter == null) return;

        // Lấy danh sách clip mặc định hoặc hiện có trong FBX
        ModelImporterClipAnimation[] clipAnimations = modelImporter.defaultClipAnimations;

        if (clipAnimations == null || clipAnimations.Length == 0)
        {
            clipAnimations = modelImporter.clipAnimations;
        }

        if (clipAnimations == null || clipAnimations.Length == 0) return;

        // Duyệt qua tất cả các clip trong FBX và áp dụng thiết lập theo ảnh
        for (int i = 0; i < clipAnimations.Length; i++)
        {
            // Loop Time & Pose
            clipAnimations[i].loopTime = true;
            clipAnimations[i].loopPose = false;
            clipAnimations[i].cycleOffset = 0f;

            // Root Transform Rotation
            clipAnimations[i].lockRootRotation = true; // Bake Into Pose
            clipAnimations[i].keepOriginalOrientation = true; // Based Upon: Original
            clipAnimations[i].rotationOffset = 0f;

            // Root Transform Position (Y)
            clipAnimations[i].lockRootHeightY = true; // Bake Into Pose
            clipAnimations[i].keepOriginalPositionY = true; // Based Upon: Original
            clipAnimations[i].heightOffset = 0f;

            // Root Transform Position (XZ)
            clipAnimations[i].lockRootPositionXZ = true; // Bake Into Pose
            clipAnimations[i].keepOriginalPositionXZ = true; // Based Upon: Original
        }

        // Cập nhật lại cấu hình cho ModelImporter và reimport asset
        modelImporter.clipAnimations = clipAnimations;
        modelImporter.SaveAndReimport();
    }
}
#endif