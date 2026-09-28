#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Inspector riêng cho DynamicBoneCollider. Chỉ hiện đúng field cần thiết theo Type, giống các
    /// component "Dynamic Bone Collider" quen thuộc:
    /// - Capsule : Direction, Reverse Direction, Aligned On Center, Length, Radius (hoặc Start/End
    ///             Radius khi bấm nút link để tách riêng 2 đầu), Center.
    /// - Sphere  : Radius, Center.
    /// - Plane   : chỉ Center.
    /// Phần Draw Gizmo / Gizmo Color gộp vào 1 foldout nhỏ ở cuối để không làm rối layout chính.
    /// </summary>
    [CustomEditor(typeof(DynamicBoneCollider))]
    [CanEditMultipleObjects]
    [InitializeOnLoad]
    public class DynamicBoneColliderEditor : Editor
    {
        private SerializedProperty typeProp;
        private SerializedProperty centerOffsetProp;

        private SerializedProperty sphereRadiusProp;

        private SerializedProperty directionProp;
        private SerializedProperty capsuleRadiusModeProp;
        private SerializedProperty capsuleRadiusProp;
        private SerializedProperty capsuleStartRadiusProp;
        private SerializedProperty capsuleEndRadiusProp;
        private SerializedProperty capsuleLengthProp;
        private SerializedProperty reverseDirectionProp;
        private SerializedProperty alignedOnCenterProp;

        private SerializedProperty boxSizeModeProp;
        private SerializedProperty boxSizeProp;
        private SerializedProperty boxTopSizeProp;
        private SerializedProperty boxBottomSizeProp;
        private SerializedProperty planeInfiniteProp;
        private SerializedProperty planeWidthProp;
        private SerializedProperty planeHeightProp;

        private SerializedProperty drawGizmoProp;
        private SerializedProperty gizmoColorProp;

        private static readonly GUIContent GcType = new GUIContent("Type");
        private static readonly GUIContent GcCenter = new GUIContent("Center");
        private static readonly GUIContent GcDirection = new GUIContent("Direction");
        private static readonly GUIContent GcReverseDirection = new GUIContent("Reverse Direction");
        private static readonly GUIContent GcAlignedOnCenter = new GUIContent("Aligned On Center");
        private static readonly GUIContent GcLength = new GUIContent("Length");
        private static readonly GUIContent GcRadius = new GUIContent("Radius");
        private static readonly GUIContent GcStartRadius = new GUIContent("Start Radius");
        private static readonly GUIContent GcEndRadius = new GUIContent("End Radius");

        // Slider "co giãn": max mặc định nhỏ (giống ảnh mẫu, kéo mượt ở khoảng nhỏ) nhưng tự phình
        // ra nếu giá trị hiện tại/đang gõ tay lớn hơn — tránh việc Slider() CLAMP mất giá trị người
        // dùng gõ tay lớn hơn max mặc định.
        private const float DefaultRadiusSliderMax = 0.5f;
        private const float DefaultLengthSliderMax = 1f;

        private bool _showGizmoFoldout;
        
        static DynamicBoneColliderEditor()
        {
            EditorApplication.delayCall += ApplyIconToAll;
            EditorApplication.hierarchyChanged -= ApplyIconToAll;
            EditorApplication.hierarchyChanged += ApplyIconToAll;
        }

        private static void ApplyIconToAll()
            => ComponentIconApplier.ApplyToAll<DynamicBoneCollider>("DynamicBoneCollider");

        private void OnEnable()
        {
            typeProp = serializedObject.FindProperty("type");
            centerOffsetProp = serializedObject.FindProperty("centerOffset");

            sphereRadiusProp = serializedObject.FindProperty("sphereRadius");

            directionProp = serializedObject.FindProperty("direction");
            capsuleRadiusModeProp = serializedObject.FindProperty("capsuleRadiusMode");
            capsuleRadiusProp = serializedObject.FindProperty("capsuleRadius");
            capsuleStartRadiusProp = serializedObject.FindProperty("capsuleStartRadius");
            capsuleEndRadiusProp = serializedObject.FindProperty("capsuleEndRadius");
            capsuleLengthProp = serializedObject.FindProperty("capsuleLength");
            reverseDirectionProp = serializedObject.FindProperty("reverseDirection");
            alignedOnCenterProp = serializedObject.FindProperty("alignedOnCenter");

            boxSizeModeProp = serializedObject.FindProperty("boxSizeMode");
            boxSizeProp = serializedObject.FindProperty("boxSize");
            boxTopSizeProp = serializedObject.FindProperty("boxTopSize");
            boxBottomSizeProp = serializedObject.FindProperty("boxBottomSize");
            planeInfiniteProp = serializedObject.FindProperty("planeInfinite");
            planeWidthProp = serializedObject.FindProperty("planeWidth");
            planeHeightProp = serializedObject.FindProperty("planeHeight");

            drawGizmoProp = serializedObject.FindProperty("drawGizmo");
            gizmoColorProp = serializedObject.FindProperty("gizmoColor");
            
            //SetComponentIcon();
        }
        
        //private void SetComponentIcon() => ComponentIconApplier.ApplyToObject(target, "DynamicBoneCollider");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            //EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(typeProp, GcType);
            //EditorGUILayout.Space(4);

            var shape = (DynamicBone.ColliderShapeType)typeProp.enumValueIndex;
            switch (shape)
            {
                case DynamicBone.ColliderShapeType.Capsule:
                    DrawCapsuleFields();
                    break;
                case DynamicBone.ColliderShapeType.Sphere:
                    DrawSphereFields();
                    break;
                case DynamicBone.ColliderShapeType.Box:
                    DrawBoxFields();
                    break;
                default:
                    DrawPlaneFields();
                    break;
            }

            EditorGUILayout.Space(8);
            DrawGizmoFoldout();

            serializedObject.ApplyModifiedProperties();
        }

        // ── CAPSULE ──────────────────────────────────────────────────────────
        private void DrawCapsuleFields()
        {
            EditorGUILayout.PropertyField(directionProp, GcDirection);
            EditorGUILayout.PropertyField(reverseDirectionProp, GcReverseDirection);
            EditorGUILayout.PropertyField(alignedOnCenterProp, GcAlignedOnCenter);

            EditorGUILayout.Space(4);
            DrawExpandingSlider(GcLength, capsuleLengthProp, DefaultLengthSliderMax);

            //EditorGUILayout.Space(2);
            DrawCapsuleRadiusRows();

            //EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(centerOffsetProp, GcCenter);
        }

        // Vẽ 1 hoặc 2 hàng Radius tuỳ capsuleRadiusMode, kèm nút nhỏ để chuyển đổi Uniform <-> PerEnd
        // — "S" (Same) khi đang linked 1 giá trị chung, "X" khi đang tách riêng Start/End.
        private void DrawCapsuleRadiusRows()
        {
            bool uniform = capsuleRadiusModeProp.enumValueIndex == (int)DynamicBoneCapsuleRadiusMode.Uniform;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (uniform)
                {
                    DrawExpandingSliderInline(GcRadius, capsuleRadiusProp, DefaultRadiusSliderMax);
                }
                else
                {
                    DrawExpandingSliderInline(GcStartRadius, capsuleStartRadiusProp, DefaultRadiusSliderMax);
                }

                if (GUILayout.Button(uniform ? "S" : "X", GUILayout.Width(20)))
                {
                    capsuleRadiusModeProp.enumValueIndex = uniform
                        ? (int)DynamicBoneCapsuleRadiusMode.PerEnd
                        : (int)DynamicBoneCapsuleRadiusMode.Uniform;
                }
            }

            if (!uniform)
            {
                DrawExpandingSlider(GcEndRadius, capsuleEndRadiusProp, DefaultRadiusSliderMax);
            }
        }

        // ── SPHERE ───────────────────────────────────────────────────────────
        private void DrawSphereFields()
        {
            DrawExpandingSlider(GcRadius, sphereRadiusProp, DefaultRadiusSliderMax);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(centerOffsetProp, GcCenter);
        }

        // ── BOX ──────────────────────────────────────────────────────────────
        private void DrawBoxFields()
        {
            EditorGUILayout.PropertyField(alignedOnCenterProp, GcAlignedOnCenter);

            bool uniform = boxSizeModeProp.enumValueIndex == (int)DynamicBoneBoxSizeMode.Uniform;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (uniform)
                    EditorGUILayout.PropertyField(boxSizeProp, new GUIContent("Size"));
                else
                    DrawExpandingSliderInline(new GUIContent("Height"), boxSizeProp.FindPropertyRelative("y"), DefaultLengthSliderMax);

                // "S" (Same) khi đang dùng 1 Size chung, "X" khi đã tách mặt trên/dưới — giống Capsule.
                if (GUILayout.Button(uniform ? "S" : "X", GUILayout.Width(20)))
                {
                    if (uniform)
                    {
                        // Sang Per Face: khởi tạo 2 mặt từ Size hiện tại để khối không bị nhảy.
                        var v = boxSizeProp.vector3Value;
                        boxTopSizeProp.vector2Value = new Vector2(v.x, v.z);
                        boxBottomSizeProp.vector2Value = new Vector2(v.x, v.z);
                    }
                    else
                    {
                        // Về Uniform: lấy Width/Length từ mặt trên.
                        var v = boxSizeProp.vector3Value;
                        var top = boxTopSizeProp.vector2Value;
                        boxSizeProp.vector3Value = new Vector3(top.x, v.y, top.y);
                    }
                    boxSizeModeProp.enumValueIndex = uniform
                        ? (int)DynamicBoneBoxSizeMode.PerFace
                        : (int)DynamicBoneBoxSizeMode.Uniform;
                }
            }

            if (!uniform)
            {
                EditorGUILayout.PropertyField(boxTopSizeProp, new GUIContent("Top (W, L)"));
                EditorGUILayout.PropertyField(boxBottomSizeProp, new GUIContent("Bottom (W, L)"));
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(centerOffsetProp, GcCenter);
        }

        // ── PLANE ────────────────────────────────────────────────────────────
        private void DrawPlaneFields()
        {
            EditorGUILayout.PropertyField(directionProp, GcDirection);
            EditorGUILayout.PropertyField(reverseDirectionProp, GcReverseDirection);
            EditorGUILayout.PropertyField(planeInfiniteProp, new GUIContent("Infinite"));
            if (!planeInfiniteProp.boolValue)
            {
                EditorGUILayout.PropertyField(alignedOnCenterProp, GcAlignedOnCenter);
                DrawExpandingSlider(new GUIContent("Width"), planeWidthProp, DefaultLengthSliderMax);
                DrawExpandingSlider(new GUIContent("Height"), planeHeightProp, DefaultLengthSliderMax);
            }
            EditorGUILayout.PropertyField(centerOffsetProp, GcCenter);
        }

        // ── GIZMO (thu gọn) ──────────────────────────────────────────────────
        private void DrawGizmoFoldout()
        {
            _showGizmoFoldout = EditorGUILayout.Foldout(_showGizmoFoldout, "Gizmo", true);
            if (!_showGizmoFoldout) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(drawGizmoProp, new GUIContent("Draw Gizmo"));
            EditorGUILayout.PropertyField(gizmoColorProp, new GUIContent("Gizmo Color"));
            EditorGUI.indentLevel--;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Slider + field trên 1 hàng riêng, range tự phình theo giá trị hiện tại.</summary>
        private static void DrawExpandingSlider(GUIContent label, SerializedProperty prop, float defaultMax)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawExpandingSliderInline(label, prop, defaultMax);
            }
        }

        /// <summary>Như trên nhưng KHÔNG tự mở HorizontalScope — để caller ghép thêm control khác
        /// (VD nút link "S"/"X") vào cùng 1 hàng.</summary>
        private static void DrawExpandingSliderInline(GUIContent label, SerializedProperty prop, float defaultMax)
        {
            float max = Mathf.Max(defaultMax, prop.floatValue * 1.5f);
            float newVal = EditorGUILayout.Slider(label, prop.floatValue, 0f, max);
            if (!Mathf.Approximately(newVal, prop.floatValue))
                prop.floatValue = Mathf.Max(0f, newVal);
        }
    }
}
#endif