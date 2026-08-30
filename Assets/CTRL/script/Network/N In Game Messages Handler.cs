using Fusion;
using Game.UI;
using WebSocketSharp;

public class NInGameMessagesHandler : NetworkBehaviour
{
    private CharComponentManager _componentManager;
    private GameHudControllerBase _gameHudController;

    public void SendInGameRPCKill(string killer, string weaponname, string victim)
    {
        RPC_KillGame(killer, weaponname, victim);
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_KillGame(string killer, string weaponname, string victim, RpcInfo info = default)
    {
        if (_componentManager == null)
            _componentManager = NPlayer.Local.GetComponent<CharComponentManager>();

        if (_componentManager != null)
            _gameHudController = _componentManager.GameHudController();

        if (_gameHudController != null)
            _gameHudController.AddKill(killer, weaponname, victim);
    }

    public void SenManagerMessage(string message)
    {
        RPC_ManagerMessage(message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ManagerMessage(string message, RpcInfo info = default)
    {
        if (_componentManager == null)
            _componentManager = NPlayer.Local.GetComponent<CharComponentManager>();

        if (_componentManager != null)
            _gameHudController = _componentManager.GameHudController();

        string mes = $"<color=red>[He Thong]</color>: <color=#FFD700>{message}</color>";
        
        if (_gameHudController != null)
            _gameHudController.SendChatMessage(mes);
    }

    public void SendInGameRPCMessage()
    {
        if (_componentManager == null)
            _componentManager = NPlayer.Local.GetComponent<CharComponentManager>();

        if (_componentManager != null)
            _gameHudController = _componentManager.GameHudController();

        string nickname = "Test";
        string message = "";

        if (_gameHudController != null)
            message = _gameHudController.GetMessage().Trim();

        if (message.IsNullOrEmpty()) return;

        int main = GetStringCode(message);

        if (message.StartsWith("@b"))
            message = message.Substring(2).TrimStart();

        if (message.IsNullOrEmpty()) return;

        if (GameManager.Instance != null)
            nickname = GameManager.Instance.GetPlayerNickName();

        PlayerTeam senderTeam = NPlayer.Local.NetworkedTeam;
        GamePlayMode gameMode = NPlayer.Local.GamePlayMode;

        RPC_Message(main, nickname, message, senderTeam, gameMode);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_Message(int main, string playername, string message,
        PlayerTeam senderTeam, GamePlayMode gameMode, RpcInfo info = default)
    {
        if (_componentManager == null)
            _componentManager = NPlayer.Local.GetComponent<CharComponentManager>();

        if (_componentManager != null)
            _gameHudController = _componentManager.GameHudController();

        if (main == 2 && gameMode == GamePlayMode.Team)
        {
            if (NPlayer.Local.NetworkedTeam != senderTeam)
                return;
        }

        if (main == 1)
        {
            _gameHudController.SendChatMessage(
                $"<color=#FFD700>{playername}</color>: <color=white>{message}</color>");
        }
        else if (main == 2)
        {
            _gameHudController.SendChatMessage(
                $"<color=#00FF7F>[Team] {playername}</color>: <color=#00FF7F>{message}</color>");
        }
    }

    private int GetStringCode(string input)
    {
        if (!string.IsNullOrEmpty(input) && input.TrimStart().StartsWith("@b"))
        {
            return 2;
        }
        return 1;
    }
}