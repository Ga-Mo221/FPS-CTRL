#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Khối "Execution Order" dùng chung cho Inspector của mọi <see cref="ConstraintBehaviour"/>:
    /// field Execution Order + vị trí thực tế trong <see cref="ConstraintScheduler"/> + danh sách thứ tự toàn scene.
    /// </summary>
    public static class ConstraintOrderGUI
    {
        private static readonly GUIContent s_GcOrder = new GUIContent("Execution Order",
            "Thứ tự thực thi do bạn chỉ định (nhỏ chạy trước, mặc định 0).\n" +
            "• Phụ thuộc luôn được ưu tiên: constraint đọc Transform do constraint khác ghi sẽ luôn chạy sau nó.\n" +
            "• Giá trị này chỉ quyết định thứ tự giữa các constraint KHÔNG phụ thuộc nhau, hoặc để phá vòng lặp phụ thuộc.\n" +
            "• Bằng nhau → theo loại (DampedTrackChain → DampedTrack → Transform → IK), rồi độ sâu hierarchy (cha trước con).");

        private static readonly GUIContent s_GcList = new GUIContent("Thứ tự thực thi toàn scene",
            "Danh sách đã sắp xếp mà ConstraintScheduler đang chạy mỗi LateUpdate. Click 1 dòng để ping object.");

        private static bool s_ShowList;
        private static GUIStyle s_RowStyle;
        private static GUIStyle s_RowSelfStyle;

        public static void Draw(SerializedObject so, ConstraintBehaviour script)
        {
            SerializedProperty orderProp = so.FindProperty("executionOrder");
            if (orderProp == null || script == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(orderProp, s_GcOrder);
            GUILayout.Label(GetStatus(script), EditorStyles.miniLabel, GUILayout.Width(110));
            EditorGUILayout.EndHorizontal();

            if (script.IsInDependencyCycle)
                EditorGUILayout.HelpBox(
                    "Constraint này nằm trong vòng lặp phụ thuộc (A đọc pose B ghi và ngược lại) — scheduler đã phá vòng theo " +
                    "Execution Order, một phía sẽ đọc pose trễ 1 frame.", MessageType.Warning);

            if (script.ExternallyDriven)
                EditorGUILayout.HelpBox(
                    "ExternallyDriven = true: scheduler bỏ qua, một script khác tự gọi Solve()/Evaluate().", MessageType.Info);

            s_ShowList = EditorGUILayout.Foldout(s_ShowList, s_GcList, true);
            if (s_ShowList) DrawExecutionList(script);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        private static string GetStatus(ConstraintBehaviour script)
        {
            if (!script.IsRegisteredWithScheduler)
                return Application.isPlaying || !script.isActiveAndEnabled ? "Không chạy (đang tắt)" : "Chỉ chạy ở Play Mode";
            if (script.ResolvedExecutionIndex < 0) return "Đang chờ sắp xếp";
            return $"Thực tế: #{script.ResolvedExecutionIndex + 1} / {ConstraintScheduler.RegisteredCount}";
        }

        private static void DrawExecutionList(ConstraintBehaviour self)
        {
            IReadOnlyList<ConstraintBehaviour> list = ConstraintScheduler.EditorGetExecutionList();
            if (list.Count == 0)
            {
                EditorGUILayout.LabelField("(Chưa có constraint nào đăng ký — ở Edit Mode chỉ IKConstraint chạy)", EditorStyles.miniLabel);
                return;
            }

            if (s_RowStyle == null)
            {
                s_RowStyle = new GUIStyle(EditorStyles.miniLabel) { richText = true };
                s_RowSelfStyle = new GUIStyle(EditorStyles.miniBoldLabel) { richText = true };
            }

            for (int i = 0; i < list.Count; i++)
            {
                ConstraintBehaviour c = list[i];
                if (c == null) continue;

                string flags = (c.ExternallyDriven ? " [External]" : "") + (c.IsInDependencyCycle ? " [Cycle]" : "");
                string text = $"#{i + 1}  {c.GetType().Name}  •  {c.name}  (order {c.ExecutionOrder}){flags}";
                if (GUILayout.Button(text, c == self ? s_RowSelfStyle : s_RowStyle))
                    EditorGUIUtility.PingObject(c);
            }
        }
    }
}
#endif
