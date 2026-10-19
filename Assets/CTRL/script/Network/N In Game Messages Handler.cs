using System.Collections.Generic;
using Fusion;
using Game.UI;
using UnityEngine;
using WebSocketSharp;

// - Kill feed: RPC chi gui NetworkId nguoi giet + byte chi so vu khi (~5B thay vi 3 string ~50B); moi may tu tra ten.
// - Chat: gui LEN HOST (RPC_SendChat); host lay ten that tu NPlayer (khong tin ten client tu dien),
//   chi chuyen tin doi cho dung dong doi (RPC_ShowChat co [RpcTarget]) -> khong lo chat doi dich, khong gui thua.
// - Chat dai: cat theo BYTE UTF-8 (tieng Viet 2-3 byte/ky tu) truoc khi gui de khong vuot gioi han payload RPC.
// - Chong chen rich-text (<size=500>, <sprite>...): boc ten + noi dung bang <noparse>.
public class NInGameMessagesHandler : NetworkBehaviour
{
    public const int MAX_CHAT_CHARS = 120;  // gioi han o nhap tren HUD
    private const int MAX_CHAT_BYTES = 300; // chua cho header + tham so khac cua RPC
    private const float CHAT_COOLDOWN = 0.4f;

    // Chi so vu khi dac biet cho kill feed (khong nam trong GameManager._weapon_Prefabs)
    public const byte WEAPON_FRAG    = 255;
    public const byte WEAPON_ZONE    = 254;
    public const byte WEAPON_UNKNOWN = 253;

    private CharComponentManager _componentManager;
    private GameHudControllerBase _gameHudController;
    private float _lastChatTime = -10f;

    // Ten theo NetworkId, cache lai de van hien dung khi nguoi giet vua thoat phong.
    private static readonly Dictionary<NetworkId, string> _nameCache = new Dictionary<NetworkId, string>();

    private bool EnsureHud()
    {
        if (_componentManager == null && NPlayer.Local != null)
            _componentManager = NPlayer.Local.GetComponent<CharComponentManager>();
        if (_componentManager != null)
            _gameHudController = _componentManager.GameHudController();
        return _gameHudController != null;
    }

    // ============================== KILL FEED ==============================

    /// <summary>Host goi khi co nguoi chet (tren handler cua NAN NHAN).</summary>
    public void SendInGameRPCKill(NetworkId killerId, string killerName, string weaponName)
    {
        if (!Object.HasStateAuthority) return;
        byte idx = WeaponIndexOf(weaponName);
        if (idx == WEAPON_UNKNOWN)
        {
            // Hiem: ten vu khi khong co trong danh sach -> du phong gui chuoi (van cat byte cho an toan).
            RPC_KillGameStr(Truncate(killerName, 64), Truncate(weaponName, 64));
            return;
        }
        if (killerId == default(NetworkId) && !string.IsNullOrEmpty(killerName) && idx != WEAPON_ZONE)
        {
            RPC_KillGameStr(Truncate(killerName, 64), Truncate(weaponName, 64));
            return;
        }
        RPC_KillGame(killerId, idx);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_KillGame(NetworkId killerId, byte weaponIdx, RpcInfo info = default)
    {
        string weapon = WeaponNameOf(weaponIdx);
        string killer = weaponIdx == WEAPON_ZONE && killerId == default(NetworkId) ? "Out of Bounds" : NameOf(killerId);
        ShowKill(killer, weapon);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_KillGameStr(string killer, string weaponname, RpcInfo info = default)
    {
        ShowKill(killer, weaponname);
    }

    private void ShowKill(string killer, string weaponname)
    {
        string victim = NameOf(Object.Id);
        if (!EnsureHud()) return;
        _gameHudController.AddKill(killer, weaponname, victim);

        // Truoc day SetDeadStatus di kem RPC_ApplyHitDamage moi vien dan.
        // RPC nay nam tren object cua NAN NHAN, nen may co InputAuthority = chinh nan nhan.
        if (Object.HasInputAuthority)
            _gameHudController.SetDeadStatus(killer, weaponname);
    }

    /// <summary>Ten hien thi cua 1 player object; cache lai de dung khi object da despawn.</summary>
    public string NameOf(NetworkId id)
    {
        if (id == default(NetworkId)) return "?";
        NetworkObject no;
        if (Runner != null && Runner.TryFindObject(id, out no) && no != null)
        {
            var np = no.GetComponent<NPlayer>();
            if (np != null)
            {
                string n = np.Nickname.ToString();
                if (!string.IsNullOrEmpty(n)) { _nameCache[id] = n; return n; }
            }
        }
        string cached;
        return _nameCache.TryGetValue(id, out cached) ? cached : "?";
    }

    public static void RememberName(NetworkId id, string name)
    {
        if (id != default(NetworkId) && !string.IsNullOrEmpty(name)) _nameCache[id] = name;
    }

    private static byte WeaponIndexOf(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName)) return WEAPON_UNKNOWN;
        if (weaponName == "Frag") return WEAPON_FRAG;
        if (weaponName == "Zone") return WEAPON_ZONE;
        var gm = GameManager.Instance;
        if (gm == null) return WEAPON_UNKNOWN;
        int i = gm.GetWeaponIndexByName(weaponName);
        return i >= 0 && i < WEAPON_UNKNOWN ? (byte)i : WEAPON_UNKNOWN;
    }

