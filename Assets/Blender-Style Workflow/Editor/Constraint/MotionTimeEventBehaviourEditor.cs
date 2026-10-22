#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;
using PayloadType = GaMo.BlenderEditorTools.MotionTimeEventBehaviour.EventPayloadType;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Inspector cho MotionTimeEventBehaviour.
    ///
    /// Ô "Function" tự liệt kê mọi hàm public "void M(Animator)" / "void M(Animator, T)" trên target
    /// (T = int, float, string, bool, Vector2/3/4, Color, enum, Object con). Chọn hàm → payloadType
    /// TỰ cập nhật theo kiểu tham số thứ 2 (reflection) và ô nhập giá trị đổi theo:
    ///   - int/float/string/bool/Vector/Color → ghi vào UnityEvent tương ứng (persistent call #0),
    ///     dữ liệu cũ vẫn là UnityEvent như trước nên tương thích ngược hoàn toàn.
    ///   - enum/Object → ghi vào AnimatorTypedCall (dropdown đúng các giá trị enum hàm đích nhận,
    ///     ObjectField lọc đúng kiểu, ví dụ AudioClip).
    /// UnityEvent gốc (nhiều listener, Runtime Only/Off...) và Payload Type thủ công vẫn nằm trong
    /// foldout "Nâng cao".
    /// </summary>
    [CustomEditor(typeof(MotionTimeEventBehaviour))]
    public class MotionTimeEventBehaviourEditor : Editor
    {
        private SerializedProperty _motionEvents;
        private SerializedProperty _showDebugLog;
        private ReorderableList    _list;

        private void OnEnable()
        {
            // Inspector của StateMachineBehaviour có thể được OnEnable với target đã bị destroy
            // (vào/ra Play mode, lưu/reimport Animator Controller) → serializedObject ném
            // SerializedObjectNotCreatableException. Bỏ qua, Unity sẽ tạo lại Editor với target mới.
            if (target == null) return;

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

                // Tóm tắt "Hàm @ thời điểm" bên phải header — đọc nhanh khi đã thu gọn.
                Rect sumRect = new Rect(rect.x + 80f, y, rect.width - 80f, lineH);
                EditorGUI.LabelField(sumRect, BuildSummary(evt), s_SummaryStyle ??= new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    clipping  = TextClipping.Clip
                });

                if (evt.isExpanded)
                {
                    float contentY = y + lineH + 2f;
                    DrawEventFields(rect, ref contentY, evt);
                }
            };

            // ── Chọn event => Preview (GM-Inspector) tua tới thời điểm của nó + tô nổi vạch ──
            _list.onSelectCallback = list =>
            {
                if (list.index >= 0 && list.index < _motionEvents.arraySize)
                    SelectForPreview(_motionEvents.GetArrayElementAtIndex(list.index));
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

        public override void OnInspectorGUI()
        {
            if (target == null || _list == null) return;

            serializedObject.Update();

            _list.DoLayoutList();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_showDebugLog, new GUIContent("Show Debug Log"));

            serializedObject.ApplyModifiedProperties();
        }

        // ── Height calculation ───────────────────────────────────────────────
        // Dùng CHUNG LayoutEventFields với lúc vẽ (draw=false) để chiều cao luôn khớp nội dung.
        private float GetEventFieldsHeight(SerializedProperty evt)
        {
            float y = 0f;
            LayoutEventFields(default, ref y, evt, false);
            return y;
        }

        // ── Field drawing ────────────────────────────────────────────────────
        private void DrawEventFields(Rect baseRect, ref float y, SerializedProperty evt)
        {
            LayoutEventFields(baseRect, ref y, evt, true);
        }

        private static GUIStyle s_SummaryStyle;

        private static readonly GUIContent[] s_ModeOptions =
        {
            new GUIContent("Seconds",    "Nhập giây (vd: 1.5s), tính theo clip gốc, không phụ thuộc speed."),
            new GUIContent("Normalized", "Nhập tỉ lệ 0→1 (vd: 0.85 = 85% clip). Không phụ thuộc speed."),
        };

        private static readonly GUIContent s_OnceContent = new GUIContent("Once",
            "Bật  = chỉ fire 1 lần trong suốt lifetime của state.\nTắt = fire mỗi lần animation loop qua vị trí này.");

        private static readonly GUIContent s_AdvancedContent = new GUIContent("Advanced",
            "Payload Type thủ công + UnityEvent gốc (nhiều listener, Runtime Only/Off...)");

        private static string BuildSummary(SerializedProperty evt)
        {
            var payload = (PayloadType)evt.FindPropertyRelative("payloadType").intValue;
            Binding b   = ReadBinding(evt, payload);
            string fn   = string.IsNullOrEmpty(b.methodName) ? "No Function" : b.methodName;

            bool seconds = evt.FindPropertyRelative("mode").intValue == (int)MotionTimeEventBehaviour.TriggerMode.Seconds;
            string time  = seconds
                ? $"{evt.FindPropertyRelative("triggerSeconds").floatValue:0.###}s"
                : $"{evt.FindPropertyRelative("normalizedValue").floatValue:0.###}";
            return $"{fn}  @ {time}";
        }

        private void LayoutEventFields(Rect baseRect, ref float y, SerializedProperty evt, bool draw)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float lineH   = EditorGUIUtility.singleLineHeight;
            // Không dùng indentLevel: mọi ô đều không có label, chỉ thụt vào dưới foldout.
            const float indent = 14f;
            float xLeft   = baseRect.x + indent;
            float width   = baseRect.width - indent;
            int   oldIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            float DrawProp(SerializedProperty prop, GUIContent label, float currentY)
            {
                float propH = EditorGUI.GetPropertyHeight(prop, label, true);
                if (draw)
                    EditorGUI.PropertyField(new Rect(xLeft, currentY, width, propH), prop, label, true);
                return currentY + propH + spacing;
            }

            Rect NextLine(ref float currentY)
            {
                var r = new Rect(xLeft, currentY, width, lineH);
                currentY += lineH + spacing;
                return r;
            }

            void Warn(string msg, ref float currentY)
            {
                Rect r = NextLine(ref currentY);
                if (draw)
                    EditorGUI.LabelField(r, GUIContent.none,
                        new GUIContent(msg, EditorGUIUtility.IconContent("console.warnicon.sml").image, msg),
                        EditorStyles.miniLabel);
            }

            SerializedProperty mode        = evt.FindPropertyRelative("mode");
            SerializedProperty triggerSec  = evt.FindPropertyRelative("triggerSeconds");
            SerializedProperty normValue   = evt.FindPropertyRelative("normalizedValue");
            SerializedProperty fireOnce    = evt.FindPropertyRelative("fireOnce");
            SerializedProperty payloadType = evt.FindPropertyRelative("payloadType");

            // ── Dòng 1: [Mode] [thời điểm] [Once] ───────────────────────────
            Rect timeRow = NextLine(ref y);
            if (draw)
            {
                const float modeW = 92f, onceW = 52f, pickW = 24f, gap = 4f;
                Rect modeRect = new Rect(timeRow.x, timeRow.y, modeW, lineH);
                Rect onceRect = new Rect(timeRow.xMax - onceW, timeRow.y, onceW, lineH);
                Rect pickRect = new Rect(onceRect.x - gap - pickW, timeRow.y, pickW, lineH);
                Rect valRect  = new Rect(modeRect.xMax + gap, timeRow.y,
                    pickRect.x - gap - (modeRect.xMax + gap), lineH);

                EditorGUI.BeginChangeCheck();
                int modeIdx = EditorGUI.Popup(modeRect, GUIContent.none, mode.enumValueIndex, s_ModeOptions);
                bool timeChanged = EditorGUI.EndChangeCheck();
                if (timeChanged) mode.enumValueIndex = modeIdx;

                // PropertyField giữ [Min(0)] / [Range(0,1)] (slider) của field gốc.
                bool seconds = mode.enumValueIndex == (int)MotionTimeEventBehaviour.TriggerMode.Seconds;
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(valRect, seconds ? triggerSec : normValue, GUIContent.none);
                timeChanged |= EditorGUI.EndChangeCheck();

                // Nút lấy thời điểm từ playhead của Preview (GM-Inspector). Chỉ chạy khi bấm => không có vòng phản hồi.
                // Ghi qua SerializedProperty => ApplyModifiedProperties ở OnInspectorGUI tạo Undo như sửa tay.
                bool canPick = TryGetPreviewTime(out float pickN);
                using (new EditorGUI.DisabledScope(!canPick))
                {
                    if (GUI.Button(pickRect, s_PickContent ??= MakePickContent(), EditorStyles.miniButton) && canPick)
                    {
                        if (seconds)
                        {
                            float len = GaMoPreviewTimeBridge.MotionLength(ResolveContext().motion);
                            triggerSec.floatValue = Mathf.Round(pickN * len * 1000f) / 1000f;
                        }
                        else normValue.floatValue = Mathf.Round(pickN * 10000f) / 10000f;
                        GUI.changed = true;
                        SelectForPreview(evt);
                    }
                }

                // Kéo thời điểm => khung Preview của GM-Inspector (nếu đang mở, đúng State này) tua theo.
                // Chỉ đổi trạng thái Preview; giá trị thật vẫn ghi qua serializedObject (có Undo) như cũ.
                if (timeChanged)
                    SelectForPreview(evt);

                EditorGUI.BeginChangeCheck();
                bool once = EditorGUI.ToggleLeft(onceRect, s_OnceContent, fireOnce.boolValue);
                if (EditorGUI.EndChangeCheck()) fireOnce.boolValue = once;
            }

            // ── Dòng 2: Function (tự nhận kiểu) ─────────────────────────────
            var payload = (PayloadType)payloadType.intValue;
            Binding binding = ReadBinding(evt, payload);

            Rect funcRect = NextLine(ref y);
            if (draw) DrawFunctionRow(funcRect, evt, binding);

            if (binding.missing)
                Warn($"Không tìm thấy hàm '{binding.methodName}' khớp {PayloadLabel(payload)} trên target.", ref y);
            if (binding.extraListeners > 0)
                Warn($"+{binding.extraListeners} listener khác trên UnityEvent (xem Advanced).", ref y);

            // ── Dòng 3: giá trị tham số (ô nhập theo kiểu, không label) ──────
            if (payload != PayloadType.None)
                LayoutValueField(evt, payload, binding, xLeft, width, ref y, draw, Warn);

            // ── Advanced: payloadType thủ công + UnityEvent gốc ─────────────
            Rect advRect = NextLine(ref y);
            if (draw)
                payloadType.isExpanded = EditorGUI.Foldout(advRect, payloadType.isExpanded, s_AdvancedContent, true,
                    EditorStyles.foldout);

            if (payloadType.isExpanded)
            {
                y = DrawProp(payloadType, new GUIContent("Payload Type", "Tự cập nhật khi chọn Function. Chỉ đổi tay khi cần."), y);

                SerializedProperty unityEvt = GetUnityEventProp(evt, payload);
                if (unityEvt != null)
                    y = DrawProp(unityEvt, new GUIContent("On Triggered"), y); // drawer tự thêm "(Animator, T)"
                else
                {
                    var typed = evt.FindPropertyRelative("onTriggeredTyped");
                    y = DrawProp(typed, new GUIContent("Typed Call (reflection)"), y);
                }
            }

            EditorGUI.indentLevel = oldIndentLevel;
        }

        // ── Preview sync (GM-Inspector) ──────────────────────────────────────
        // Event đang chọn (để Preview tô nổi vạch của nó). Static: Preview hỏi qua provider tĩnh, không giữ editor.
        private static MotionTimeEventBehaviour s_SelBehaviour;
        private static int s_SelIndex = -1;
        private static GUIContent s_PickContent;

        // State chứa behaviour: dò 1 lần cho mỗi editor (editor được dựng lại khi đổi selection). Motion đọc mới mỗi lần.
        private AnimatorState _ctxState;
        private bool _ctxResolved;

        private (AnimatorState state, Motion motion) ResolveContext()
        {
            if (!_ctxResolved)
            {
                _ctxResolved = true;
                GaMoPreviewTimeBridge.TryResolveState(target as StateMachineBehaviour, out _ctxState, out _);
            }
            return (_ctxState, _ctxState != null ? _ctxState.motion : null);
        }

        private static GUIContent MakePickContent()
        {
            var gc = new GUIContent(EditorGUIUtility.IconContent("EyeDropper.Large"));
            if (gc.image == null) gc.text = "◉";
            gc.tooltip = "Lấy thời điểm từ playhead của Preview (GM-Inspector).\n" +
                         "Cần: Preview đang mở và đang xem Motion của State này.";
            return gc;
        }

        private bool TryGetPreviewTime(out float n)
        {
            n = 0f;
            if (!GaMoPreviewTimeBridge.HasTimeSources) return false;
            var (state, motion) = ResolveContext();
            return GaMoPreviewTimeBridge.TryGetPreviewTime(state, motion, out n);
        }

        private static int IndexFromPath(string path)
        {
            int a = path.LastIndexOf('['), b = path.LastIndexOf(']');
            return a >= 0 && b > a && int.TryParse(path.Substring(a + 1, b - a - 1), out int i) ? i : -1;
        }

        /// <summary>Normalized time của 1 event theo độ dài Motion. Seconds mà không biết độ dài => -1.</summary>
        private static float NormalizedOf(SerializedProperty evt, float motionLength)
        {
            if (evt.FindPropertyRelative("mode").intValue == (int)MotionTimeEventBehaviour.TriggerMode.Seconds)
                return motionLength > 1e-5f
                    ? Mathf.Clamp01(evt.FindPropertyRelative("triggerSeconds").floatValue / motionLength)
                    : -1f;
            return Mathf.Clamp01(evt.FindPropertyRelative("normalizedValue").floatValue);
        }

        /// <summary>Đánh dấu event đang chọn + tua Preview tới thời điểm của nó.</summary>
        private void SelectForPreview(SerializedProperty evt)
        {
            s_SelBehaviour = target as MotionTimeEventBehaviour;
            s_SelIndex     = IndexFromPath(evt.propertyPath);

            // Không có cửa sổ nào nghe (chưa mở GM-Inspector) thì bỏ qua ngay, không dò State.
            if (!GaMoPreviewTimeBridge.HasListeners) return;
            var (state, motion) = ResolveContext();
            if (state == null || motion == null) return;

            float n = NormalizedOf(evt, GaMoPreviewTimeBridge.MotionLength(motion));
            if (n >= 0f) GaMoPreviewTimeBridge.RequestSeek(state, motion, n);
        }

        // ── Vạch event trên Preview (provider tĩnh, đăng ký lại sau mỗi lần reload domain) ──
        // SerializedObject riêng, chỉ đọc (không Apply) => không làm dirty asset. Cache theo behaviour.
        private static readonly Dictionary<MotionTimeEventBehaviour, SerializedObject> s_MarkerSo =
            new Dictionary<MotionTimeEventBehaviour, SerializedObject>();

        [InitializeOnLoadMethod]
        private static void RegisterPreviewMarkers()
        {
            GaMoPreviewTimeBridge.MarkersRequested -= CollectPreviewMarkers;
            GaMoPreviewTimeBridge.MarkersRequested += CollectPreviewMarkers;
        }

        private static void CollectPreviewMarkers(AnimatorState state, Motion motion,
            List<GaMoPreviewTimeBridge.TimeMarker> output)
        {
            float len = GaMoPreviewTimeBridge.MotionLength(motion);
            foreach (StateMachineBehaviour smb in state.behaviours)
            {
                if (!(smb is MotionTimeEventBehaviour b)) continue;

                if (!s_MarkerSo.TryGetValue(b, out SerializedObject so) || so.targetObject == null)
                {
                    if (s_MarkerSo.Count >= 32) PurgeMarkerCache();
                    so = new SerializedObject(b);
                    s_MarkerSo[b] = so;
                }
                else so.UpdateIfRequiredOrScript();

                SerializedProperty arr = so.FindProperty("motionEvents");
                if (arr == null) continue;
                for (int i = 0; i < arr.arraySize; i++)
                {
                    SerializedProperty evt = arr.GetArrayElementAtIndex(i);
                    float n = NormalizedOf(evt, len);
                    if (n < 0f) continue;
                    output.Add(new GaMoPreviewTimeBridge.TimeMarker
                    {
                        normalizedTime = n,
                        label          = BuildSummary(evt),
                        selected       = b == s_SelBehaviour && i == s_SelIndex
                    });
                }
            }
        }

        private static void PurgeMarkerCache()
        {
            foreach (var so in s_MarkerSo.Values)
                so?.Dispose();
            s_MarkerSo.Clear();
        }

        // ── Function row ─────────────────────────────────────────────────────
        private void DrawFunctionRow(Rect rect, SerializedProperty evt, Binding binding)
        {
            // Không label: [target][hàm] chiếm cả dòng.
            Rect content = rect;

            float objW   = Mathf.Min(content.width * 0.4f, 170f);
            Rect objRect = new Rect(content.x, content.y, objW, content.height);
            Rect btnRect = new Rect(content.x + objW + 2f, content.y, content.width - objW - 2f, content.height);

            SerializedObject so   = evt.serializedObject;
            string           path = evt.propertyPath;

            // Target: controller asset không chứa được scene object → allowSceneObjects = false.
            EditorGUI.BeginChangeCheck();
            Object newTarget = EditorGUI.ObjectField(objRect, binding.target, typeof(Object), false);
            if (EditorGUI.EndChangeCheck())
            {
                // Giữ hàm cũ nếu target mới cũng có hàm cùng tên + cùng kiểu, ngược lại bỏ chọn hàm.
                MethodInfo keep = newTarget != null && (binding.isVoid || binding.paramKnown)
                    ? FindBindableMethod(newTarget.GetType(), binding.methodName, binding.paramType, binding.isVoid)
                    : null;
                ApplyBinding(so, path, newTarget, keep);
            }

            // Ô target đã hiện tên type → nút chỉ cần tên hàm + kiểu tham số (tooltip có đủ).
            string paramText = ParamLabel(binding.isVoid ? null : binding.paramType, binding.payload);
            string btnText   = string.IsNullOrEmpty(binding.methodName)
                ? "No Function"
                : $"{binding.methodName} ({paramText})";
            string btnTip = string.IsNullOrEmpty(binding.methodName) || binding.target == null
                ? btnText
                : $"{binding.target.GetType().Name}.{binding.methodName}(Animator{(binding.isVoid ? "" : ", " + paramText)})";

            using (new EditorGUI.DisabledScope(binding.target == null))
            {
                if (EditorGUI.DropdownButton(btnRect, new GUIContent(btnText, btnTip), FocusType.Keyboard))
                    BuildFunctionMenu(so, path, binding).DropDown(btnRect);
            }
        }

        private GenericMenu BuildFunctionMenu(SerializedObject so, string path, Binding binding)
        {
            var menu = new GenericMenu();
            Object current = binding.target;

            menu.AddItem(new GUIContent("No Function"), string.IsNullOrEmpty(binding.methodName),
                () => ApplyBinding(so, path, current, null));
            menu.AddSeparator("");

            var candidates = GetCandidateObjects(current);
            bool useSubMenu = candidates.Count > 1;

            foreach (Object obj in candidates)
            {
                string prefix = useSubMenu ? obj.GetType().Name + "/" : "";
                foreach (MethodInfo mi in GetBindableMethods(obj.GetType()))
                {
                    ParameterInfo[] ps = mi.GetParameters();
                    Type pt = ps.Length == 2 ? ps[1].ParameterType : null;
                    PayloadType pl = pt == null ? PayloadType.None : ClassifyParam(pt).GetValueOrDefault();

                    bool on = obj == current && mi.Name == binding.methodName && binding.payload == pl &&
                              (pt == null ? binding.isVoid : pt == binding.paramType);

                    Object  capturedObj = obj;
                    MethodInfo capturedMi = mi;
                    menu.AddItem(new GUIContent($"{prefix}{mi.Name} ({ParamLabel(pt, pl)})"), on,
                        () => ApplyBinding(so, path, capturedObj, capturedMi));
                }
            }

            return menu;
        }

        /// <summary>
        /// Ghi target + hàm vào đúng chỗ lưu theo kiểu tham số, tự đặt payloadType.
        /// method == null → chỉ đổi target, bỏ chọn hàm (giữ nguyên payloadType).
        /// Chạy trong callback của GenericMenu (ngoài OnGUI) nên tự Update/Apply SerializedObject.
        /// </summary>
        private void ApplyBinding(SerializedObject so, string path, Object newTarget, MethodInfo method)
        {
            if (so == null || so.targetObject == null) return;

            so.Update();
            SerializedProperty evt = so.FindProperty(path);
            if (evt == null) return;

            SerializedProperty payloadProp = evt.FindPropertyRelative("payloadType");
            var oldType = (PayloadType)payloadProp.intValue;

            Type paramType = null;
            PayloadType newType = oldType;
            if (method != null)
            {
                ParameterInfo[] ps = method.GetParameters();
                paramType = ps.Length == 2 ? ps[1].ParameterType : null;
                newType   = paramType == null ? PayloadType.None : ClassifyParam(paramType).GetValueOrDefault();
            }

            // Đổi kiểu → gỡ listener #0 khỏi chỗ lưu cũ (listener thêm khác, nếu có, được giữ nguyên).
            if (newType != oldType)
                ClearSlot(evt, oldType);

            payloadProp.intValue = (int)newType;
            WriteSlot(evt, newType, newTarget, method, paramType);

            if (paramType != null)
                InitValueForParam(evt, newType, paramType);

            so.ApplyModifiedProperties();
            Repaint();
        }

        private static void ClearSlot(SerializedProperty evt, PayloadType type)
        {
            if (IsTyped(type))
            {
                var typed = evt.FindPropertyRelative("onTriggeredTyped");
                typed.FindPropertyRelative("target").objectReferenceValue = null;
                typed.FindPropertyRelative("methodName").stringValue    = "";
                typed.FindPropertyRelative("parameterType").stringValue = "";
                return;
            }

            SerializedProperty calls = GetUnityEventProp(evt, type)?.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (calls != null && calls.arraySize > 0)
                calls.DeleteArrayElementAtIndex(0);
        }

        private static void WriteSlot(SerializedProperty evt, PayloadType type, Object tgt, MethodInfo method,
            Type paramType)
        {
            string methodName = method != null ? method.Name : "";

            if (IsTyped(type))
            {
                var typed = evt.FindPropertyRelative("onTriggeredTyped");
                typed.FindPropertyRelative("target").objectReferenceValue = tgt;
                typed.FindPropertyRelative("methodName").stringValue      = methodName;
                typed.FindPropertyRelative("parameterType").stringValue   =
                    paramType != null ? AnimatorTypedCall.FormatTypeName(paramType) : "";
                return;
            }

            SerializedProperty calls = GetUnityEventProp(evt, type).FindPropertyRelative("m_PersistentCalls.m_Calls");
            bool isNew = calls.arraySize == 0;
            if (isNew)
            {
                if (tgt == null) return; // không có gì để ghi
                calls.InsertArrayElementAtIndex(0);
            }

            SerializedProperty call = calls.GetArrayElementAtIndex(0);
            call.FindPropertyRelative("m_Target").objectReferenceValue = tgt;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
                tgt != null ? AnimatorTypedCall.FormatTypeName(tgt.GetType()) : "";
            call.FindPropertyRelative("m_MethodName").stringValue = methodName;
            call.FindPropertyRelative("m_Mode").intValue = (int)PersistentListenerMode.EventDefined;

            if (isNew)
            {
                // Giống mặc định của UnityEventDrawer khi bấm "+".
                call.FindPropertyRelative("m_CallState").intValue = (int)UnityEventCallState.RuntimeOnly;
                var args = call.FindPropertyRelative("m_Arguments");
                args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = null;
                args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue =
                    AnimatorTypedCall.FormatTypeName(typeof(Object));
                args.FindPropertyRelative("m_IntArgument").intValue     = 0;
                args.FindPropertyRelative("m_FloatArgument").floatValue = 0f;
                args.FindPropertyRelative("m_StringArgument").stringValue = "";
                args.FindPropertyRelative("m_BoolArgument").boolValue   = false;
            }
        }

        /// <summary>Giá trị cũ không hợp lệ với kiểu tham số mới → đặt lại giá trị hợp lệ.</summary>
        private static void InitValueForParam(SerializedProperty evt, PayloadType type, Type paramType)
        {
            if (type == PayloadType.Enum)
            {
                var nameProp  = evt.FindPropertyRelative("enumName");
                var valueProp = evt.FindPropertyRelative("enumValue");
                if (TryGetEnumByName(paramType, nameProp.stringValue, out _)) return;

                Array values = Enum.GetValues(paramType);
                object first = values.Length > 0 ? values.GetValue(0) : Enum.ToObject(paramType, 0);
                nameProp.stringValue = first.ToString();
                valueProp.longValue  = EnumToLong(first);
            }
            else if (type == PayloadType.Object)
            {
                var objProp = evt.FindPropertyRelative("objectValue");
                if (objProp.objectReferenceValue != null && !paramType.IsInstanceOfType(objProp.objectReferenceValue))
                    objProp.objectReferenceValue = null;
            }
        }

        // ── Value field ──────────────────────────────────────────────────────
        private delegate void WarnFn(string msg, ref float y);

        private static void LayoutValueField(SerializedProperty evt, PayloadType payload, Binding binding,
            float xLeft, float width, ref float y, bool draw, WarnFn warn)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float lineH   = EditorGUIUtility.singleLineHeight;

            // Không label — ô nhập nằm ngay dưới Function, kiểu đã hiện trên nút chọn hàm.
            GUIContent label = GUIContent.none;

            string valuePropName = payload switch
            {
                PayloadType.Int     => "intValue",
                PayloadType.Float   => "floatValue",
                PayloadType.String  => "stringValue",
                PayloadType.Bool    => "boolValue",
                PayloadType.Vector2 => "vector2Value",
                PayloadType.Vector3 => "vector3Value",
                PayloadType.Vector4 => "vector4Value",
                PayloadType.Color   => "colorValue",
                _                   => null
            };

            if (valuePropName != null)
            {
                var prop = evt.FindPropertyRelative(valuePropName);
                float h  = EditorGUI.GetPropertyHeight(prop, label, true);
                if (draw) EditorGUI.PropertyField(new Rect(xLeft, y, width, h), prop, label, true);
                y += h + spacing;
                return;
            }

            if (payload == PayloadType.Enum)
            {
                var nameProp  = evt.FindPropertyRelative("enumName");
                var valueProp = evt.FindPropertyRelative("enumValue");
                Type enumType = binding.paramKnown && binding.paramType.IsEnum ? binding.paramType : null;

                Rect r = new Rect(xLeft, y, width, lineH);
                y += lineH + spacing;

                if (enumType == null)
                {
                    if (draw)
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUI.TextField(r, label, $"{nameProp.stringValue} ({valueProp.longValue})");
                    warn("Chưa xác định được kiểu enum — chọn Function có tham số enum.", ref y);
                    return;
                }

                bool nameOk = TryGetEnumByName(enumType, nameProp.stringValue, out object current);
                if (!nameOk)
                    current = Enum.ToObject(enumType, valueProp.longValue);

                if (draw)
                {
                    EditorGUI.BeginChangeCheck();
                    Enum picked = enumType.IsDefined(typeof(FlagsAttribute), false)
                        ? EditorGUI.EnumFlagsField(r, label, (Enum)current)
                        : EditorGUI.EnumPopup(r, label, (Enum)current);
                    if (EditorGUI.EndChangeCheck())
                    {
                        nameProp.stringValue = picked.ToString();
                        valueProp.longValue  = EnumToLong(picked);
                    }
                }

                if (!nameOk && !string.IsNullOrEmpty(nameProp.stringValue))
                    warn($"Tên '{nameProp.stringValue}' không còn trong {enumType.Name} → đang dùng giá trị số {valueProp.longValue}.", ref y);
                return;
            }

            if (payload == PayloadType.Object)
            {
                var objProp = evt.FindPropertyRelative("objectValue");
                Type objType = binding.paramKnown && typeof(Object).IsAssignableFrom(binding.paramType)
                    ? binding.paramType
                    : typeof(Object);

                Rect r = new Rect(xLeft, y, width, lineH);
                y += lineH + spacing;
                if (draw)
                {
                    EditorGUI.BeginChangeCheck();
                    Object picked = EditorGUI.ObjectField(r, label, objProp.objectReferenceValue, objType, false);
                    if (EditorGUI.EndChangeCheck())
                        objProp.objectReferenceValue = picked;
                }

                if (objProp.objectReferenceValue != null && !objType.IsInstanceOfType(objProp.objectReferenceValue))
                    warn($"Object hiện tại không phải {objType.Name} → runtime sẽ truyền null.", ref y);
            }
        }

        // ── Binding (đọc hàm đang chọn theo payloadType) ─────────────────────
        private struct Binding
        {
            public PayloadType payload;
            public Object      target;
            public string      methodName;
            public bool        isVoid;      // hàm chỉ nhận Animator
            public Type        paramType;   // kiểu tham số thứ 2 (null nếu không xác định)
            public bool        paramKnown;
            public string      paramName;
            public bool        missing;     // có tên hàm nhưng không resolve được
            public int         extraListeners;
        }

        private static Binding ReadBinding(SerializedProperty evt, PayloadType payload)
        {
            var b = new Binding { payload = payload, isVoid = payload == PayloadType.None };

            if (IsTyped(payload))
            {
                var typed    = evt.FindPropertyRelative("onTriggeredTyped");
                b.target     = typed.FindPropertyRelative("target").objectReferenceValue;
                b.methodName = typed.FindPropertyRelative("methodName").stringValue;
                string ptn   = typed.FindPropertyRelative("parameterType").stringValue;

                MethodInfo mi = b.target != null
                    ? CachedTypedMethod(b.target.GetType(), b.methodName, ptn)
                    : null;
                if (mi != null)
                {
                    ParameterInfo p = mi.GetParameters()[1];
                    b.paramType = p.ParameterType;
                    b.paramName = p.Name;
                }
                else if (!string.IsNullOrEmpty(ptn))
                {
                    b.paramType = Type.GetType(ptn, false); // vẫn hiện được dropdown enum khi target mất
                }

                b.paramKnown = b.paramType != null;
                b.missing    = b.target != null && !string.IsNullOrEmpty(b.methodName) && mi == null;
                return b;
            }

            b.paramType  = NativeParamType(payload);
            b.paramKnown = b.paramType != null;

            SerializedProperty calls = GetUnityEventProp(evt, payload).FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (calls.arraySize == 0) return b;

            SerializedProperty call = calls.GetArrayElementAtIndex(0);
            b.target         = call.FindPropertyRelative("m_Target").objectReferenceValue;
            b.methodName     = call.FindPropertyRelative("m_MethodName").stringValue;
            b.extraListeners = calls.arraySize - 1;

            if (b.target != null && !string.IsNullOrEmpty(b.methodName))
            {
                MethodInfo mi = CachedNativeMethod(b.target.GetType(), b.methodName, payload);
                b.missing = mi == null;
                if (mi != null && !b.isVoid)
                    b.paramName = mi.GetParameters()[1].Name;
            }

            return b;
        }

        // ── Reflection helpers (cache theo domain, xoá khi recompile) ─────────
        private static readonly Dictionary<(Type, string, string), MethodInfo> s_TypedCache =
            new Dictionary<(Type, string, string), MethodInfo>();

        private static readonly Dictionary<(Type, string, PayloadType), MethodInfo> s_NativeCache =
            new Dictionary<(Type, string, PayloadType), MethodInfo>();

        private static readonly Dictionary<Type, List<MethodInfo>> s_BindableCache =
            new Dictionary<Type, List<MethodInfo>>();

        private static MethodInfo CachedTypedMethod(Type t, string name, string paramTypeName)
        {
            var key = (t, name ?? "", paramTypeName ?? "");
            if (!s_TypedCache.TryGetValue(key, out MethodInfo mi))
                s_TypedCache[key] = mi = AnimatorTypedCall.FindMethod(t, name, paramTypeName);
            return mi;
        }

        private static MethodInfo CachedNativeMethod(Type t, string name, PayloadType payload)
        {
            var key = (t, name ?? "", payload);
            if (!s_NativeCache.TryGetValue(key, out MethodInfo mi))
            {
                Type pt = NativeParamType(payload);
                Type[] args = pt == null ? new[] { typeof(Animator) } : new[] { typeof(Animator), pt };
                // Cùng luật resolve với UnityEvent lúc runtime.
                s_NativeCache[key] = mi = UnityEventBase.GetValidMethodInfo(t, name, args);
            }
            return mi;
        }

        /// <summary>Tìm hàm có cùng tên + cùng kiểu tham số trên một type khác (khi đổi target).</summary>
        private static MethodInfo FindBindableMethod(Type t, string name, Type paramType, bool isVoid)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (MethodInfo mi in GetBindableMethods(t))
            {
                if (mi.Name != name) continue;
                ParameterInfo[] ps = mi.GetParameters();
                if (isVoid ? ps.Length == 1 : ps.Length == 2 && ps[1].ParameterType == paramType)
                    return mi;
            }
            return null;
        }

        /// <summary>
        /// Mọi hàm public instance "void M(Animator)" hoặc "void M(Animator, T)" với T hỗ trợ.
        /// Bỏ qua hàm khai báo trong UnityEngine (vd OnStateMachineEnter(Animator,int) của
        /// StateMachineBehaviour) và property setter.
        /// </summary>
        private static List<MethodInfo> GetBindableMethods(Type t)
        {
            if (s_BindableCache.TryGetValue(t, out var list)) return list;

            list = new List<MethodInfo>();
            foreach (MethodInfo mi in t.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (mi.ReturnType != typeof(void) || mi.IsSpecialName || mi.IsGenericMethodDefinition) continue;

                Type declaring = mi.GetBaseDefinition().DeclaringType;
                if (declaring != null && declaring.Assembly.GetName().Name.StartsWith("UnityEngine")) continue;

                ParameterInfo[] ps = mi.GetParameters();
                if (ps.Length < 1 || ps.Length > 2 || ps[0].ParameterType != typeof(Animator)) continue;
                if (ps.Length == 2 && ClassifyParam(ps[1].ParameterType) == null) continue;

                list.Add(mi);
            }

            list.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.Name, b.Name);
                return c != 0 ? c : a.GetParameters().Length.CompareTo(b.GetParameters().Length);
            });
            s_BindableCache[t] = list;
            return list;
        }

        /// <summary>Giống UnityEventDrawer: GameObject/Component → liệt kê cả GameObject và mọi Component.</summary>
        private static List<Object> GetCandidateObjects(Object target)
        {
            var result = new List<Object>();
            if (target == null) return result;

            GameObject go = target as GameObject ?? (target as Component)?.gameObject;
            if (go == null)
            {
                result.Add(target);
                return result;
            }

            result.Add(go);
            foreach (Component c in go.GetComponents<Component>())
                if (c != null) result.Add(c);
            return result;
        }

        /// <summary>Kiểu tham số thứ 2 → payloadType. null = không hỗ trợ.</summary>
        private static PayloadType? ClassifyParam(Type t)
        {
            if (t == typeof(int))     return PayloadType.Int;
            if (t == typeof(float))   return PayloadType.Float;
            if (t == typeof(string))  return PayloadType.String;
            if (t == typeof(bool))    return PayloadType.Bool;
            if (t == typeof(Vector2)) return PayloadType.Vector2;
            if (t == typeof(Vector3)) return PayloadType.Vector3;
            if (t == typeof(Vector4)) return PayloadType.Vector4;
            if (t == typeof(Color))   return PayloadType.Color;
            if (t.IsEnum)             return PayloadType.Enum;
            if (typeof(Object).IsAssignableFrom(t)) return PayloadType.Object;
            return null;
        }

        private static Type NativeParamType(PayloadType p) => p switch
        {
            PayloadType.Int     => typeof(int),
            PayloadType.Float   => typeof(float),
            PayloadType.String  => typeof(string),
            PayloadType.Bool    => typeof(bool),
            PayloadType.Vector2 => typeof(Vector2),
            PayloadType.Vector3 => typeof(Vector3),
            PayloadType.Vector4 => typeof(Vector4),
            PayloadType.Color   => typeof(Color),
            _                   => null
        };

        private static SerializedProperty GetUnityEventProp(SerializedProperty evt, PayloadType p) => p switch
        {
            PayloadType.Int     => evt.FindPropertyRelative("onTriggeredInt"),
            PayloadType.Float   => evt.FindPropertyRelative("onTriggeredFloat"),
            PayloadType.String  => evt.FindPropertyRelative("onTriggeredString"),
            PayloadType.Bool    => evt.FindPropertyRelative("onTriggeredBool"),
            PayloadType.Vector2 => evt.FindPropertyRelative("onTriggeredVector2"),
            PayloadType.Vector3 => evt.FindPropertyRelative("onTriggeredVector3"),
            PayloadType.Vector4 => evt.FindPropertyRelative("onTriggeredVector4"),
            PayloadType.Color   => evt.FindPropertyRelative("onTriggeredColor"),
            PayloadType.Enum    => null,
            PayloadType.Object  => null,
            _                   => evt.FindPropertyRelative("onTriggered"),
        };

        private static bool IsTyped(PayloadType p) => p == PayloadType.Enum || p == PayloadType.Object;

        private static string PayloadLabel(PayloadType p) => p switch
        {
            PayloadType.None => "(Animator)",
            _                => $"({p.ToString().ToLowerInvariant()})",
        };

        private static string ParamLabel(Type paramType, PayloadType payload)
        {
            if (payload == PayloadType.None) return "Animator";
            if (paramType == null) return payload.ToString().ToLowerInvariant();
            if (paramType == typeof(int))    return "int";
            if (paramType == typeof(float))  return "float";
            if (paramType == typeof(string)) return "string";
            if (paramType == typeof(bool))   return "bool";
            if (paramType.IsEnum)            return "enum " + paramType.Name;
            return paramType.Name;
        }

        private static bool TryGetEnumByName(Type enumType, string name, out object value)
        {
            value = null;
            if (enumType == null || !enumType.IsEnum || string.IsNullOrEmpty(name)) return false;
            try
            {
                value = Enum.Parse(enumType, name, false);
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (OverflowException) { return false; }
        }

        private static long EnumToLong(object e)
        {
            Type u = Enum.GetUnderlyingType(e.GetType());
            return u == typeof(ulong) ? unchecked((long)Convert.ToUInt64(e)) : Convert.ToInt64(e);
        }
    }
}
#endif
