using Fusion;
using UnityEngine;

public class NPlayer : NetworkBehaviour, IPlayerLeft
{
    [SerializeField] private PlayerType _type = PlayerType.Blue;
    public static NPlayer Local { get; private set; }

    [SerializeField] private Transform[] _hide_Mesh;
    
    [Networked, OnChangedRender(nameof(OnNicknameChanged))]
    public NetworkString<_16> Nickname { get; private set; }

    private bool _isPublicJoinMessageSent = false;
    
    public CharComponentManager _componentManager {get; private set;}
    private NInGameMessagesHandler _nInGameMessagesHandler;
    private UIHPPlayer _uiLocalPlayer;
    private UIPlayerNickName _uiPlayerNickName;
    
    public override void Spawned()
    {
        _componentManager = GetComponent<CharComponentManager>();
        _nInGameMessagesHandler = _componentManager.NInGameMessagesHandler;
        _uiLocalPlayer = _componentManager.UihpPlayer;
        _uiPlayerNickName = _componentManager.UIPlayerNickName;
        
        if (Object.HasInputAuthority)
        {
            Local = this;
            
            Utils.SetRenderLayerInChildren(_hide_Mesh, LayerMask.NameToLayer("LocalPlayerModel"));
            
            Rpc_SetNickName(PlayerPrefs.GetString("PlayerNickName"));
            
            Camera.main.gameObject.SetActive(false);
            
            Debug.Log("<color=green>[NPlayer]</color> Spawned Local Player.", this);
        }else
        {
            _uiLocalPlayer?.gameObject.SetActive(false);
            Camera localCamera = _componentManager.Camera;
            localCamera.transform.parent.gameObject.SetActive(false);
            _uiPlayerNickName.SetActive(_type);
            
            if (!string.IsNullOrEmpty(Nickname.ToString()))
            {
                _uiPlayerNickName.SetNickName(Nickname.ToString());
            }
            
            Debug.Log("<color=green>[NPlayer]</color> Spawned Remote Player.", this);
        }
        
        // set player này là player objcet
        Runner.SetPlayerObject(Object.InputAuthority, Object);

        transform.name = $"Player_{Object.Id}";
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (Object.HasStateAuthority)
        {
            if (Runner.TryGetPlayerObject(player, out NetworkObject playerLeftNetworkObject))
            {
                if (playerLeftNetworkObject == Object)
                    Local.GetComponent<NInGameMessagesHandler>().SendInGameRPCMessage(
                        playerLeftNetworkObject.GetComponent<NPlayer>().Nickname.ToString(), "Left");
            }
        }
        
        if (player == Object.InputAuthority)
            Runner.Despawn(Object);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_SetNickName(string nickName, RpcInfo info = default)
    {
        Debug.Log($"<color=green>[NPlayer]</color> Rpc_SetNickName: {nickName}", this);
        Nickname = nickName;

        if (!_isPublicJoinMessageSent)
        {
            _nInGameMessagesHandler.SendInGameRPCMessage(nickName, "Joined");
            _isPublicJoinMessageSent = true;
        }
    }

    void OnNicknameChanged()
    {
        Debug.Log($"<color=green>[NPlayer]</color> OnNicknameChanged to {Nickname.ToString()}", this);
        _componentManager.UIPlayerNickName.SetNickName(Nickname.ToString());
    }
}
