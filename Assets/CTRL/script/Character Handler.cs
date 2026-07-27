// using System.Collections;
// using TrueInputSystem;
// using UnityEngine;
//
// [RequireComponent(typeof(CharacterController))]
// public class CharacterHandler : MonoBehaviour
// {
//     private CharComponentManager _componentManager;
//     private PlayerMotionController _anim;
//     private TISInputBridge _input;
//     private CharacterController _characterController;
//     
//     [Header("Move")]
//     [SerializeField] private Vector2 _move;
//     private Vector2 _inputMove;
//     private Vector2 _targetMove;
//     private Vector2 _moveVelocity;
//     
//     private TurnInPlace _turnInPlace;
//     
//     [SerializeField]
//     [Tooltip("Thời gian để đạt tới target. Giá trị nhỏ = phản hồi nhanh hơn.")]
//     private float _moveSmoothTime = 0.08f;
//     
//     [Header("Movement Speeds (m/s)")]
//     [SerializeField] private float _tacSprintSpeed = 7.4f; // Tactical Sprint
//     [SerializeField] private float _sprintSpeed = 5.6f;    // Sprint
//     [SerializeField] private float _walkSpeed = 4.5f;      // Walk / Jog
//     [SerializeField] private float _adsWalkSpeed = 2.1f;   // ADS Walk
//     [SerializeField] private float _crouchSpeed = 2.3f;    // Crouch
//     [SerializeField] private float _slideInitialSpeed = 8.5f; // Tốc độ khởi đầu khi trượt
//     
//     [Header("Character Controller Heights")]
//     [SerializeField] private float _standingHeight = 1.8f;  // Chiều cao khi đứng
//     [SerializeField] private float _crouchHeight = 1.0f;    // Chiều cao khi ngồi / trượt
//     [SerializeField] private Vector3 _standingCenter = new Vector3(0, 0.9f, 0);
//     [SerializeField] private Vector3 _crouchCenter = new Vector3(0, 0.5f, 0);
//     
//     [Header("Physics & Gravity")]
//     [SerializeField] private float _gravity = -19.62f;     // Trọng lực
//     [SerializeField] private float _jumpHeight = 1.2f;      // Độ cao nhảy
//     [SerializeField] private float _jumpDelay = 0.25f;      // Thời gian delay trước khi bứt khỏi mặt đất
//     private Vector3 _velocity;                              // Vận tốc rơi / nhảy
//     private bool _isJumpingPending = false;                 // Cờ kiểm tra đang trong quá trình chờ nhảy
//     
//     [Header("State")]
//     [Range(0.4f, 1.5f)]
//     [SerializeField] private float _slideDuration = 0.5f;
//     [SerializeField] private int GrenadeID;
//     [SerializeField] private CharacterState _state;
//     [SerializeField] private ConstraintManager _constraintManager;
//     [SerializeField] private FreeCameraController _freeCamera;
//     
//     // Biến hỗ trợ Slide
//     private Vector3 _slideDirection;
//     private float _slideTimer;
//     private Coroutine _resetSlide;
//     public AudioSource _sound;
//     
//     private void Awake()
//     {
//         _input = TISInputBridge.Instance;
//         _componentManager = GetComponent<CharComponentManager>();
//         _anim = _componentManager.PlayerMotionController;
//         _constraintManager = _componentManager.ConstraintManager;
//         _characterController = GetComponent<CharacterController>();
//         _turnInPlace = _componentManager.TurnInPlace;
//         _freeCamera = _componentManager.FreeCameraController;
//         
//         if (_freeCamera != null)
//             _state.FreeCamera = _freeCamera;
//     
//         if (_constraintManager != null) 
//             _state.C_manager = _constraintManager;
//     
//         if (_sound != null) _state.Source = _sound;
//     }
//     
//     private void Update()
//     {
//         ReloadHandler();
//         MoveHandler();
//         SprintHandler();
//         ADSHandler();
//         ShootHandler();
//         CrouchHandler();
//         SwitchWeaponHandler();
//         ThrowHandler();
//         JumpHandler();
//         ModeSwitchHandler();
//     
//         // Xử lý di chuyển thực tế bằng CharacterController
//         ApplyMovement();
//     
//         _anim.Move(_move);
//         
//     
//         // Kích hoạt Coroutine reset slide khi bắt đầu Slide
//         if (_state.IsSlide && _resetSlide == null)
//         {
//             StartSlidePhysics();
//             _resetSlide = StartCoroutine(ResetSlide());
//         }
//     }
//     
//     /// <summary>
//     /// Khởi tạo hướng trượt và thời gian trượt khi bắt đầu Slide
//     /// </summary>
//     private void StartSlidePhysics()
//     {
//         _slideTimer = _slideDuration;
//         
//         // Trượt theo hướng nhân vật đang nhìn/di chuyển
//         Vector3 inputDir = new Vector3(_input.MoveDirection.x, 0f, _input.MoveDirection.y).normalized;
//         if (inputDir.sqrMagnitude > 0.01f)
//             _slideDirection = transform.TransformDirection(inputDir);
//         else
//             _slideDirection = transform.forward;
//     }
//     
//     private IEnumerator ResetSlide()
//     {
//         yield return new WaitForSeconds(_slideDuration);
//         _state.StopSlide();
//         _resetSlide = null;
//     }
//     
//     private void MoveHandler()
//     {
//         _targetMove = Vector2.zero;
//     
//         // ===== X =====
//         _inputMove = _input.MoveDirection;
//         if (_inputMove.x != 0)
//         {
//             if (_state.IsADS || _state.IsThrow)
//                 _targetMove.x = 0.2f * Mathf.Sign(_inputMove.x);
//             else if (_state.IsRun && !_state.IsCrouch)
//                 _targetMove.x = 0.6f * Mathf.Sign(_inputMove.x);
//             else
//                 _targetMove.x = 0.4f * Mathf.Sign(_inputMove.x);
//     
//             if (_state.IsSprint)
//             {
//                 _state.StartRun();
//             }
//         }
//     
//         // ===== Y =====
//         if (_inputMove.y != 0)
//         {
//             if (_state.IsADS || _state.IsThrow)
//                 _targetMove.y = 0.2f * Mathf.Sign(_inputMove.y);
//             else if (_state.IsRun && !_state.IsCrouch)
//                 _targetMove.y = 0.6f * Mathf.Sign(_inputMove.y);
//             else if (_state.IsSprint)
//                 _targetMove.y = 1.0f * Mathf.Sign(_inputMove.y);
//             else
//                 _targetMove.y = 0.4f * Mathf.Sign(_inputMove.y);
//         }
//     
//         // ===== Smooth =====
//         _move = Vector2.SmoothDamp(
//             _move,
//             _targetMove,
//             ref _moveVelocity,
//             _moveSmoothTime);
//         
//         // Tránh giá trị rất nhỏ gây rung Animator
//         if (_move.sqrMagnitude < 0.0001f && _characterController.isGrounded)
//         {
//             _move = Vector2.zero;
//             _turnInPlace.TurnOn();
//         }
//         else _turnInPlace.TurnOff();
//         
//         
//     
//         // Khi không còn input thì reset trạng thái
//         if (_targetMove == Vector2.zero && !_state.IsSlide)
//         {
//             _state.StopMovement();
//         }
//     
//         if (_anim.ActiveWeponCTRL() != null)
//         {
//             _anim.ActiveWeponCTRL().SetMoveDirection(_input.MoveDirection);
//         }
//     }
//     
//     /// <summary>
//     /// Áp dụng di chuyển thực tế vào CharacterController bao gồm Trọng lực và Slide
//     /// </summary>
//     private void ApplyMovement()
//     {
//         // 1. Kiểm tra chạm đất
//         bool isGrounded = _characterController.isGrounded;
//         _anim.IsGrouded(isGrounded);
//     
//         if (isGrounded && _velocity.y < 0)
//         {
//             _velocity.y = -2f; // Giữ nhân vật dính sát mặt đất
//         }
//     
//         // 2. Cập nhật Kích thước CharacterController (Hitbox) theo tư thế
//         AdjustColliderHeight();
//     
//         // 3. Xử lý di chuyển riêng cho Slide vs Di chuyển thường
//         if (_state.IsSlide)
//         {
//             _slideTimer -= Time.deltaTime;
//             // Tốc độ giảm dần theo thời gian trượt (từ _slideInitialSpeed xuống _crouchSpeed)
//             float slideProgress = Mathf.Clamp01(_slideTimer / _slideDuration);
//             float currentSlideSpeed = Mathf.Lerp(_crouchSpeed, _slideInitialSpeed, slideProgress);
//     
//             _characterController.Move(_slideDirection * currentSlideSpeed * Time.deltaTime);
//         }
//         else
//         {
//             // Di chuyển mặt phẳng Z, X tiêu chuẩn
//             Vector3 moveInput = new Vector3(_input.MoveDirection.x, 0f, _input.MoveDirection.y).normalized;
//             Vector3 moveDirection = transform.TransformDirection(moveInput);
//     
//             float currentSpeed = GetCurrentSpeed();
//             _characterController.Move(moveDirection * currentSpeed * Time.deltaTime);
//         }
//     
//         // 4. Áp dụng Trọng lực trục Y
//         _velocity.y += _gravity * Time.deltaTime;
//         _characterController.Move(_velocity * Time.deltaTime);
//     }
//     
//     /// <summary>
//     /// Điều chỉnh Height & Center của CharacterController khi Crouch/Slide
//     /// </summary>
//     private void AdjustColliderHeight()
//     {
//         bool isLowProfile = _state.IsCrouch || _state.IsSlide;
//         float targetHeight = isLowProfile ? _crouchHeight : _standingHeight;
//         Vector3 targetCenter = isLowProfile ? _crouchCenter : _standingCenter;
//     
//         _characterController.height = Mathf.Lerp(_characterController.height, targetHeight, Time.deltaTime * 10f);
//         _characterController.center = Vector3.Lerp(_characterController.center, targetCenter, Time.deltaTime * 10f);
//     }
//     
//     private float GetCurrentSpeed()
//     {
//         if (_state.IsSprint) return _tacSprintSpeed;
//         if (_state.IsRun) return _sprintSpeed;
//         if (_state.IsCrouch) return _crouchSpeed;
//         if (_state.IsADS) return _adsWalkSpeed;
//     
//         return _walkSpeed;
//     }
//     
//     private void JumpHandler()
//     {
//         if (_input.GetSkillAction("Jump", TISButtonReadMode.PressDown))
//         {
//             // Bỏ qua nếu đang trong quá trình chờ nhảy
//             if (_isJumpingPending) return;
//     
//             // Nếu đang slide thì hủy slide ngay lập tức
//             if (_state.IsSlide)
//             {
//                 _state.StopSlide();
//                 if (_resetSlide != null)
//                 {
//                     StopCoroutine(_resetSlide);
//                     _resetSlide = null;
//                 }
//             }
//             else if (_state.IsCrouch)
//             {
//                 _state.StopCrouch();
//                 return;
//             }
//     
//             // Bắt đầu Coroutine chờ nhảy nếu đang ở trên mặt đất
//             if (_characterController.isGrounded)
//             {
//                 StartCoroutine(DelayedJumpRoutine());
//             }
//             // Trigger animation lấy đà nhảy ngay khi bấm nút
//             _anim.IsJumping();
//             _state.Jump();
//         }
//     }
//     
//     /// <summary>
//     /// Coroutine hoãn việc tác động lực nhảy trong khoảng _jumpDelay giây
//     /// </summary>
//     private IEnumerator DelayedJumpRoutine()
//     {
//         _isJumpingPending = true;
//     
//         // Chờ đúng khoảng delay (0.25f)
//         yield return new WaitForSeconds(_jumpDelay);
//     
//         // Kiểm tra lại nếu vẫn đang ở gần mặt đất mới tung người lên
//         if (_characterController.isGrounded)
//         {
//             _velocity.y = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
//         }
//     
//         _isJumpingPending = false;
//     }
//     
//     private void SprintHandler()
//     {
//         if (_input.GetSkillAction("Run", TISButtonReadMode.PressDown))
//         {
//             if (_inputMove.y < 0.01f) return;
//             
//             _state.StopReload();
//             _state.StopThrow();
//             GrenadeID = 0;
//     
//             if (_state.IsCrouch)
//             {
//                 _state.StopCrouch();
//                 _state.StartRun();
//                 return;
//             }
//     
//             if (_state.IsSprint)
//                 return;
//     
//             if (_state.IsRun)
//             {
//                 _state.StartSprint();
//                 return;
//             }
//     
//             _state.StartRun();
//         }
//     }
//     
//     private void CrouchHandler()
//     {
//         if (_input.GetSkillAction("Crouch", TISButtonReadMode.PressDown))
//         {
//             _state.ToggleCrouch();
//         }
//     
//         _anim.IsCrouching(_state.IsCrouch);
//         _anim.IsSliding(_state.IsSlide);
//     }
//     
//     private void ShootHandler()
//     {
//         if (_state.IsSwitching || _state.IsThrow)
//             return;
//     
//         switch (_anim.ActiveWeaponMode())
//         {
//             case GunRecoilController.FiringMode.Semi:
//             case GunRecoilController.FiringMode.Burst:
//                 if (_input.GetSkillAction("Attack", TISButtonReadMode.PressDown))
//                 {
//                     _state.ToggleShoot();
//                     var (hitPos1, hitNormal1, showFx1) = GetRaycastHitInfo();
//                     if (_anim.ActiveWeponCTRL() != null)
//                         _anim.ActiveWeponCTRL().OnFire(hitPos1, hitNormal1, showFx1);
//                 }
//                 break;
//         
//             case GunRecoilController.FiringMode.Auto:
//                 if (_input.GetSkillAction("Attack", TISButtonReadMode.Hold))
//                 {
//                     _state.ToggleShoot();
//                     var (hitPos2, hitNormal2, showFx2) = GetRaycastHitInfo();
//                     if (_anim.ActiveWeponCTRL() != null)
//                         _anim.ActiveWeponCTRL().OnFire(hitPos2, hitNormal2, showFx2);
//                 }
//                 break;
//         }
//     }
//     
//     private (Vector3 hitPosition, Vector3 hitNormal, bool showEffect) GetRaycastHitInfo()
//     {
//         var weaponCtrl = _anim.ActiveWeponCTRL();
//         float maxDistance = weaponCtrl != null ? weaponCtrl.Data.Range : 100f;
//     
//         Camera cam = _componentManager.WeaponManager.Camera;
//         Ray ray = new Ray(cam.transform.position, cam.transform.forward);
//     
//         if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
//         {
//             bool showEffect = !hit.collider.CompareTag("Player");
//             return (hit.point, hit.normal, showEffect);
//         }
//     
//         Vector3 missPoint = ray.origin + ray.direction * maxDistance;
//         return (missPoint, -ray.direction, false);
//     }
//     
//     private void ADSHandler()
//     {
//         if (_state.IsSwitching || _state.IsThrow || _state.IsSlide)
//             return;
//     
//         if (_input.GetSkillAction("ADS", TISButtonReadMode.PressDown))
//         {
//             _state.EnterADS();
//         }
//     
//         if (_input.GetSkillAction("ADS", TISButtonReadMode.PressUp))
//         {
//             _state.ExitADS();
//         }
//     
//         _anim.ADS(_state.IsADS);
//     }
//     
//     private void ReloadHandler()
//     {
//         if (_input.GetSkillAction("Reload", TISButtonReadMode.PressDown))
//         {
//             _state.StartReload();
//         }
//         _anim.IsReloading(_state.IsReloading);
//     }
//     
//     public void StopReload() => _state.StopReload();
//     
//     private void SwitchWeaponHandler()
//     {
//         if (_state.IsThrow) return;
//     
//         if (_input.ZoomDelta != 0 || _input.GetSkillAction("Switch"))
//         {
//             _state.StartSwitch();
//         }
//         _anim.IsSwitching(_state.IsSwitching);
//     }
//     
//     public void StopSwitch() => _state.StopSwitch();
//     
//     private void ThrowHandler()
//     {
//         if (_state.IsSwitching || _state.IsSlide) return;
//     
//         bool Frag = _input.GetSkillAction("Q", TISButtonReadMode.Hold);
//         bool Flash = _input.GetSkillAction("E", TISButtonReadMode.Hold);
//     
//         if ((Frag || Flash) && !_state.IsThrow)
//         {
//             GrenadeID = Frag ? 1 : 2;
//             _state.StartThrow();
//         }
//         if (!Frag && !Flash && _state.IsThrow)
//         {
//             GrenadeID = 0;
//             _state.StopThrow();
//         }
//         _anim.GrenadeID(GrenadeID);
//         _anim.IsThrow(_state.IsThrow);
//     }
//     
//     private void ModeSwitchHandler()
//     {
//         if (_input.GetSkillAction("Mode"))
//         {
//             _anim.ModeSwitch();
//         }
//     }
// }