using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// GameObject duy nhất (Edit Mode: ẩn HideAndDontSave; Play Mode: "~ConstraintScheduler" dưới DontDestroyOnLoad) chạy <see cref="ConstraintScheduler.Tick"/> mỗi LateUpdate.
    /// Tự sinh khi constraint đầu tiên đăng ký — KHÔNG cần (và không nên) đặt tay vào scene.
    ///
    /// DefaultExecutionOrder 150: sau Animator + các LateUpdate mặc định (0) và WeaponCollisionResolver (100),
    /// trước ConstraintManager (300) — giữ đúng khung thời gian cũ của TransformConstraint (100) / IK (200).
    /// [ExecuteAlways]: IKConstraint vẫn preview được ở Edit Mode như trước.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(150)]
    [AddComponentMenu("")]
    public sealed class ConstraintSchedulerRunner : MonoBehaviour
    {
        private static ConstraintSchedulerRunner s_Instance;
#if UNITY_EDITOR
        private static bool s_CreateQueued;
#endif

        internal static void ResetStatic()
        {
            // Runner cũ (nếu còn sống) sẽ tự huỷ ở LateUpdate vì không còn là s_Instance
            s_Instance = null;
#if UNITY_EDITOR
            s_CreateQueued = false;
#endif
        }

        internal static void EnsureExists()
        {
            if (s_Instance != null) return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Edit Mode: OnEnable có thể chạy giữa lúc load scene / domain reload — hoãn tạo object
                // sang delayCall cho an toàn, và dùng CreateGameObjectWithHideFlags để không làm dirty scene.
                if (s_CreateQueued) return;
                s_CreateQueued = true;
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    s_CreateQueued = false;
                    if (s_Instance != null || Application.isPlaying) return;
                    if (ConstraintScheduler.RegisteredCount == 0) return;
                    UnityEditor.EditorUtility.CreateGameObjectWithHideFlags(
                        "~ConstraintScheduler", HideFlags.HideAndDontSave, typeof(ConstraintSchedulerRunner));
                };
                return;
            }
#endif
            // Play Mode: KHÔNG dùng DontSave (object DontSave tạo lúc Play không tự huỷ khi thoát Play → leak).
            // Hiện dưới DontDestroyOnLoad giống ~DynamicBoneManager, khoá Inspector để không ai sửa tay.
            var go = new GameObject("~ConstraintScheduler") { hideFlags = HideFlags.NotEditable };
            DontDestroyOnLoad(go);
            go.AddComponent<ConstraintSchedulerRunner>(); // OnEnable bên dưới gán s_Instance
        }

        private void OnEnable()
        {
            if (s_Instance == null) s_Instance = this;
        }

        private void OnDisable()
        {
            if (s_Instance == this) s_Instance = null;
        }

        private void LateUpdate()
        {
            if (s_Instance == null) s_Instance = this;
            if (s_Instance != this)
            {
                // Bản thừa (VD sót lại từ Edit Mode sau khi vào Play không reload domain) → tự huỷ
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            ConstraintScheduler.Tick();
        }
    }
}