    private static string WeaponNameOf(byte idx)
    {
        if (idx == WEAPON_FRAG) return "Frag";
        if (idx == WEAPON_ZONE) return "Zone";
        var gm = GameManager.Instance;
        return gm != null ? (gm.GetWeaponNameByIndex(idx) ?? "") : "";
    }

    // ============================== HE THONG ==============================

    public void SenManagerMessage(string message)
    {
        RPC_ManagerMessage(message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ManagerMessage(string message, RpcInfo info = default)
    {
        if (!EnsureHud()) return;
        string mes = $"<color=red>[He Thong]</color>: <color=#FFD700>{NoParse(message)}</color>";
        _gameHudController.SendChatMessage(mes);
    }

    // ================================ CHAT ================================

    /// <summary>May local goi khi bam Enter (tren handler cua chinh minh).</summary>
    public void SendInGameRPCMessage()
    {
        if (!EnsureHud()) return;

        string message = _gameHudController.GetMessage();
        message = message != null ? message.Trim() : "";
        if (message.IsNullOrEmpty()) return;

        byte channel = (byte)GetStringCode(message);
        if (message.StartsWith("@b"))
            message = message.Substring(2).TrimStart();
        if (message.IsNullOrEmpty()) return;

        if (Time.unscaledTime - _lastChatTime < CHAT_COOLDOWN) return; // chong spam
        _lastChatTime = Time.unscaledTime;

        if (message.Length > MAX_CHAT_CHARS) message = message.Substring(0, MAX_CHAT_CHARS);
        message = Truncate(message, MAX_CHAT_BYTES);

        // Gui tren handler cua NPlayer.Local (co InputAuthority) de host biet chinh xac ai gui.
        var self = NPlayer.Local != null ? NPlayer.Local.GetComponent<NInGameMessagesHandler>() : this;
        if (self == null) self = this;
        self.RPC_SendChat(channel, message);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SendChat(byte channel, string msg, RpcInfo info = default)
    {
        msg = Truncate(msg, MAX_CHAT_BYTES);
        if (string.IsNullOrEmpty(msg)) return;
        var sender = GetComponent<NPlayer>(); // object cua nguoi gui (InputAuthority) -> ten that, khong gia mao duoc
        if (sender == null) return;
        bool teamOnly = channel == 2 && sender.GamePlayMode == GamePlayMode.Team;

        foreach (var p in Runner.ActivePlayers)
        {
            if (teamOnly)
            {
                NetworkObject o;
                if (!Runner.TryGetPlayerObject(p, out o) || o == null) continue;
                var np = o.GetComponent<NPlayer>();
                if (np == null || np.NetworkedTeam != sender.NetworkedTeam) continue;
            }
            RPC_ShowChat(p, channel, msg);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowChat([RpcTarget] PlayerRef target, byte channel, string msg, RpcInfo info = default)
    {
        if (!EnsureHud()) return;
        var sender = GetComponent<NPlayer>();
        string playername = sender != null ? sender.Nickname.ToString() : "?";
        if (sender != null) RememberName(Object.Id, playername);

        if (channel == 2)
            _gameHudController.SendChatMessage(
                $"<color=#00FF7F>[Team] {NoParse(playername)}</color>: <color=#00FF7F>{NoParse(msg)}</color>");
        else
            _gameHudController.SendChatMessage(
                $"<color=#FFD700>{NoParse(playername)}</color>: <color=white>{NoParse(msg)}</color>");
    }

    private int GetStringCode(string input)
    {
        if (!string.IsNullOrEmpty(input) && input.TrimStart().StartsWith("@b"))
        {
            return 2;
        }
        return 1;
    }

    // ================================ TIEN ICH ================================

    /// <summary>Cat chuoi de so byte UTF-8 &lt;= maxBytes (khong cat doi surrogate pair).</summary>
    public static string Truncate(string s, int maxBytes)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var enc = System.Text.Encoding.UTF8;
        if (enc.GetByteCount(s) <= maxBytes) return s;
        int len = s.Length;
        while (len > 0 && enc.GetByteCount(s.Substring(0, len)) > maxBytes) len--;
        if (len > 0 && char.IsHighSurrogate(s[len - 1])) len--;
        return s.Substring(0, len);
    }

    /// <summary>Hien nguyen van (khong cho chen tag rich-text).</summary>
    public static string NoParse(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return "<noparse>" + s.Replace("</noparse>", "</ noparse>") + "</noparse>";
    }
}
