using System.Collections;
using TrueInputSystem;
using Unity.Mathematics;
using UnityEngine;

public class CharacterHandler : MonoBehaviour
{
    private PlayerMotionController _anim;
    private TISInputBridge _input;

    [Header("Move")]
    [SerializeField] private Vector2 _move;
    private Vector2 _targetMove;
    private Vector2 _moveVelocity;

    [SerializeField]
    [Tooltip("Thời gian để đạt tới target. Giá trị nhỏ = phản hồi nhanh hơn.")]
    private float _moveSmoothTime = 0.08f;

    [Header("State")]
    [SerializeField] private bool IsADS;
    [SerializeField] private bool IsRun;
    [SerializeField] private bool IsSprint;
    [SerializeField] private bool IsCrouch;
    [SerializeField] private bool IsSlide;
    [Range(0.4f, 1.5f)]
    [SerializeField] private float _slideDuration = 0.5f;
    [SerializeField] private bool IsReloading;
    public bool IsReload { get { return IsReloading; } set { IsReloading = value; } }
    [SerializeField] private bool IsSwitching;
    public bool IsSwitch { get { return IsSwitching; } set { IsSwitching = value; } }
    [SerializeField] private bool IsThrow;
    [SerializeField] private int GrenadeID;

    public Coroutine _resetSlide;

    private void Awake()
    {
        _input = TISInputBridge.Instance;
        _anim = GetComponent<PlayerMotionController>();
    }

    private void Update()
    {
        ReloadHandler();
        MoveHandler();
        SprintHandler();
        ADSHandler();
        ShootHandler();
        CrouchHandler();
        SwitchWeaponHandler();
        ThrowHandler();

        _anim.Move(_move);

        if (IsSlide && _resetSlide == null)
            _resetSlide = StartCoroutine(ResetSlide());
    }

    private IEnumerator ResetSlide()
    {
        yield return new WaitForSeconds(_slideDuration);
        IsSlide = false;
        _resetSlide = null;
    }

    private void MoveHandler()
    {
        _targetMove = Vector2.zero;

        // ===== X =====
        if (_input.MoveDirection.x != 0)
        {
            if (IsADS || IsThrow)
                _targetMove.x = 0.2f * Mathf.Sign(_input.MoveDirection.x);
            else if (IsRun && !IsCrouch)
                _targetMove.x = 0.6f * Mathf.Sign(_input.MoveDirection.x);
            else
                _targetMove.x = 0.4f * Mathf.Sign(_input.MoveDirection.x);

            if (IsSprint)
            {
                IsRun = true;
                IsSprint = false;
            }
        }

        // ===== Y =====
        if (_input.MoveDirection.y != 0)
        {
            if (IsADS || IsThrow)
                _targetMove.y = 0.2f * Mathf.Sign(_input.MoveDirection.y);
            else if (IsRun && !IsCrouch)
                _targetMove.y = 0.6f * Mathf.Sign(_input.MoveDirection.y);
            else if (IsSprint)
                _targetMove.y = 1.0f * Mathf.Sign(_input.MoveDirection.y);
            else
                _targetMove.y = 0.4f * Mathf.Sign(_input.MoveDirection.y);
        }

        // ===== Smooth =====
        _move = Vector2.SmoothDamp(
            _move,
            _targetMove,
            ref _moveVelocity,
            _moveSmoothTime);

        // tránh giá trị rất nhỏ gây rung Animator
        if (_move.sqrMagnitude < 0.0001f)
            _move = Vector2.zero;

        // Khi không còn input thì reset trạng thái
        if (_targetMove == Vector2.zero)
        {
            IsRun = false;
            IsSprint = false;
        }

        _anim.ActiveWeponCTRL().SetMoveDirection(_input.MoveDirection);
    }

    private void SprintHandler()
    {
        if (_input.GetSkillAction("Run", TISButtonReadMode.PressDown))
        {
            IsReloading = false;
            IsThrow = false;
            GrenadeID = 0;

            if (IsCrouch)
            {
                IsCrouch = false;
                IsRun = true;
                return;
            }

            if (IsSprint)
                return;

            if (IsRun)
            {
                IsSprint = true;
                IsRun = false;
                IsADS = false;
                return;
            }

            IsRun = !IsRun;

            if (IsRun)
            {
                IsSprint = false;
                IsADS = false;
            }
        }
    }

    private void CrouchHandler()
    {
        if (_input.GetSkillAction("Crouch", TISButtonReadMode.PressDown))
        {
            if (IsSlide)
                return;

            if (IsSprint)
            {
                IsSlide = true;
                IsSprint = false;
                IsRun = false;
            }

            IsCrouch = !IsCrouch;

            if (IsCrouch)
                IsRun = false;
        }

        _anim.IsCrouching(IsCrouch);
        _anim.IsSliding(IsSlide);
    }

    private void ShootHandler()
    {
        if (IsSwitching || IsThrow)
            return;
        switch (_anim.ActiveWeaponMode())
        {
            case GunRecoilController.FiringMode.Semi:
            case GunRecoilController.FiringMode.Burst:

                if (_input.GetSkillAction("Attack", TISButtonReadMode.PressDown))
                {
                    if (IsSprint) IsSprint = false;
                    if (IsRun) IsRun = false;
                    IsReloading = false;

                    _anim.ActiveWeponCTRL().OnFire();
                }

                break;

            case GunRecoilController.FiringMode.Auto:

                if (_input.GetSkillAction("Attack", TISButtonReadMode.Hold))
                {
                    if (IsSprint) IsSprint = false;
                    if (IsRun) IsRun = false;
                    IsReloading = false;

                    _anim.ActiveWeponCTRL().OnFire();
                }

                break;
        }
    }

    private void ADSHandler()
    {
        if (IsSwitching || IsThrow)
            return;

        if (_input.GetSkillAction("ADS", TISButtonReadMode.PressDown))
        {
            IsADS = true;
            IsRun = false;
            IsSprint = false;
            IsReloading = false;
        }

        if (_input.GetSkillAction("ADS", TISButtonReadMode.PressUp))
        {
            IsADS = false;
        }

        _anim.ADS(IsADS);
    }

    private void ReloadHandler()
    {
        if (IsSwitching || IsThrow)
            return;
        if (_input.GetSkillAction("Reload", TISButtonReadMode.PressDown))
        {
            IsReloading = true;
            IsRun = false;
            IsSprint = false;
            IsADS = false;
        }
        _anim.IsReloading(IsReloading);
    }

    private void SwitchWeaponHandler()
    {
        if (IsThrow) return;

        if (_input.ZoomDelta != 0)
        {
            IsSwitching = true;
            IsADS = false;
            IsReloading = false;
        }
        _anim.IsSwitching(IsSwitching);
    }

    private void ThrowHandler()
    {
        if (IsSwitching || IsSlide) return;

        bool Frag = _input.GetSkillAction("Q", TISButtonReadMode.Hold);
        bool Flash = _input.GetSkillAction("E", TISButtonReadMode.Hold);

        if ((Frag || Flash) && !IsThrow)
        {
            GrenadeID = Frag ? 1 : 2;
            IsThrow = true;
            IsADS = false;
            IsReloading = false;
            IsRun = false;
            IsSprint = false;
        }
        if (!Frag && !Flash && IsThrow)
        {
            GrenadeID = 0;
            IsThrow = false;
        }
        _anim.GrenadeID(GrenadeID);
        _anim.IsThrow(IsThrow);
    }
}