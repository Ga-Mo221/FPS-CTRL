using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace TrueInputSystem
{
    [CustomEditor(typeof(TISGamepadUIButton))]
    [CanEditMultipleObjects]
    public class TISGamepadUIButtonEditor : UnityEditor.Editor
    {
        private int currentTab = 0;
        private readonly string[] tabLabels = { "Navigation", "Visuals", "Events" };

        private SerializedProperty navigationModeProp;
        private SerializedProperty inputSourceProp;
        private SerializedProperty layerProp;
        private SerializedProperty holdDelayProp;
        private SerializedProperty navUpProp;
        private SerializedProperty navDownProp;
        private SerializedProperty navLeftProp;
        private SerializedProperty navRightProp;
        private SerializedProperty alwaysActiveProp;
        private SerializedProperty isFirstSelectedProp;
        private SerializedProperty isNavigationEnabledProp;
        private SerializedProperty interactableProp;

        private SerializedProperty onClickProp;
        private SerializedProperty onSelectProp;
        private SerializedProperty onDeselectProp;
        private SerializedProperty onBecameActiveInLayerProp;

        private SerializedProperty transitionProp;
        private SerializedProperty targetGraphicProp;
        private SerializedProperty colorBlockProp;
        private SerializedProperty spriteStateProp;
        private SerializedProperty animationTriggersProp;

        private void OnEnable()
        {
            navigationModeProp = serializedObject.FindProperty("navigationMode");
            inputSourceProp = serializedObject.FindProperty("inputSource");
            layerProp = serializedObject.FindProperty("layer");
            holdDelayProp = serializedObject.FindProperty("holdDelay");
            navUpProp = serializedObject.FindProperty("navUp");
            navDownProp = serializedObject.FindProperty("navDown");
            navLeftProp = serializedObject.FindProperty("navLeft");
            navRightProp = serializedObject.FindProperty("navRight");
            alwaysActiveProp = serializedObject.FindProperty("alwaysActive");
            isFirstSelectedProp = serializedObject.FindProperty("isFirstSelected");
            isNavigationEnabledProp = serializedObject.FindProperty("isNavigationEnabled");
            interactableProp = serializedObject.FindProperty("m_Interactable");

            onClickProp = serializedObject.FindProperty("m_OnClick");
            onSelectProp = serializedObject.FindProperty("onSelect");
            onDeselectProp = serializedObject.FindProperty("onDeselect");
            onBecameActiveInLayerProp = serializedObject.FindProperty("onBecameActiveInLayer");

            transitionProp = serializedObject.FindProperty("m_Transition");
            targetGraphicProp = serializedObject.FindProperty("m_TargetGraphic");
            colorBlockProp = serializedObject.FindProperty("m_Colors");
            spriteStateProp = serializedObject.FindProperty("m_SpriteState");
            animationTriggersProp = serializedObject.FindProperty("m_AnimationTriggers");

            SetComponentIcon();
        }

        private void SetComponentIcon()
        {
            string[] guids = AssetDatabase.FindAssets("GamepadButton t:Texture2D");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("TrueInputSystem") && path.Contains("Editor/Icon"))
                {
                    var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
            serializedObject.Update();

            currentTab = GUILayout.Toolbar(currentTab, tabLabels);
            EditorGUILayout.Space(10);

            switch (currentTab)
            {
                case 0: DrawNavigationTab(); break;
                case 1: DrawVisualsTab(); break;
                case 2: DrawEventsTab(); break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ─────────────────────────────────────────────────────────────────────────

        private void DrawNavigationTab()
        {
            TISGamepadUIButton.NavigationLayer lyr = (TISGamepadUIButton.NavigationLayer)layerProp.enumValueIndex;
            bool isContent = lyr == TISGamepadUIButton.NavigationLayer.Content;

            // ── Button Settings ───────────────────────────────────────────────────
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Button Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // Hàng 1: Interactable + Navigation Enabled
            EditorGUILayout.BeginHorizontal();
            interactableProp.boolValue = EditorGUILayout.ToggleLeft(
                new GUIContent("Interactable",
                    "Khi tắt, button không nhận click/submit và hiển thị trạng thái Disabled.\n" +
                    "Tương đương với Button.interactable gốc của Unity."),
                interactableProp.boolValue);
            isNavigationEnabledProp.boolValue = EditorGUILayout.ToggleLeft(
                new GUIContent("Navigation Enabled",
                    "Khi tắt, button không xử lý gamepad input và không nhận / chuyển focus.\n" +
                    "Dùng để 'đóng băng' nhóm button khi một panel khác đang active."),
                isNavigationEnabledProp.boolValue);
            EditorGUILayout.EndHorizontal();

            // Hàng 2: Always Active (Tab/SubTab) | First Selected (Content)
            EditorGUILayout.BeginHorizontal();
            if (!isContent)
            {
                alwaysActiveProp.boolValue = EditorGUILayout.ToggleLeft(
                    new GUIContent("Always Active",
                        "Giữ visual 'Selected' dù focus đang ở layer khác.\n" +
                        "Dùng cho Tab và SubTab — KHÔNG dùng cho Content."),
                    alwaysActiveProp.boolValue);
            }
            else
            {
                isFirstSelectedProp.boolValue = EditorGUILayout.ToggleLeft(
                    new GUIContent("First Selected",
                        "Khi bật, button này sẽ tự nhận EventSystem focus sau 1 frame khi GameObject được kích hoạt.\n" +
                        "Dùng để chỉ định button mặc định khi mở panel.\n\n" +
                        "Chỉ hoạt động với Content layer."),
                    isFirstSelectedProp.boolValue);
            }

            EditorGUILayout.EndHorizontal();

            // Warning / info hints
            if (!isNavigationEnabledProp.boolValue)
                EditorGUILayout.HelpBox("⚠  Navigation đang bị tắt. Button sẽ không phản hồi input gamepad/keyboard.",
                    MessageType.Warning);

            if (!isContent && alwaysActiveProp.boolValue)
            {
                TISGamepadUIButton.InputSource src = (TISGamepadUIButton.InputSource)inputSourceProp.enumValueIndex;
                if (src == TISGamepadUIButton.InputSource.Default)
                    EditorGUILayout.HelpBox(
                        "⚠  Input Source = Default: global layer input sẽ không hoạt động.\nĐổi sang LT_RT (Tab) hoặc LB_RB (SubTab).",
                        MessageType.Warning);
            }

            if (isContent && alwaysActiveProp.boolValue)
                EditorGUILayout.HelpBox(
                    "⚠  Always Active trên Content layer không có tác dụng.\nContent button dùng real EventSystem focus.",
                    MessageType.Warning);

            EditorGUILayout.Space(4);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // ── Layer selector ────────────────────────────────────────────────────
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Layer", EditorStyles.boldLabel);

            GUIContent[] layerContents =
            {
                new GUIContent("Tab",
                    "Layer 0 — Tab\n" +
                    "Dùng cho các button tab chính (thanh ngang).\n\n" +
                    "Cấu hình gợi ý:\n" +
                    "• Navigation Mode = Horizontal\n" +
                    "• Input Source    = LT_RT\n" +
                    "• Always Active   = true\n\n" +
                    "Keyboard fallback: Q (trái) / E (phải)"),
                new GUIContent("SubTab",
                    "Layer 1 — SubTab\n" +
                    "Dùng cho các button subtab (thanh dọc bên trong tab).\n\n" +
                    "Cấu hình gợi ý:\n" +
                    "• Navigation Mode = Vertical\n" +
                    "• Input Source    = LB_RB\n" +
                    "• Always Active   = true\n\n" +
                    "Keyboard fallback: Z (lên) / C (xuống)"),
                new GUIContent("Content",
                    "Layer 2 — Content\n" +
                    "Dùng cho các button nội dung bên trong grid/list.\n\n" +
                    "Cấu hình gợi ý:\n" +
                    "• Navigation Mode = FourDirectional\n" +
                    "• Input Source    = Default\n" +
                    "• Always Active   = false\n\n" +
                    "Keyboard fallback: WASD / Arrow Keys\n\n" +
                    "Layer này dùng real EventSystem focus —\n" +
                    "Tab và SubTab chỉ giữ virtual highlight."),
            };
            layerProp.enumValueIndex = GUILayout.Toolbar(layerProp.enumValueIndex, layerContents);

            EditorGUILayout.Space(4);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // ── Nav mode + Input source ───────────────────────────────────────────
            EditorGUILayout.PropertyField(navigationModeProp, new GUIContent(
                "Navigation Mode",
                "FourDirectional — di chuyển 4 hướng bằng DPad / Left Stick.\n" +
                "Dùng cho Content button trong grid hoặc list 2 chiều.\n\n" +
                "Horizontal — di chuyển trái/phải.\n" +
                "Dùng cho Tab button (LT/RT) hoặc row ngang.\n\n" +
                "Vertical — di chuyển lên/xuống.\n" +
                "Dùng cho SubTab button (LB/RB) hoặc list dọc."));

            TISGamepadUIButton.NavMode mode2 = (TISGamepadUIButton.NavMode)navigationModeProp.enumValueIndex;

            if (mode2 == TISGamepadUIButton.NavMode.Horizontal || mode2 == TISGamepadUIButton.NavMode.Vertical)
                EditorGUILayout.PropertyField(inputSourceProp, new GUIContent("Input Source"));
            else
                inputSourceProp.enumValueIndex = (int)TISGamepadUIButton.InputSource.Default;

            EditorGUILayout.PropertyField(holdDelayProp, new GUIContent(
                "Hold Delay",
                "Thời gian chờ (giây) giữa các lần di chuyển khi giữ nút.\n" +
                "Giá trị nhỏ = phản hồi nhanh hơn, dễ vượt qua button.\n" +
                "Giá trị lớn = di chuyển chậm hơn, dễ kiểm soát hơn.\n\n" +
                "Khuyến nghị: 0.15 – 0.25 giây."));

            // ── Navigation targets ────────────────────────────────────────────────
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Navigation Targets", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.Space(8);

            switch (mode2)
            {
                case TISGamepadUIButton.NavMode.FourDirectional: DrawFourDirectional(); break;
                case TISGamepadUIButton.NavMode.Horizontal: DrawHorizontal(); break;
                case TISGamepadUIButton.NavMode.Vertical: DrawVertical(); break;
            }

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Clear All Navigation", GUILayout.Height(24)))
            {
                navUpProp.objectReferenceValue = null;
                navDownProp.objectReferenceValue = null;
                navLeftProp.objectReferenceValue = null;
                navRightProp.objectReferenceValue = null;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        // ── Layout helpers ────────────────────────────────────────────────────────

        private void DrawFourDirectional()
        {
            float totalW = EditorGUIUtility.currentViewWidth - 40f;
            float sideW = (totalW - kSelfWidth - kGapH * 2f) / 2f;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(sideW + kGapH);
            DrawLabeledField("▲  Up", navUpProp, kSelfWidth);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(kGapV);

            EditorGUILayout.BeginHorizontal();
            DrawLabeledField("◀  Left", navLeftProp, sideW);
            GUILayout.Space(kGapH);
            DrawSelfBox(kSelfWidth);
            GUILayout.Space(kGapH);
            DrawLabeledField("Right  ▶", navRightProp, sideW);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(kGapV);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(sideW + kGapH);
            DrawLabeledField("▼  Down", navDownProp, kSelfWidth);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHorizontal()
        {
            float totalW = EditorGUIUtility.currentViewWidth - 40f;
            float sideW = (totalW - kSelfWidth - kGapH * 2f) / 2f;

            EditorGUILayout.BeginHorizontal();
            DrawLabeledField("◀  Left", navLeftProp, sideW);
            GUILayout.Space(kGapH);
            DrawSelfBox(kSelfWidth);
            GUILayout.Space(kGapH);
            DrawLabeledField("Right  ▶", navRightProp, sideW);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawVertical()
        {
            float fieldW = 180f;
            float padL = (EditorGUIUtility.currentViewWidth - 40f - fieldW) / 2f;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(padL);
            EditorGUILayout.BeginVertical(GUILayout.Width(fieldW));
            DrawLabeledField("▲  Up", navUpProp, fieldW);
            EditorGUILayout.Space(kGapV);
            DrawSelfBox(fieldW);
            EditorGUILayout.Space(kGapV);
            DrawLabeledField("▼  Down", navDownProp, fieldW);
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ── Drawing primitives ────────────────────────────────────────────────────

        private static void DrawLabeledField(string label, SerializedProperty prop, float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width));
            EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(width));
            EditorGUILayout.PropertyField(prop, GUIContent.none, GUILayout.Width(width), GUILayout.Height(kFieldH));
            EditorGUILayout.EndVertical();
        }

        private static void DrawSelfBox(float width)
        {
            float totalH = EditorGUIUtility.singleLineHeight + kFieldH + EditorGUIUtility.standardVerticalSpacing;
            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = Color.gray;
            GUILayout.Box("SELF", style, GUILayout.Width(width), GUILayout.Height(totalH));
        }

        private const float kSelfWidth = 90f;
        private const float kFieldH = 18f;
        private const float kGapH = 6f;
        private const float kGapV = 4f;

        // ─────────────────────────────────────────────────────────────────────────

        private void DrawVisualsTab()
        {
            EditorGUILayout.PropertyField(targetGraphicProp);
            EditorGUILayout.PropertyField(transitionProp);

            Selectable.Transition transition = (Selectable.Transition)transitionProp.enumValueIndex;

            EditorGUI.indentLevel++;
            if (transition == Selectable.Transition.ColorTint)
                EditorGUILayout.PropertyField(colorBlockProp, true);
            else if (transition == Selectable.Transition.SpriteSwap)
                EditorGUILayout.PropertyField(spriteStateProp, true);
            else if (transition == Selectable.Transition.Animation)
                EditorGUILayout.PropertyField(animationTriggersProp, true);
            EditorGUI.indentLevel--;
        }

        private void DrawEventsTab()
        {
            EditorGUILayout.PropertyField(onClickProp);
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(onSelectProp);
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(onDeselectProp);

            // Chỉ hiện event này cho Tab và SubTab layer (không hữu ích với Content)
            TISGamepadUIButton.NavigationLayer lyr = (TISGamepadUIButton.NavigationLayer)layerProp.enumValueIndex;
            if (lyr != TISGamepadUIButton.NavigationLayer.Content)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(onBecameActiveInLayerProp,
                    new GUIContent("On Became Active In Layer",
                        "Fired khi button này trở thành active trong layer của nó qua global input.\n" +
                        "UINavigationContext tự subscribe event này — bạn không cần assign thủ công."));
            }
        }
    }
}