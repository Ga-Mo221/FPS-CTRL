#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    [CustomEditor(typeof(MotionTimeEventBehaviour))]
    public class MotionTimeEventBehaviourEditor : Editor
    {
        private SerializedProperty _motionEvents;
        private SerializedProperty _showDebugLog;
        private ReorderableList    _list;

        private void OnEnable()
        {
            SetComponentIcon();
            _motionEvents = serializedObject.FindProperty("motionEvents");
            _showDebugLog = serializedObject.FindProperty("showDebugLog");

            _list = new ReorderableList(serializedObject, _motionEvents,
                draggable:          true,
                displayHeader:       true,
                displayAddButton:    true,
                displayRemoveButton: true);

            // ── Header ──────────────────────────────────────────────────────
            _list.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect,
                    $"Motion Events  [{_motionEvents.arraySize}]",
                    EditorStyles.boldLabel);
            };

            // ── Element height (dynamic — expands when unfolded) ────────────
            _list.elementHeightCallback = index =>
            {
                SerializedProperty evt = _motionEvents.GetArrayElementAtIndex(index);
                if (!evt.isExpanded)
                    return EditorGUIUtility.singleLineHeight + 4f;

                return EditorGUIUtility.singleLineHeight * 1.5f + 4f   // header + spacing
                       + GetEventFieldsHeight(evt);
            };

            // ── Draw element ────────────────────────────────────────────────
            _list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                SerializedProperty evt = _motionEvents.GetArrayElementAtIndex(index);

                float lineH   = EditorGUIUtility.singleLineHeight;
                float y       = rect.y + 2f;
                Rect  foldRect = new Rect(rect.x, y, rect.width, lineH);

                evt.isExpanded = EditorGUI.Foldout(foldRect, evt.isExpanded,
                    $"Element {index}", true);

                if (evt.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    float contentY = y + lineH + 2f;
                    DrawEventFields(rect, ref contentY, evt);
                    EditorGUI.indentLevel--;
                }
            };

            // ── Background (subtle alternating tint) ───────────────────────
            _list.drawElementBackgroundCallback = (rect, index, isActive, isFocused) =>
            {
                if (Event.current.type != EventType.Repaint) return;

                Color bg = isActive
                    ? new Color(0.24f, 0.48f, 0.90f, 0.3f)
                    : (index % 2 == 0
                        ? new Color(0f, 0f, 0f, 0.05f)
                        : Color.clear);

                EditorGUI.DrawRect(rect, bg);
            };
        }
        
        private void SetComponentIcon()
        {
            if (target == null) return;

            string[] guids = AssetDatabase.FindAssets("Event t:Texture2D");

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (path.Contains("Editor/Icon") || path.Contains("Blender-Style Workflow"))
                {
                    Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (icon != null)
                    {
                        // Gán icon cho file C# Script (MonoScript) thay vì gán trực tiếp vào instance target
                        var script = MonoScript.FromScriptableObject((ScriptableObject)target);
                        if (script != null)
                        {
                            EditorGUIUtility.SetIconForObject(script, icon);
                        }
                        break;
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            _list.DoLayoutList();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_showDebugLog, new GUIContent("Show Debug Log"));

            serializedObject.ApplyModifiedProperties();
        }

        // ── Height calculation ───────────────────────────────────────────────
        private float GetEventFieldsHeight(SerializedProperty evt)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float h = 0f;

            SerializedProperty mode        = evt.FindPropertyRelative("mode");
            SerializedProperty triggerSec  = evt.FindPropertyRelative("triggerSeconds");
            SerializedProperty normValue   = evt.FindPropertyRelative("normalizedValue");
            SerializedProperty fireOnce    = evt.FindPropertyRelative("fireOnce");
            SerializedProperty payloadType = evt.FindPropertyRelative("payloadType");

            h += EditorGUI.GetPropertyHeight(mode) + spacing;

            var modeEnum = (MotionTimeEventBehaviour.TriggerMode)mode.enumValueIndex;
            h += EditorGUI.GetPropertyHeight(modeEnum == MotionTimeEventBehaviour.TriggerMode.Seconds ? triggerSec : normValue) + spacing;

            h += EditorGUI.GetPropertyHeight(fireOnce) + spacing;
            h += EditorGUI.GetPropertyHeight(payloadType) + spacing;

            var payload = (MotionTimeEventBehaviour.EventPayloadType)payloadType.enumValueIndex;

            if (payload != MotionTimeEventBehaviour.EventPayloadType.None)
            {
                SerializedProperty valProp = payload switch
                {
                    MotionTimeEventBehaviour.EventPayloadType.Int    => evt.FindPropertyRelative("intValue"),
                    MotionTimeEventBehaviour.EventPayloadType.Float  => evt.FindPropertyRelative("floatValue"),
                    MotionTimeEventBehaviour.EventPayloadType.String => evt.FindPropertyRelative("stringValue"),
                    MotionTimeEventBehaviour.EventPayloadType.Bool   => evt.FindPropertyRelative("boolValue"),
                    _ => null
                };
                if (valProp != null)
                    h += EditorGUI.GetPropertyHeight(valProp) + spacing;
            }

            h += EditorGUIUtility.singleLineHeight * 0.5f;

            SerializedProperty unityEvt = payload switch
            {
                MotionTimeEventBehaviour.EventPayloadType.Int    => evt.FindPropertyRelative("onTriggeredInt"),
                MotionTimeEventBehaviour.EventPayloadType.Float  => evt.FindPropertyRelative("onTriggeredFloat"),
                MotionTimeEventBehaviour.EventPayloadType.String => evt.FindPropertyRelative("onTriggeredString"),
                MotionTimeEventBehaviour.EventPayloadType.Bool   => evt.FindPropertyRelative("onTriggeredBool"),
                _                                                => evt.FindPropertyRelative("onTriggered"),
            };

            h += EditorGUI.GetPropertyHeight(unityEvt, true) + spacing;

            return h;
        }

        // ── Field drawing ────────────────────────────────────────────────────
        private void DrawEventFields(Rect baseRect, ref float y, SerializedProperty evt)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float indent  = EditorGUI.indentLevel * 15f;
            float xLeft   = baseRect.x + indent;
            float width   = baseRect.width - indent;

            float DrawProp(SerializedProperty prop, GUIContent label, float currentY)
            {
                float propH = EditorGUI.GetPropertyHeight(prop, true);
                EditorGUI.PropertyField(new Rect(xLeft, currentY, width, propH), prop, label, true);
                return currentY + propH + spacing;
            }

            SerializedProperty mode        = evt.FindPropertyRelative("mode");
            SerializedProperty triggerSec  = evt.FindPropertyRelative("triggerSeconds");
            SerializedProperty normValue   = evt.FindPropertyRelative("normalizedValue");
            SerializedProperty fireOnce    = evt.FindPropertyRelative("fireOnce");
            SerializedProperty payloadType = evt.FindPropertyRelative("payloadType");

            y = DrawProp(mode, new GUIContent("Trigger Mode", "Seconds: nhập giây (vd: 1.5s)\nNormalized: nhập tỉ lệ 0→1 (vd: 0.85 = 85% clip)"), y);

            var modeEnum = (MotionTimeEventBehaviour.TriggerMode)mode.enumValueIndex;
            if (modeEnum == MotionTimeEventBehaviour.TriggerMode.Seconds)
                y = DrawProp(triggerSec, new GUIContent("Trigger Seconds", "Thời điểm kích hoạt (giây tính theo clip gốc, không phụ thuộc speed)."), y);
            else
                y = DrawProp(normValue, new GUIContent("Normalized Value", "Thời điểm kích hoạt (0 = đầu clip, 1 = cuối clip). Không phụ thuộc speed."), y);

            y = DrawProp(fireOnce, new GUIContent("Fire Once", "true = chỉ fire 1 lần...\nfalse = fire mỗi loop..."), y);
            y = DrawProp(payloadType, new GUIContent("Payload Type"), y);

            var payload = (MotionTimeEventBehaviour.EventPayloadType)payloadType.enumValueIndex;

            SerializedProperty valProp = payload switch
            {
                MotionTimeEventBehaviour.EventPayloadType.Int    => evt.FindPropertyRelative("intValue"),
                MotionTimeEventBehaviour.EventPayloadType.Float  => evt.FindPropertyRelative("floatValue"),
                MotionTimeEventBehaviour.EventPayloadType.String => evt.FindPropertyRelative("stringValue"),
                MotionTimeEventBehaviour.EventPayloadType.Bool   => evt.FindPropertyRelative("boolValue"),
                _ => null
            };

            if (valProp != null)
            {
                string valLabel = payload switch
                {
                    MotionTimeEventBehaviour.EventPayloadType.Int    => "Int Value",
                    MotionTimeEventBehaviour.EventPayloadType.Float  => "Float Value",
                    MotionTimeEventBehaviour.EventPayloadType.String => "String Value",
                    MotionTimeEventBehaviour.EventPayloadType.Bool   => "Bool Value",
                    _ => ""
                };
                y = DrawProp(valProp, new GUIContent(valLabel), y);
            }

            y += EditorGUIUtility.singleLineHeight * 0.5f;

            SerializedProperty unityEvt = payload switch
            {
                MotionTimeEventBehaviour.EventPayloadType.Int    => evt.FindPropertyRelative("onTriggeredInt"),
                MotionTimeEventBehaviour.EventPayloadType.Float  => evt.FindPropertyRelative("onTriggeredFloat"),
                MotionTimeEventBehaviour.EventPayloadType.String => evt.FindPropertyRelative("onTriggeredString"),
                MotionTimeEventBehaviour.EventPayloadType.Bool   => evt.FindPropertyRelative("onTriggeredBool"),
                _                                                => evt.FindPropertyRelative("onTriggered"),
            };

            string evtLabel = payload switch
            {
                MotionTimeEventBehaviour.EventPayloadType.Int    => "On Triggered (int)",
                MotionTimeEventBehaviour.EventPayloadType.Float  => "On Triggered (float)",
                MotionTimeEventBehaviour.EventPayloadType.String => "On Triggered (string)",
                MotionTimeEventBehaviour.EventPayloadType.Bool   => "On Triggered (bool)",
                _                                                => "On Triggered",
            };

            y = DrawProp(unityEvt, new GUIContent(evtLabel), y);
        }
    }
}
#endif