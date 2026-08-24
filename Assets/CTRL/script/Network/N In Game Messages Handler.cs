using System;
using Fusion;
using Game.UI;
using UnityEngine;

public class NInGameMessagesHandler : NetworkBehaviour
{
    private CharComponentManager _componentManager;
    private GameHudControllerBase _gameHudController;

    public void SendInGameRPCMessage(string killer, string weaponname, string victim)
    {
        RPC_InGameMessage(killer, weaponname, victim);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_InGameMessage(string killer, string weaponname, string victim, RpcInfo info = default)
    {
        if (_componentManager == null)
            _componentManager = NPlayer.Local.GetComponent<CharComponentManager>();

        if (_componentManager != null)
            _gameHudController = _componentManager.GameHudController();
            
        if (_gameHudController != null)
            _gameHudController.AddKill(killer, weaponname, victim);
    }
}
