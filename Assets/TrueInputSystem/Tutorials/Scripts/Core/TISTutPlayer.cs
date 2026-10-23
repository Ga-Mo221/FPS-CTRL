// ============================================================
//  TrueInputSystem Tutorials — TISTutPlayer.cs
//
//  Nhân vật mẫu của sân chơi — đọc input HOÀN TOÀN qua TISInputBridge:
//    MoveDirection        → đi theo hướng camera
//    IsSkillHeld("Run")   → chạy
//    IsSkillDown("Jump")  → nhảy (tuỳ chọn qua TISInputBuffer)
//  Bài học bật / tắt từng khả năng (CanMove, CanJump, CanRun...).
// ============================================================

using System;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Player")]
    public class TISTutPlayer : MonoBehaviour
    {
        [Header("Di chuyển")]
        public float walkSpeed  = 4.5f;
        public float runSpeed   = 8.5f;
        public float jumpHeight = 1.6f;
        public float gravity    = -24f;
        public float turnSpeed  = 14f;
        [Tooltip("Bán kính sân chơi — nhân vật không đi ra ngoài.")]
        public float boundsRadius = 17f;

        [Header("Hình")]
        [SerializeField] Transform _visual;

        [Header("Khả năng (bài học bật / tắt)")]
        public bool CanMove = true;
        public bool CanJump;
        public bool CanRun;
        public bool UseJumpBuffer;

        /// <summary>Hệ số tốc độ thêm (vd. chế độ lái xe).</summary>
        public float SpeedMultiplier = 1f;

        public readonly TISInputBuffer JumpBuffer = new TISInputBuffer("Jump", 0.18f);

        /// <summary>true = nhảy nhờ buffer (bấm trước khi chạm đất).</summary>
        public event Action<bool> OnJumped;
        public event Action       OnLanded;

        /// <summary>Hình hiển thị của nhân vật (capsule) — bài lái xe ẩn đi khi lên xe.</summary>
        public Transform Visual => _visual;

        public bool    IsGrounded  { get; private set; }
        public bool    IsRunning   { get; private set; }
        public Vector3 Velocity    => _vel;
        public float   AirTime     { get; private set; }
        public float   Travelled   { get; private set; }

        CharacterController _cc;
        Vector3 _vel;
        float   _squash;
        Vector3 _visualBase = Vector3.one;

        TISInputBridge TIS => TISInputBridge.Instance;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (_visual != null) _visualBase = _visual.localScale;
        }

        void Update()
        {
            var tis = TIS;
            float dt = Time.deltaTime;

            // ── Hướng đi theo camera ─────────────────────────
            Vector2 mv = CanMove && tis != null ? tis.MoveDirection : Vector2.zero;
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 dir = fwd * mv.y + right * mv.x;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            IsRunning = CanRun && tis != null && tis.IsSkillHeld("Run") && dir.sqrMagnitude > 0.01f;
            float speed = (IsRunning ? runSpeed : walkSpeed) * SpeedMultiplier;
            Vector3 horiz = dir * speed;

            // ── Nhảy ──────────────────────────────────────────
            bool wasGrounded = IsGrounded;
            IsGrounded = _cc.isGrounded;
            if (IsGrounded && !wasGrounded) { _squash = 1f; OnLanded?.Invoke(); }
            AirTime = IsGrounded ? 0f : AirTime + dt;

            bool pressed = CanJump && tis != null && tis.IsSkillDown("Jump");
            if (UseJumpBuffer)
            {
                JumpBuffer.Tick(pressed);
                if (IsGrounded && JumpBuffer.Consume()) Jump(buffered: !pressed);
            }
            else if (IsGrounded && pressed)
            {
                Jump(buffered: false);
            }

            if (IsGrounded && _vel.y < 0f) _vel.y = -2f;
            _vel.y += gravity * dt;
            _vel.x = horiz.x;
            _vel.z = horiz.z;

            Vector3 before = transform.position;
            _cc.Move(_vel * dt);
            // Giữ trong sân chơi: chặn ở rìa, rơi khỏi mép thì về chỗ xuất phát
            Vector3 pos = transform.position;
            Vector2 flat = new Vector2(pos.x, pos.z);
            if (flat.magnitude > boundsRadius)
            {
                flat = flat.normalized * boundsRadius;
                Teleport(new Vector3(flat.x, pos.y, flat.y), transform.eulerAngles.y);
            }
            if (pos.y < -5f) Teleport(TISTutPlayground.PlayerStart);

            Vector3 moved = transform.position - before;
            moved.y = 0f;
            Travelled += moved.magnitude;

            // ── Quay mặt theo hướng đi ───────────────────────
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-turnSpeed * dt));

            // ── Hiệu ứng nhún khi tiếp đất ───────────────────
            if (_visual != null)
            {
                _squash = Mathf.MoveTowards(_squash, 0f, dt * 5f);
                float s = Mathf.Sin(_squash * Mathf.PI) * 0.18f;
                _visual.localScale = Vector3.Scale(_visualBase, new Vector3(1f + s, 1f - s, 1f + s));
            }
        }

        void Jump(bool buffered)
        {
            _vel.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            IsGrounded = false;
            OnJumped?.Invoke(buffered);
        }

        /// <summary>Đặt lại vị trí (tắt CharacterController tạm thời).</summary>
        public void Teleport(Vector3 pos, float yawDeg = 0f)
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawDeg, 0f));
            _cc.enabled = true;
            _vel = Vector3.zero;
        }

        /// <summary>Đặt lại mọi khả năng về mặc định (gọi khi đổi bài).</summary>
        public void ResetAbilities()
        {
            CanMove = true;
            CanJump = CanRun = UseJumpBuffer = false;
            SpeedMultiplier = 1f;
            JumpBuffer.Clear();
            OnJumped = null;
            OnLanded = null;
            Travelled = 0f;
        }
    }
}
