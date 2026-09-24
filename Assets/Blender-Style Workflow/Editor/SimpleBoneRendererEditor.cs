using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;


namespace GaMo.BlenderEditorTools
{
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

        // Stage (Main Stage hoặc Prefab Stage) mà cache trên được quét ra.
        // Khi mở/đóng/chuyển Prefab Mode thì stage đổi -> phải quét lại.
        private static StageHandle _cachedStage;

        static SimpleBoneRendererEditor()
        {
            SceneView.duringSceneGui -= OnGlobalSceneGUI;
            SceneView.duringSceneGui += OnGlobalSceneGUI;

            EditorApplication.hierarchyChanged -= MarkRenderersDirty;
            EditorApplication.hierarchyChanged += MarkRenderersDirty;

            Undo.undoRedoPerformed -= MarkRenderersDirty;
            Undo.undoRedoPerformed += MarkRenderersDirty;
        }

        private static void MarkRenderersDirty()
        {
            _renderersDirty = true;

            // Đổi tên / xóa / reparent xương đều làm hierarchy đổi -> bỏ cache label để
            // tên hiển thị luôn đúng, đồng thời dọn entry của xương đã bị xóa (không rò rỉ).
            _jointLabelCache.Clear();
        }

        private static SimpleBoneRenderer[] GetRenderers()
        {
            // Object.FindObjectsByType không thấy các object nằm trong Prefab Mode (prefab stage
            // là 1 preview scene riêng), nên phải quét theo stage đang mở thay vì quét toàn cục.
            // - Đang ở Scene thường: stage hiện tại = Main Stage (gồm mọi scene đang mở).
            // - Đang ở Prefab Mode: stage hiện tại = prefab đang chỉnh sửa.
            StageHandle stage = StageUtility.GetCurrentStageHandle();

            if (_renderersDirty || _cachedRenderers == null || stage != _cachedStage)
            {
                if (stage != _cachedStage)
                    _jointLabelCache.Clear(); // Transform của stage cũ không còn dùng nữa

                _cachedRenderers = stage.FindComponentsOfType<SimpleBoneRenderer>();
                _cachedStage = stage;
                _renderersDirty = false;
            }

            return _cachedRenderers;
        }

        // ────────────────────────────────────────────────────────────────────────
        // UI Inspector theo phong cách tab (giống TransformConstraintEditor)
        // ────────────────────────────────────────────────────────────────────────
        private int currentTab = 0;
        private readonly string[] tabTitles = { "Setup", "Bone & Joint Settings" };

        // Properties - Setup
        private SerializedProperty rootBoneProp;
        private SerializedProperty bonesProp;
        private SerializedProperty showProp;

        // Properties - Bone Settings
        private SerializedProperty boneColorProp;
        private SerializedProperty boneSizeProp;

        // Properties - Joint Settings
        private SerializedProperty drawJointsProp;
        private SerializedProperty jointColorProp;
        private SerializedProperty jointFontSizeProp;

