using Fusion;
using UnityEngine;

public class NInGameMessagesHandler : NetworkBehaviour
{
    private CharComponentManager _componentManager;
    private UIInGameMessagesHandler _uiInGameMessages;

    public void SendInGameRPCMessage(string userNickName, string msg)
    {
        RPC_InGameMessage($"<b>{userNickName}</b>: {msg}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_InGameMessage(string msg, RpcInfo info = default)
    {
        Debug.Log($"<color=cyan>[NInGameMessages]</color>: {msg}");
        
        if (_componentManager == null)
            _componentManager = NPlayer.Local.GetComponent<CharComponentManager>();

        if (_componentManager != null)
            _uiInGameMessages = _componentManager.UIInGameMessagesHandler;
        
        if (_uiInGameMessages != null)
            _uiInGameMessages.OnGameMessageReceived(msg);
        else Debug.Log("<color=red>[NInGameMessages]</color> null");
    }
}
