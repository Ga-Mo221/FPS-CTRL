using Fusion;
using GaMo.TrueInputSystem;
using UnityEngine;

public class CharacterInputHandler : MonoBehaviour
{
    private const string ATTACK = "Attack";
    private const string RELOAD = "Reload";
    private const string ADS = "ADS";
    private const string CROUCH = "Crouch";
    private const string SWITCH_WEAPON = "Switch";
    private const string THROW_FRAG = "Q";
    private const string THROW_FLASH = "E";
    private const string JUMP = "Jump";
    private const string SWITCH_MODE = "Mode";
    private const string RUN = "Run";
    private const string SWITCH_WEAPON_1 = "Weapon1";
    private const string SWITCH_WEAPON_2 = "Weapon2";
    
    private CharComponentManager _componentManager;
    private NCharMovementHandler _movementHandler;
    //private NPlayer _player;
    private TISInputBridge _input;
    
    Vector2 _moveDirection;
    private Vector2 _lookDelta;
    // [Aemy 2026-10-19] Tay cam: lay TRUNG BINH toc do cac frame giua 2 tick (truoc day chi lay frame cuoi).
    // Stick gan nhu khong doi; gyro (toc do goc thay doi lien tuc) thi khong bi mat chuyen dong giua cac frame.
    private Vector2 _padLookSum;
    private int _padLookFrames;
    private float _zoomDelta;
    private Vector3 _aimForward;
    
    private NetworkButtons _buttons;

    private void Awake()
    {
        _input = TISInputBridge.Instance;
        _componentManager = GetComponent<CharComponentManager>();
        _movementHandler = _componentManager.NCharMovementHandler;
        //_player = _componentManager.NPlayer;
    }

    private void Update()
    {
        if (!_movementHandler.Object.HasInputAuthority) return;
        
        if (NetworkGameState.Instance != null && NetworkGameState.Instance.IsMatchOver()) return;
        
        if (GameManager.Instance.OnUI()) return;
        
        if (NetworkGameState.Instance != null && NetworkGameState.Instance.GameStarted)
        {
            _moveDirection = _input.MoveDirection;
        }
        else _moveDirection = Vector2.zero;
        
        // [NET-OPT 2026-10-18 Aemy] (bao cao 9.7) Chuot/cam ung tra delta THEO FRAME -> cong don giua 2 tick
        // (truoc day gan de: FPS cao mat delta, FPS thap dung lai 1 delta cho nhieu tick => do nhay phu thuoc FPS).
        // Tay cam tra TOC DO (stick) -> giu gia tri moi nhat, khong cong don.
        if (_input.IsGamepad)
        {
            _padLookSum += _input.LookDelta;
            _padLookFrames++;
            _lookDelta = _padLookSum / _padLookFrames;
        }
        else _lookDelta += _input.LookDelta;
        _zoomDelta += _input.ZoomDelta;
        _aimForward = _componentManager.Camera.transform.forward;
        
        if (_input.GetSkillAction(RELOAD, TISButtonReadMode.PressDown))
            _buttons.Set(InputButtons.Reload, true);
        
        if (_input.GetSkillAction(CROUCH))
            _buttons.Set(InputButtons.Crouch, true);
        
        if (_input.GetSkillAction(SWITCH_WEAPON))
            _buttons.Set(InputButtons.Switch, true);
        
        if (_input.GetSkillAction(JUMP))
            _buttons.Set(InputButtons.Jump, true);
        
        if (_input.GetSkillAction(SWITCH_MODE))
            _buttons.Set(InputButtons.Mode, true);
        
        if (_input.GetSkillAction(RUN))
            _buttons.Set(InputButtons.Run, true);
        
        if (_input.GetSkillAction(SWITCH_WEAPON_1, TISButtonReadMode.PressDown))
            _buttons.Set(InputButtons.Weapon1, true);
        
        if (_input.GetSkillAction(SWITCH_WEAPON_2, TISButtonReadMode.PressDown))
            _buttons.Set(InputButtons.Weapon2, true);
    }

    public NInputData GetNetworkInput()
    {
        NInputData nInputData = new NInputData();
        
        if (GameManager.Instance != null && 
            !GameManager.Instance.OnUI() && 
            NetworkGameState.Instance != null && 
            !NetworkGameState.Instance.IsMatchOver())
        {
            nInputData.MoveDirection = _moveDirection;
            nInputData.LookDelta = _lookDelta;
            nInputData.AimForward = _aimForward;

            nInputData.Buttons = _buttons;
            if (_zoomDelta != 0f) nInputData.Buttons.Set(InputButtons.ScrollSwitch, true); // [NET-OPT] thay ZoomDelta float

            nInputData.Buttons.Set(InputButtons.ADS,        _input.GetSkillAction(ADS,         TISButtonReadMode.Hold));
            if (NetworkGameState.Instance != null && NetworkGameState.Instance.GameStarted)
            {
                nInputData.Buttons.Set(InputButtons.Attack, _input.GetSkillAction(ATTACK, TISButtonReadMode.Hold));
                nInputData.Buttons.Set(InputButtons.ThrowFrag, _input.GetSkillAction(THROW_FRAG, TISButtonReadMode.Hold));
                nInputData.Buttons.Set(InputButtons.ThrowFlash, _input.GetSkillAction(THROW_FLASH, TISButtonReadMode.Hold));
            }
        }
        
        return nInputData;
    }

    public void ResetButton()
    {
        _buttons = default;
        if (!_input.IsGamepad) _lookDelta = default; // tay cam: giu toc do stick cho tick sau (frame co the khong Update kip)
        _padLookSum = default; _padLookFrames = 0;   // [Aemy 2026-10-19] tick moi -> trung binh lai tu dau
        _zoomDelta = 0f;
    }
}
