using System.Collections;
using TrueInputSystem;
using UnityEngine;

public class CharacterHandler : MonoBehaviour
{
    private PlayerMotionController _anim;
    private TISInputBridge _input;

    [SerializeField] private Vector2 _move;
    
    [SerializeField] private bool IsADS;
    [SerializeField] private bool IsRun;
    [SerializeField] private bool IsSprint;
    [SerializeField] private bool IsCrouch;
    [SerializeField] private bool IsSlide;

    public Coroutine _resetSlide;

    private void Awake()
    {
        _input = TISInputBridge.Instance;
        _anim = GetComponent<PlayerMotionController>();
    }

    private void Update()
    {
        MoveHandler();
        SprintHandler();
        ADSHandler();
        ShootHandler();
        CrouchHandler();
        
        _anim.Move(_move);
        
        if (IsSlide && _resetSlide == null)
            _resetSlide = StartCoroutine(ResetSlide());
    }

    private IEnumerator ResetSlide()
    {
        yield return new WaitForSeconds(0.5f);
        IsSlide = false;
        _resetSlide = null;
    }

    private void MoveHandler()
    {
        _move = Vector2.zero;
        if (_input.MoveDirection.x != 0)
        {
            if (IsADS) _move.x = 0.2f * minasu(_input.MoveDirection.x);
            else if (IsRun && !IsCrouch) _move.x = 0.6f * minasu(_input.MoveDirection.x);
            else _move.x = 0.4f  * minasu(_input.MoveDirection.x);
            
            if (IsSprint)
            {
                IsRun = true;
                IsSprint = false;
            }
        }
        
        if (_input.MoveDirection.y != 0)
        {
            if (IsADS) _move.y = 0.2f * minasu(_input.MoveDirection.y);
            else if (IsRun && !IsCrouch) _move.y = 0.6f * minasu(_input.MoveDirection.y);
            else if (IsSprint) _move.y = 1f * minasu(_input.MoveDirection.y);
            else _move.y = 0.4f * minasu(_input.MoveDirection.y);
        }

        if (_move == Vector2.zero)
        {
            IsRun = false;
            IsSprint = false;
            Debug.Log("return");
        }
    }

    private void SprintHandler()
    {
        if (_input.GetSkillAction("Run", TISButtonReadMode.PressDown))
        {
            if (IsCrouch)
            {
                IsCrouch = false;
                IsRun = true;
                return;
            }
            if (IsSprint) return;
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
            if (IsSlide) return;
            if (IsSprint)
            {
                IsSlide = true;
                IsSprint = false;
                IsRun = false;
            }
            IsCrouch = !IsCrouch;
            if (IsCrouch)
            {
                IsRun = false;
            }
        }
        _anim.IsCrouching(IsCrouch);
        _anim.IsSliding(IsSlide);
    }

    private void ShootHandler()
    {
        switch (_anim.ActiveWeaponMode())
        {
            case GunRecoilController.FiringMode.Semi:
            case GunRecoilController.FiringMode.Burst:
                if (_input.GetSkillAction("Attack", TISButtonReadMode.PressDown))
                {
                    if (IsSprint) IsSprint = false;
                    if (IsRun) IsRun = false;
                    _anim.ActiveWeponCTRL().OnFire();
                }
                break;
            case GunRecoilController.FiringMode.Auto:
                if (_input.GetSkillAction("Attack", TISButtonReadMode.Hold))
                {
                    if (IsSprint) IsSprint = false;
                    if (IsRun) IsRun = false;
                    _anim.ActiveWeponCTRL().OnFire();
                }
                break;
        }
    }

    private void ADSHandler()
    {
        if (_input.GetSkillAction("ADS", TISButtonReadMode.PressDown))
        {
            IsADS = true;
            IsRun = false;
            IsSprint = false;
        }

        if (_input.GetSkillAction("ADS", TISButtonReadMode.PressUp))
        {
            IsADS = false;
        }
        _anim.ADS(IsADS);
    }
    
    

    private int minasu(float value)
    {
        if (value < 0) return -1;
        if (value > 0) return 1;
        return 0;
    }
}
