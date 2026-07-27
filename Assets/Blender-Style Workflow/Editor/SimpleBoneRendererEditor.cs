using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SimpleBoneRenderer))]
[InitializeOnLoad]
public class SimpleBoneRendererEditor : Editor
{
    // --- Cache danh sách renderer trong scene ---
    // Trước kia FindObjectsByType() bị gọi lại ở MỌI event (MouseMove, Layout,
    // Repaint...) tức hàng chục lần/giây. Giờ chỉ quét lại khi hierarchy thực sự
    // thay đổi (thêm/xóa object, đổi scene, undo/redo...).
    private static SimpleBoneRenderer[] _cachedRenderers;
    private static bool _renderersDirty = true;

    static SimpleBoneRendererEditor()
    {
        SceneView.duringSceneGui -= OnGlobalSceneGUI;
        SceneView.duringSceneGui += OnGlobalSceneGUI;

        EditorApplication.hierarchyChanged -= MarkRenderersDirty;
        EditorApplication.hierarchyChanged += MarkRenderersDirty;
    }

    private static void MarkRenderersDirty()
    {
        _renderersDirty = true;
    }

    private static SimpleBoneRenderer[] GetRenderers()
    {
        if (_renderersDirty || _cachedRenderers == null)
        {
            _cachedRenderers = Object.FindObjectsByType<SimpleBoneRenderer>(FindObjectsSortMode.None);
            _renderersDirty = false;
        }
        return _cachedRenderers;
    }

    // ────────────────────────────────────────────────────────────────────────
    // UI Inspector theo phong cách tab (giống TransformConstraintEditor)
    // ────────────────────────────────────────────────────────────────────────
    private int currentTab = 0;
    private readonly string[] tabTitles = { "Setup", "Bone Settings", "Joint Settings" };

    // Properties - Setup
    private SerializedProperty rootBoneProp;
    private SerializedProperty bonesProp;

    // Properties - Bone Settings
    private SerializedProperty boneColorProp;
    private SerializedProperty boneSizeProp;

    // Properties - Joint Settings
    private SerializedProperty drawJointsProp;
    private SerializedProperty jointColorProp;
    private SerializedProperty jointSizeProp;

    private void OnEnable()
    {
        // Guard: nếu target null (object vừa bị xóa/Undo, hoặc Editor được tái tạo
        // ngay lúc recompile/prefab mode...), việc chạm vào serializedObject sẽ
        // ném SerializedObjectNotCreatableException. Thoát sớm để tránh crash Inspector.
        if (target == null)
            return;

        rootBoneProp = serializedObject.FindProperty("rootBone");
        bonesProp    = serializedObject.FindProperty("bones");

        boneColorProp = serializedObject.FindProperty("boneColor");
        boneSizeProp  = serializedObject.FindProperty("boneSize");

        drawJointsProp = serializedObject.FindProperty("drawJoints");
        jointColorProp = serializedObject.FindProperty("jointColor");
        jointSizeProp  = serializedObject.FindProperty("jointSize");

        SetComponentIcon();
    }

