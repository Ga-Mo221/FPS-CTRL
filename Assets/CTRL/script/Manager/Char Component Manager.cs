using Fusion;
using Sirenix.OdinInspector;
using UnityEngine;

[DefaultExecutionOrder(10)]
public class CharComponentManager : MonoBehaviour
{
    public NetworkCharacterControllerCustom NetworkCharacterController;
    public NCharMovementHandler NCharMovementHandler;
    public NPlayer NPlayer;
    public ConstraintManager ConstraintManager;
    public WeaponManager WeaponManager;
    public FreeCameraController FreeCameraController;
    public PlayerMotionController PlayerMotionController;
    public TurnInPlace TurnInPlace;
    public Animator Animator;
    public AudioSource Audio_Body;
    public AudioSource Audio_HitSound;
    public AudioSource Audio_Flashbang;
    public Camera Camera;
    public NCharacterAnimationHandler NCharacterAnimationHandler;
    public NShootHandler NShootHandler;
    public NHPHandler NHPHandler;
    public UIHPPlayer UihpPlayer;
    public HitboxRoot HitboxRoot;
    public UIPlayerNickName UIPlayerNickName;
    public UIInGameMessagesHandler UIInGameMessagesHandler;
    public NInGameMessagesHandler NInGameMessagesHandler;
    public UIWeapon UIWeapon;
    public Transform CameraClippingPoint;
    public CameraShake CameraShake;
    public NetworkObject NetworkObject;

    [Button]
    public void Setup()
    {
        NetworkCharacterController = GetComponentInChildren<NetworkCharacterControllerCustom>(true);
        NCharMovementHandler = GetComponentInChildren<NCharMovementHandler>(true);
        NPlayer = GetComponentInChildren<NPlayer>(true);
        ConstraintManager = GetComponentInChildren<ConstraintManager>(true);
        WeaponManager = GetComponentInChildren<WeaponManager>(true);
        FreeCameraController = GetComponentInChildren<FreeCameraController>(true);
        PlayerMotionController = GetComponentInChildren<PlayerMotionController>(true);
        TurnInPlace = GetComponentInChildren<TurnInPlace>(true);
        Animator = GetComponentInChildren<Animator>(true);
        Audio_Body = GetComponentInChildren<AudioSource>(true);
        Camera = GetComponentInChildren<Camera>(true);
        NCharacterAnimationHandler = GetComponentInChildren<NCharacterAnimationHandler>(true);
        NShootHandler = GetComponentInChildren<NShootHandler>(true);
        NHPHandler = GetComponentInChildren<NHPHandler>(true);
        UihpPlayer = GetComponentInChildren<UIHPPlayer>(true);
        HitboxRoot = GetComponentInChildren<HitboxRoot>(true);
        UIPlayerNickName = GetComponentInChildren<UIPlayerNickName>(true);
        UIInGameMessagesHandler = GetComponentInChildren<UIInGameMessagesHandler>(true);
        NInGameMessagesHandler = GetComponentInChildren<NInGameMessagesHandler>(true);
        UIWeapon = GetComponentInChildren<UIWeapon>(true);
        CameraShake = GetComponentInChildren<CameraShake>(true);
        NetworkObject = GetComponent<NetworkObject>();
    }
}
