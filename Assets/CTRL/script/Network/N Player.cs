using Fusion;
using UnityEngine;

public class NPlayer : NetworkBehaviour, IPlayerLeft
{
    public static NPlayer Local { get; set; }

    [SerializeField] private Transform[] _hide_Mesh;
    [SerializeField] private GameObject _uiLocalPlayer;
    
    private CharComponentManager _componentManager;
    
    public override void Spawned()
    {
        _componentManager = GetComponent<CharComponentManager>();
        
        if (Object.HasInputAuthority)
        {
            Local = this;
            
            Utils.SetRenderLayerInChildren(_hide_Mesh, LayerMask.NameToLayer("LocalPlayerModel"));
            
            Camera.main.gameObject.SetActive(false);
            
            Debug.Log("<color=green>[NPlayer]</color> Spawned Local Player.", this);
        }else
        {
            _uiLocalPlayer?.SetActive(false);
            Camera localCamera = _componentManager.Camera;
            localCamera.transform.parent.gameObject.SetActive(false);
            // localCamera.enabled = false;
            //
            // AudioListener audioListener = localCamera.GetComponent<AudioListener>();
            // if (audioListener != null)
            // {
            //     Debug.Log("<color=green>[NPlayer]</color> Client Disable AudioListener.", this);
            //     audioListener.enabled = false;
            // }
            
            Debug.Log("<color=green>[NPlayer]</color> Spawned Remote Player.", this);
        }

        transform.name = $"Player_{Object.Id}";
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (player == Object.InputAuthority)
            Runner.Despawn(Object);
    }
}