    private void SetComponentIcon()
    {
        // Chỉ tìm đúng file có tên là "BoneRenderer" (icon của bạn) để tối ưu tốc độ quét.
        // Nếu bạn có icon riêng, đặt tên file trùng với chuỗi tìm kiếm bên dưới,
        // hoặc đổi lại chuỗi "BoneRenderer" cho đúng tên file icon bạn đang dùng.
        string[] guids = AssetDatabase.FindAssets("BoneRenderer t:Texture2D");

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (path.Contains("Editor/Icon") || path.Contains("Blender-Style Workflow"))
            {
                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (icon != null)
                {
                    EditorGUIUtility.SetIconForObject(target, icon);
                    break;
                }
            }
        }
    }

    public override void OnInspectorGUI()
    {
        // Guard tương tự OnEnable: target có thể trở thành null giữa chừng
        // (ví dụ object bị xóa trong khi Inspector đang mở).
        if (target == null)
            return;

        // Nếu OnEnable từng bail sớm vì target null, properties sẽ chưa được gán.
        // Thử khởi tạo lại một lần cho chắc trước khi dùng.
        if (rootBoneProp == null)
        {
            OnEnable();
            if (rootBoneProp == null) return;
        }

        serializedObject.Update();

        SimpleBoneRenderer boneRenderer = (SimpleBoneRenderer)target;

        EditorGUILayout.Space(3);
        currentTab = GUILayout.Toolbar(currentTab, tabTitles, EditorStyles.toolbarButton, GUILayout.Height(20));
        EditorGUILayout.Space(10);

        switch (currentTab)
        {
            case 0:
                DrawSetupTab(boneRenderer);
                break;
            case 1:
                DrawBoneSettingsTab();
                break;
            case 2:
                DrawJointSettingsTab();
                break;
        }

        EditorGUILayout.Space(5);
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSetupTab(SimpleBoneRenderer boneRenderer)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.PropertyField(rootBoneProp, Content.RootBone);

        EditorGUILayout.Space(6);

        if (GUILayout.Button(Content.LoadBones, "Button", GUILayout.Height(20)))
        {
            boneRenderer.PopulateChildren();
            MarkRenderersDirty();
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.PropertyField(bonesProp, Content.Bones, true);
        EditorGUILayout.EndVertical();
    }

    private void DrawBoneSettingsTab()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.PropertyField(boneColorProp, Content.BoneColor);
        EditorGUILayout.PropertyField(boneSizeProp,  Content.BoneSize);
        EditorGUILayout.EndVertical();
    }

    private void DrawJointSettingsTab()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.PropertyField(drawJointsProp, Content.DrawJoints);

        EditorGUI.BeginDisabledGroup(!drawJointsProp.boolValue);
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(jointColorProp, Content.JointColor);
            EditorGUILayout.PropertyField(jointSizeProp,  Content.JointSize);
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();
    }

    private void OnSceneGUI()
    {
        // Để trống vì sự kiện đã được quản lý tập trung ở OnGlobalSceneGUI static
    }

    // --- Cache GUIContent để tránh new GUIContent(...) mỗi lần repaint ---
    // Inspector repaint hàng chục lần/giây; mỗi lần tạo mới GUIContent là 1 lần GC alloc.
    private static class Content
    {
        public static readonly GUIContent RootBone    = new GUIContent("Root Bone");
        public static readonly GUIContent LoadBones   = new GUIContent("Load All Child Bones");
        public static readonly GUIContent Bones       = new GUIContent("Bones");
        public static readonly GUIContent BoneColor   = new GUIContent("Bone Color");
        public static readonly GUIContent BoneSize    = new GUIContent("Bone Size");
        public static readonly GUIContent DrawJoints  = new GUIContent("Draw Joints");
        public static readonly GUIContent JointColor  = new GUIContent("Joint Color");
        public static readonly GUIContent JointSize   = new GUIContent("Joint Size");
    }

    // Dùng lại DUY NHẤT 1 List cho mọi xương, mọi frame, thay vì "new List<Transform>()"
    // liên tục — đây là nguồn rác (GC) chính gây giật khi có nhiều xương.
    private static readonly List<Transform> _reusableChildren = new List<Transform>(8);

    private static void OnGlobalSceneGUI(SceneView sceneView)
    {
        if (Application.isPlaying) return;

        SimpleBoneRenderer[] renderers = GetRenderers();
        if (renderers == null || renderers.Length == 0) return;

        Event currentEvent = Event.current;
        Matrix4x4 originalMatrix = Handles.matrix;

        // Cache một lần bên ngoài vòng lặp kép; truy cập Selection.activeTransform
        // mỗi xương mỗi frame tốn không đáng.
        Transform selectedTransform = Selection.activeTransform;

        // Handles.Button bên trong DrawBoneGraphics gọi HandleUtility nặng hơn hẳn
        // so với chỉ vẽ — chỉ cần xử lý khi event thực sự là chuột.
        bool isMouseEvent = currentEvent.type == EventType.MouseDown
                         || currentEvent.type == EventType.MouseUp
                         || currentEvent.type == EventType.Layout; // Layout cần thiết để Unity đăng ký hit-test

        foreach (var boneRenderer in renderers)
        {
            if (boneRenderer == null || !boneRenderer.enabled || !boneRenderer.gameObject.activeInHierarchy || boneRenderer.bones == null)
                continue;

            HashSet<Transform> boneSet = boneRenderer.BoneSet;
            List<Transform> bones = boneRenderer.bones;

            for (int i = 0; i < bones.Count; i++)
            {
                Transform bone = bones[i];
                if (bone == null) continue;

                _reusableChildren.Clear();
                foreach (Transform child in bone)
                {
                    if (boneSet.Contains(child)) _reusableChildren.Add(child);
                }

                if (_reusableChildren.Count > 0)
                {
                    foreach (Transform child in _reusableChildren)
                    {
                        Vector3 start = bone.position;
                        Vector3 end   = child.position;
                        if ((end - start).sqrMagnitude < 0.000001f) continue;

                        ProcessBone(boneRenderer, bone, start, end, currentEvent, selectedTransform, isMouseEvent);
                    }
                }
                else
                {
                    Vector3 start     = bone.position;
                    Vector3 end       = start + bone.up * (boneRenderer.boneSize * 2.5f);
                    ProcessBone(boneRenderer, bone, start, end, currentEvent, selectedTransform, isMouseEvent);
                }
            }
        }

        Handles.matrix = originalMatrix;
    }

    private static void ProcessBone(SimpleBoneRenderer renderer, Transform bone,
        Vector3 start, Vector3 end, Event currentEvent, Transform selectedTransform, bool isMouseEvent)
    {
        Handles.color = (selectedTransform == bone) ? Color.green : renderer.boneColor;
        DrawBoneGraphics(start, end, renderer, bone, currentEvent, isMouseEvent);
    }

    private static void DrawBoneGraphics(Vector3 start, Vector3 end, SimpleBoneRenderer renderer,
        Transform bone, Event currentEvent, bool isMouseEvent)
    {
        Vector3 direction = end - start;
        float distance = direction.magnitude;
        if (distance < 0.0001f) return;

        float size = renderer.boneSize;
        Color originalColor = Handles.color;

        // --- 1. VẼ THÂN XƯƠNG ---
        Handles.DrawLine(start, end);

        Quaternion lookRotation = Quaternion.LookRotation(direction);
        float scaledSize = size * distance;
        Handles.DrawWireDisc(start, direction, scaledSize);

        Vector3 right = lookRotation * Vector3.right * scaledSize;
        Vector3 up    = lookRotation * Vector3.up    * scaledSize;

        Handles.DrawLine(start + right, end);
        Handles.DrawLine(start - right, end);
        Handles.DrawLine(start + up,    end);
        Handles.DrawLine(start - up,    end);

        // --- 2. VẼ KHỚP ---
        if (renderer.drawJoints)
        {
            Handles.color = renderer.jointColor;
            float sphereSize = renderer.jointSize;

            // Handles.Button gọi HandleUtility hit-test nặng mỗi frame.
            // Chỉ cần xử lý khi event là chuột hoặc Layout (đăng ký hit-test).
            // Các event còn lại (Repaint, MouseMove...) chỉ cần vẽ sphere đơn giản.
            if (isMouseEvent)
            {
                bool isClicked = Handles.Button(
                    end,
                    Quaternion.identity,
                    sphereSize,
                    sphereSize,
                    Handles.SphereHandleCap
                );

                if (isClicked)
                {
                    Selection.activeObject = bone;
                    currentEvent?.Use();
                }
            }
            else
            {
                // Repaint path: chỉ vẽ, không cần đăng ký hit-test.
                Handles.SphereHandleCap(0, end, Quaternion.identity, sphereSize, EventType.Repaint);
            }
        }

        Handles.color = originalColor;
    }
}