        private void OnEnable()
        {
            // Guard: nếu target null (object vừa bị xóa/Undo, hoặc Editor được tái tạo
            // ngay lúc recompile/prefab mode...), việc chạm vào serializedObject sẽ
            // ném SerializedObjectNotCreatableException. Thoát sớm để tránh crash Inspector.
            if (target == null)
                return;

            rootBoneProp = serializedObject.FindProperty("rootBone");
            bonesProp = serializedObject.FindProperty("bones");
            showProp = serializedObject.FindProperty("show");

            boneColorProp = serializedObject.FindProperty("boneColor");
            boneSizeProp = serializedObject.FindProperty("boneSize");

            drawJointsProp = serializedObject.FindProperty("drawJoints");
            jointColorProp = serializedObject.FindProperty("jointColor");
            jointFontSizeProp = serializedObject.FindProperty("jointFontSize");

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

        // ── Wrapped Toolbar (giống TransformConstraintEditor) ───────────────────────
        /// <summary>
        /// Vẽ toolbar giống GUILayout.Toolbar nhưng tự xuống hàng khi
        /// inspector quá hẹp để chứa tất cả tab trên một hàng.
        /// </summary>
        private int DrawWrappedToolbar(int selected, string[] tabs)
        {
            float viewWidth   = EditorGUIUtility.currentViewWidth - 20f;
            float tabMinWidth = 100f;
            float tabPadding  = 4f;

            int tabsPerRow = Mathf.Max(1, Mathf.FloorToInt(
                (viewWidth + tabPadding) / (tabMinWidth + tabPadding)));

            int tabCount = tabs.Length;
            int rows     = Mathf.CeilToInt((float)tabCount / tabsPerRow);

            for (int row = 0; row < rows; row++)
            {
                int startIdx = row * tabsPerRow;
                int endIdx   = Mathf.Min(startIdx + tabsPerRow, tabCount);
                int count    = endIdx - startIdx;

                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = startIdx; i < endIdx; i++)
                    {
                        float tabWidth = (viewWidth - (count - 1) * tabPadding) / count;

                        bool isSelected = (selected == i);
                        bool clicked    = GUILayout.Toggle(
                            isSelected, tabs[i],
                            EditorStyles.toolbarButton,
                            GUILayout.Width(tabWidth), GUILayout.Height(22f));

                        if (clicked && !isSelected)
                            selected = i;

                        if (i < endIdx - 1)
                            GUILayout.Space(tabPadding);
                    }
                }
            }

            return selected;
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

            // Nút Show / Hide luôn nằm trên cùng (ngoài các tab) để bật/tắt tức thì.
            // Nhấn = Show (đang hiển thị), nhả = Hide (đang ẩn).
            EditorGUI.BeginChangeCheck();
            bool newShow = GUILayout.Toggle(showProp.boolValue,
                showProp.boolValue ? Content.Show : Content.Hide, "Button", GUILayout.Height(22));
            if (EditorGUI.EndChangeCheck())
            {
                showProp.boolValue = newShow;
                serializedObject.ApplyModifiedProperties();
                SceneView.RepaintAll(); // cập nhật Scene view ngay, không chờ event kế tiếp
            }

            EditorGUILayout.Space(3);
            currentTab = DrawWrappedToolbar(currentTab, tabTitles);
            EditorGUILayout.Space(10);

