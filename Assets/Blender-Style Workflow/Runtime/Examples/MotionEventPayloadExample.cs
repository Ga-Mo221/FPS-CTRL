using UnityEngine;
using UnityEngine.Scripting;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Ví dụ hàm đích cho MotionTimeEventBehaviour với các kiểu tham số mới.
    /// Gắn behaviour này vào 1 state (hoặc dùng MotionCall có sẵn), kéo nó vào ô target của
    /// "Function" trong MotionTimeEventBehaviour → dropdown tự liệt kê các hàm dưới đây,
    /// chọn hàm nào thì ô nhập tự đổi theo (dropdown enum, ô Vector3, ô AudioClip...).
    ///
    /// [Preserve]: hàm enum/Object được gọi qua reflection (theo tên) — cần khi bật
    /// Managed Stripping Level ở bản build IL2CPP.
    /// </summary>
    public class MotionEventPayloadExample : StateMachineBehaviour
    {
        public enum ExampleWeaponState
        {
            Idle,
            Reload,
            Fire,
            Inspect
        }

        // Giá trị nhận gần nhất — để kiểm tra nhanh trong Editor/test.
        public static ExampleWeaponState LastState { get; private set; }
        public static Vector3 LastVector { get; private set; }
        public static AudioClip LastClip { get; private set; }
        public static int CallCount { get; private set; }

        [Preserve]
        public void SetWeaponState(Animator animator, ExampleWeaponState state)
        {
            LastState = state;
            CallCount++;
        }

        public void PushOffset(Animator animator, Vector3 offset)
        {
            LastVector = offset;
            CallCount++;
        }

        [Preserve]
        public void PlayClip(Animator animator, AudioClip clip)
        {
            LastClip = clip;
            CallCount++;
        }
    }
}
