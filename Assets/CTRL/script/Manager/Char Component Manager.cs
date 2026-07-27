using Sirenix.OdinInspector;
using UnityEngine;

[DefaultExecutionOrder(10)]
public class CharComponentManager : MonoBehaviour
{
    public NetworkCharacterControllerCustom NetworkCharacterController;
    public NCharMovementHandler NCharMovementHandler;
    public NPlayer NPlayer;
    public ConstraintManager ConstraintManager;
    public NWeaponManager NWeaponManager;
    public FreeCameraController FreeCameraController;
    public PlayerMotionController PlayerMotionController;
    public TurnInPlace TurnInPlace;
    public Animator Animator;
    public AudioSource AudioSource;
    public Camera Camera;

    [Button]
    public void Setup()
    {
        NetworkCharacterController = GetComponentInChildren<NetworkCharacterControllerCustom>(true);
        NCharMovementHandler = GetComponentInChildren<NCharMovementHandler>(true);
        NPlayer = GetComponentInChildren<NPlayer>(true);
        ConstraintManager = GetComponentInChildren<ConstraintManager>(true);
        NWeaponManager = GetComponentInChildren<NWeaponManager>(true);
        FreeCameraController = GetComponentInChildren<FreeCameraController>(true);
        PlayerMotionController = GetComponentInChildren<PlayerMotionController>(true);
        TurnInPlace = GetComponentInChildren<TurnInPlace>(true);
        Animator = GetComponentInChildren<Animator>(true);
        AudioSource = GetComponentInChildren<AudioSource>(true);
        Camera = GetComponentInChildren<Camera>(true);
    }
}