            switch (currentTab)
            {
                case 0:
                    DrawSetupTab(boneRenderer);
                    break;
                case 1:
                    DrawBoneAndJointSettingsTab();
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

        private void DrawBoneAndJointSettingsTab()
        {
            // ── BONE SETTINGS ────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Bone Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(boneColorProp, Content.BoneColor);
            EditorGUILayout.PropertyField(boneSizeProp, Content.BoneSize);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── JOINT SETTINGS ───────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Joint Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(drawJointsProp, Content.DrawJoints);

            EditorGUI.BeginDisabledGroup(!drawJointsProp.boolValue);
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(jointColorProp, Content.JointColor);
                EditorGUILayout.PropertyField(jointFontSizeProp, Content.FontSize);
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
            public static readonly GUIContent RootBone = new GUIContent("Root Bone");
            public static readonly GUIContent LoadBones = new GUIContent("Load All Child Bones");
            public static readonly GUIContent Show = new GUIContent("Show",
                "Đang hiển thị khung xương trong Scene view. Nhấn để ẩn.");
            public static readonly GUIContent Hide = new GUIContent("Hide",
                "Đang ẩn khung xương trong Scene view. Nhấn để hiện.");
            public static readonly GUIContent Bones = new GUIContent("Bones");
            public static readonly GUIContent BoneColor = new GUIContent("Bone Color");
            public static readonly GUIContent BoneSize = new GUIContent("Bone Size");
            public static readonly GUIContent DrawJoints = new GUIContent("Draw Joints");
            public static readonly GUIContent JointColor = new GUIContent("Joint Color");
            public static readonly GUIContent FontSize = new GUIContent("Font Size",
                "Cỡ chữ của tên xương hiển thị trong Scene view.");
        }

        // --- Dung sai xác định xương con có "connect" với xương cha hay không ---
        // Xương con được coi là nối (connected) khi gốc của nó nằm trên trục Y của xương cha
        // (phía trước, theo chiều +Y). Lệch trục quá dung sai -> xương con chỉ là "offset",
        // không nối với cha. Dung sai = max(tuyệt đối, tỉ lệ * chiều dài đoạn dọc trục Y) để
        // chịu được sai số float khi import từ Blender/FBX, mà vẫn đúng với xương nhỏ hay lớn.
        private const float ConnectMinAlong = 0.0001f;      // dưới mức này coi như trùng gốc
        private const float ConnectAbsTolerance = 0.0001f;  // sai số lệch trục tối thiểu (world unit)
        private const float ConnectRelTolerance = 0.01f;    // 1% chiều dài (~0.6°)

        // --- Danh sách khớp cần vẽ dạng GUI Button (thay cho Handles.Button hình cầu) ---
        // Handles.Button là handle 3D, hit-test của nó tranh chấp trực tiếp với gizmo
        // Move/Rotate/Scale mặc định của Unity khi cùng nằm ở 1 vị trí trong màn hình,
        // khiến gizmo bị "nuốt" click. Chuyển sang GUI.Button (vẽ ở không gian màn hình,
        // trong Handles.BeginGUI/EndGUI) để tách biệt hoàn toàn với hit-test 3D của gizmo.
        private struct JointGuiInfo
        {
            public Vector3 worldPos;
            public Transform bone;
            public Color jointColor;
            public int fontSize;
        }

        private static readonly List<JointGuiInfo> _reusableJointInfos = new List<JointGuiInfo>(32);

        // Cache theo xương: GUIContent + kích thước chữ đã đo (GUIStyle.CalcSize khá tốn),
        // chỉ đo lại khi font size đổi. Không gọi bone.name mỗi frame vì mỗi lần gọi Unity
        // cấp phát 1 string mới; thay vào đó cache bị xóa trong MarkRenderersDirty khi đổi tên.
        private sealed class LabelEntry
        {
            public GUIContent content;
            public Vector2 size;
            public int fontSize = -1; // -1 = chưa đo
        }

        private static readonly Dictionary<Transform, LabelEntry> _jointLabelCache =
            new Dictionary<Transform, LabelEntry>(32);

        private static LabelEntry GetJointLabel(Transform bone)
        {
            if (!_jointLabelCache.TryGetValue(bone, out LabelEntry entry))
            {
                entry = new LabelEntry { content = new GUIContent(bone.name) };
                _jointLabelCache[bone] = entry;
            }

            return entry;
        }

        // Handles.Draw* chỉ có tác dụng ở event Repaint. Các event khác (Layout, MouseMove,
        // MouseDown...) vẫn cần gom danh sách label cho GUI.Button nhưng không cần tính hình
        // học để vẽ thân xương -> bỏ qua phần đó ở các event này.
        private static bool _isRepaint;

        private static void OnGlobalSceneGUI(SceneView sceneView)
        {
            SimpleBoneRenderer[] renderers = GetRenderers();
            if (renderers == null || renderers.Length == 0) return;

            Matrix4x4 originalMatrix = Handles.matrix;
            _isRepaint = Event.current.type == EventType.Repaint;

            // Cache một lần bên ngoài vòng lặp kép; truy cập Selection.activeTransform
            // mỗi xương mỗi frame tốn không đáng.
            Transform selectedTransform = Selection.activeTransform;

            // Gom danh sách khớp cần vẽ button ở pass GUI riêng, thay vì vẽ Handles.Button
            // ngay trong vòng lặp 3D — xem giải thích ở khai báo _reusableJointInfos.
            _reusableJointInfos.Clear();

            // true nếu đối tượng đang chọn là 1 bone thuộc bất kỳ SimpleBoneRenderer nào.
            bool selectionIsBone = false;

            foreach (var boneRenderer in renderers)
            {
                if (boneRenderer == null || !boneRenderer.enabled || !boneRenderer.gameObject.activeInHierarchy ||
                    boneRenderer.bones == null)
                    continue;

                // Nút Show/Hide trong Inspector: tắt là bỏ qua hoàn toàn (cả Edit lẫn Play),
                // gồm cả thân xương lẫn label tên.
                if (!boneRenderer.show)
                    continue;

                HashSet<Transform> boneSet = boneRenderer.BoneSet;
                List<Transform> bones = boneRenderer.bones;

                if (selectedTransform != null && boneSet.Contains(selectedTransform))
                    selectionIsBone = true;

                for (int i = 0; i < bones.Count; i++)
                {
                    Transform bone = bones[i];
                    if (bone == null) continue;

                    // Mỗi xương là MỘT đoạn đi dọc trục Y cục bộ của chính nó (kiểu Blender).
                    // Tail = gốc của xương con gần nhất mà nằm đúng trên trục Y đó (connected).
                    // Xương con lệch trục chỉ là offset: KHÔNG kéo xương cha tới nó; xương cha
                    // dùng chiều dài mặc định, còn xương con tự vẽ đoạn của riêng nó.
                    Vector3 start = bone.position;
                    Vector3 axis = bone.up;

                    bool hasConnectedChild = false;
                    float nearestAlong = float.MaxValue;
                    Vector3 connectedTail = start;

                    // Không dùng foreach trên Transform: nó cấp phát 1 enumerator mỗi lần gọi.
                    int childCount = bone.childCount;
                    for (int c = 0; c < childCount; c++)
                    {
                        Transform child = bone.GetChild(c);
                        if (!boneSet.Contains(child)) continue;

                        Vector3 toChild = child.position - start;
                        float along = Vector3.Dot(toChild, axis);
                        if (along <= ConnectMinAlong) continue; // nằm sau/trùng gốc -> không nối

                        Vector3 perpendicular = toChild - axis * along;
                        float tolerance = Mathf.Max(ConnectAbsTolerance, along * ConnectRelTolerance);
                        if (perpendicular.sqrMagnitude > tolerance * tolerance) continue; // lệch trục -> offset

                        if (along < nearestAlong)
                        {
                            nearestAlong = along;
                            connectedTail = child.position;
                            hasConnectedChild = true;
                        }
                    }

                    Vector3 end = hasConnectedChild
                        ? connectedTail
                        : start + axis * (boneRenderer.boneSize * 2.5f);

                    ProcessBone(boneRenderer, bone, start, end, selectedTransform);
                }
            }

            Handles.matrix = originalMatrix;

            // Khi đang chọn 1 bone: chỉ vẽ label của CHÍNH bone đó, ẩn tất cả label còn lại.
            // Bỏ chọn bone (hoặc chọn đối tượng không phải bone) thì onlyBone = null -> hiện lại tất cả.
            Transform onlyBone = selectionIsBone ? selectedTransform : null;

            // Pass GUI riêng, tách khỏi Handles 3D phía trên — không tranh chấp hit-test
            // với gizmo Move/Rotate/Scale mặc định.
            DrawJointGuiButtons(sceneView, onlyBone);
        }

        private static void ProcessBone(SimpleBoneRenderer renderer, Transform bone,
            Vector3 start, Vector3 end, Transform selectedTransform)
        {
            if (_isRepaint)
                Handles.color = (selectedTransform == bone) ? Color.green : renderer.boneColor;

            DrawBoneGraphics(start, end, renderer, bone);
        }

        private static void DrawBoneGraphics(Vector3 start, Vector3 end, SimpleBoneRenderer renderer,
            Transform bone)
        {
            Vector3 direction = end - start;
            float distance = direction.magnitude;
            if (distance < 0.0001f) return;

            Color originalColor = default;

            // --- 1. VẼ THÂN XƯƠNG (chỉ ở Repaint) ---
            if (_isRepaint)
            {
                float size = renderer.boneSize;
                originalColor = Handles.color;

                Handles.DrawLine(start, end);

                Quaternion lookRotation = Quaternion.LookRotation(direction);
                float scaledSize = size * distance;
                Handles.DrawWireDisc(start, direction, scaledSize);

                Vector3 right = lookRotation * Vector3.right * scaledSize;
                Vector3 up = lookRotation * Vector3.up * scaledSize;

                Handles.DrawLine(start + right, end);
                Handles.DrawLine(start - right, end);
                Handles.DrawLine(start + up, end);
                Handles.DrawLine(start - up, end);
            }

            // --- 2. GOM KHỚP ĐỂ VẼ BUTTON Ở PASS GUI RIÊNG ---
            // Không dùng Handles.Button/SphereHandleCap nữa: chỉ enqueue vị trí + xương,
            // việc vẽ và bắt click thực sự nằm ở DrawJointGuiButtons (không gian màn hình),
            // nên sẽ không còn giành hit-test với gizmo Move/Rotate/Scale mặc định.
            if (renderer.drawJoints)
            {
                // Label nằm ở điểm GIỮA thân xương (mid), không phải đầu mút (tip).
                Vector3 mid = (start + end) * 0.5f;
                _reusableJointInfos.Add(new JointGuiInfo
                {
                    worldPos = mid,
                    bone = bone,
                    jointColor = renderer.jointColor,
                    fontSize = renderer.jointFontSize
                });
            }

            if (_isRepaint)
                Handles.color = originalColor;
        }

        // Khởi tạo trễ (không phải static field initializer): EditorStyles chỉ an toàn
        // để truy cập bên trong 1 lệnh gọi kiểu OnGUI, không phải lúc static constructor
        // của class chạy (có thể xảy ra sớm hơn do [InitializeOnLoad]).
        // Style này KHÔNG có background/border — chỉ chữ, để khớp không chiếm diện tích
        // hình khối trên màn hình như button thường.
        private static GUIStyle _jointLabelStyle;

        private static void DrawJointGuiButtons(SceneView sceneView, Transform onlyBone)
        {
            if (_reusableJointInfos.Count == 0) return;

            Handles.BeginGUI();

            if (_jointLabelStyle == null)
            {
                _jointLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(4, 4, 1, 1)
                };
                _jointLabelStyle.normal.background = null;
                _jointLabelStyle.hover.background = null;
                _jointLabelStyle.active.background = null;
                _jointLabelStyle.focused.background = null;
                _jointLabelStyle.border = new RectOffset(0, 0, 0, 0);
            }

            // Lấy camera 1 lần cho cả vòng lặp thay vì mỗi label.
            Camera cam = sceneView.camera;

            for (int i = 0; i < _reusableJointInfos.Count; i++)
            {
                JointGuiInfo info = _reusableJointInfos[i];
                if (info.bone == null) continue;

                // Có bone đang được chọn -> chỉ giữ lại label của bone đó.
                if (onlyBone != null && info.bone != onlyBone) continue;

                Vector2 guiPoint = HandleUtility.WorldToGUIPoint(info.worldPos);

                // Khớp ở phía sau camera thì bỏ qua, tránh vẽ chữ "ma" ở góc màn hình.
                if (cam != null)
                {
                    Vector3 viewPos = cam.WorldToViewportPoint(info.worldPos);
                    if (viewPos.z < 0f) continue;
                }

                LabelEntry label = GetJointLabel(info.bone);

                // Màu chữ theo Joint Color; mutate trực tiếp style dùng chung vì vẽ tuần
                // tự trong cùng 1 thread, không có tranh chấp.
                _jointLabelStyle.normal.textColor = info.jointColor;
                _jointLabelStyle.hover.textColor = info.jointColor;
                _jointLabelStyle.fontSize = info.fontSize; // set trước CalcSize để rect ôm đúng chữ

                // Chỉ đo lại kích thước chữ khi lần đầu hoặc font size đổi.
                if (label.fontSize != info.fontSize)
                {
                    label.size = _jointLabelStyle.CalcSize(label.content);
                    label.fontSize = info.fontSize;
                }

                Vector2 size = label.size;
                Rect rect = new Rect(guiPoint.x - size.x * 0.5f, guiPoint.y - size.y * 0.5f, size.x, size.y);

                // GUI.Button với style không nền vẫn bắt click bình thường — chỉ khác là
                // không vẽ khối nền/border, nên trực quan chỉ thấy đúng dòng chữ tên xương.
                if (GUI.Button(rect, label.content, _jointLabelStyle))
                {
                    Selection.activeObject = info.bone;
                }
            }

            Handles.EndGUI();
        }
    }
}