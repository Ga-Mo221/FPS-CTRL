using System.Collections.Generic;
using Fusion;
using Game.UI;
using UnityEngine;
using WebSocketSharp;

// - Kill feed: RPC chi gui NetworkId nguoi giet + byte chi so vu khi (~5B thay vi 3 string ~50B); moi may tu tra ten.
// - Chat: gui LEN HOST (RPC_SendChat); host lay ten that tu NPlayer (khong tin ten client tu dien),
//   chi chuyen tin doi cho dung dong doi (RPC_ShowChat co [RpcTarget]) -> khong lo chat doi dich, khong gui thua.
// - Chat dai: payload 1 RPC toi da 512 byte (Fusion) -> chia tin thanh nhieu manh theo BYTE UTF-8
//   (tieng Viet 2-3 byte/ky tu), moi manh 1 RPC kem msgId/part/count; may nhan ghep lai roi moi hien.
// - Chong chen rich-text (<size=500>, <sprite>...): boc ten + noi dung bang <noparse>.
public class NInGameMessagesHandler : NetworkBehaviour
{
    public const int MAX_CHAT_CHARS = 400;    // gioi han o nhap tren HUD
    private const int CHAT_CHUNK_BYTES = 400; // 1 manh/RPC: con du cho header + PlayerRef + 4 byte tham so (< 512)
    private const int MAX_CHAT_CHUNKS = 4;    // 4 x 400 byte >= 400 ky tu tieng Viet (toi da 3 byte/ky tu)
    private const float CHAT_PART_TIMEOUT = 5f; // manh le qua lau khong du bo -> bo
    private const float CHAT_COOLDOWN = 0.4f;

    // Chi so vu khi dac biet cho kill feed (khong nam trong GameManager._weapon_Prefabs)
    public const byte WEAPON_FRAG    = 255;
    public const byte WEAPON_ZONE    = 254;
    public const byte WEAPON_UNKNOWN = 253;

    private CharComponentManager _componentManager;
    private GameHudControllerBase _gameHudController;
    private float _lastChatTime = -10f;
    private byte _nextChatMsgId;

    // Manh chat dang cho ghep (tren handler cua NGUOI GUI, o moi may nhan) — key = msgId.
    private class ChatPartial
    {
        public byte Channel;
        public string[] Parts;
        public int Received;
        public float Time;
    }
    private readonly Dictionary<byte, ChatPartial> _chatPartials = new Dictionary<byte, ChatPartial>();
    private readonly List<byte> _expiredChat = new List<byte>();

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
    /// headshot: chi host biet bo phan trung dan -> gui kem 1 byte trong RPC kill feed san co
    /// (khong them RPC/bien networked rieng) de may cua nguoi giet tu phat am "headshoot".
    public void SendInGameRPCKill(NetworkId killerId, string killerName, string weaponName, bool headshot = false)
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
        RPC_KillGame(killerId, idx, headshot);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_KillGame(NetworkId killerId, byte weaponIdx, NetworkBool headshot, RpcInfo info = default)
    {
        string weapon = WeaponNameOf(weaponIdx);
        string killer = weaponIdx == WEAPON_ZONE && killerId == default(NetworkId) ? "Out of Bounds" : NameOf(killerId);
        ShowKill(killer, weapon);

        // Headshot (am thanh + UI): chi may cua nguoi giet (local player), bo qua tu sat.
        bool localKiller = killerId != Object.Id && NPlayer.Local != null && NPlayer.Local.Object != null
            && NPlayer.Local.Object.Id == killerId;
        if (headshot && localKiller)
            LocalKillEvents.RaiseHeadshot();

        // [Aemy 2026-10-22] Kill-theo-sung cho panel "SUNG CHU LUC". Truoc day RegisterKill khong duoc goi o dau ca
        // -> bang kill-theo-sung tren PlayFab dung yen (1 sung, 2 kill tu luc test cu).
        if (localKiller && weaponIdx != WEAPON_ZONE)
            Game.Match.MatchStatsReporter.Instance.RegisterKill(weapon, headshot);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_KillGameStr(string killer, string weaponname, RpcInfo info = default)
    {
        ShowKill(killer, weaponname);
        // [Aemy 2026-10-22] Duong du phong (chi co ten nguoi giet): khop ten local player, bo tu sat.
        if (NPlayer.Local != null && NPlayer.Local.Object != Object
            && !string.IsNullOrEmpty(killer) && killer == NPlayer.Local.Nickname.ToString() && weaponname != "Zone")
            Game.Match.MatchStatsReporter.Instance.RegisterKill(weaponname);
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

        List<string> chunks = SplitUtf8(message, CHAT_CHUNK_BYTES, MAX_CHAT_CHUNKS);
        if (chunks.Count == 0) return;

        // Gui tren handler cua NPlayer.Local (co InputAuthority) de host biet chinh xac ai gui.
        var self = NPlayer.Local != null ? NPlayer.Local.GetComponent<NInGameMessagesHandler>() : this;
        if (self == null) self = this;

        byte msgId = ++self._nextChatMsgId;
        byte count = (byte)chunks.Count;
        for (int i = 0; i < chunks.Count; i++)
            self.RPC_SendChat(channel, msgId, (byte)i, count, chunks[i]);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SendChat(byte channel, byte msgId, byte part, byte count, string msg, RpcInfo info = default)
    {
        // Host khong tin client: chan so manh/chi so sai va manh qua dai.
        if (count == 0 || count > MAX_CHAT_CHUNKS || part >= count) return;
        msg = Truncate(msg, CHAT_CHUNK_BYTES);
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
            RPC_ShowChat(p, channel, msgId, part, count, msg);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowChat([RpcTarget] PlayerRef target, byte channel, byte msgId, byte part, byte count, string msg, RpcInfo info = default)
    {
        if (count == 0 || count > MAX_CHAT_CHUNKS || part >= count) return;

        if (count == 1)
        {
            DisplayChat(channel, msg);
            return;
        }

        // Ghep manh: RPC reliable nhung khong dua vao thu tu den -> dat theo chi so part.
        float now = Time.unscaledTime;
        DropExpiredChatParts(now);

        if (!_chatPartials.TryGetValue(msgId, out var partial) || partial.Parts.Length != count)
        {
            partial = new ChatPartial { Channel = channel, Parts = new string[count] };
            _chatPartials[msgId] = partial;
        }
        partial.Time = now;
        if (partial.Parts[part] == null)
        {
            partial.Parts[part] = msg;
            partial.Received++;
        }

        if (partial.Received < count) return;
        _chatPartials.Remove(msgId);
        DisplayChat(partial.Channel, string.Concat(partial.Parts));
    }

    private void DropExpiredChatParts(float now)
    {
        if (_chatPartials.Count == 0) return;
        _expiredChat.Clear();
        foreach (var kv in _chatPartials)
            if (now - kv.Value.Time > CHAT_PART_TIMEOUT) _expiredChat.Add(kv.Key);
        foreach (var id in _expiredChat)
            _chatPartials.Remove(id);
    }

    private void DisplayChat(byte channel, string msg)
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

    /// <summary>
    /// Chia chuoi thanh cac manh, moi manh &lt;= maxBytes byte UTF-8, khong cat doi surrogate pair.
    /// Toi da maxChunks manh (phan du bi bo).
    /// </summary>
    public static List<string> SplitUtf8(string s, int maxBytes, int maxChunks)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(s)) return result;

        var enc = System.Text.Encoding.UTF8;
        int start = 0, bytes = 0;
        for (int i = 0; i < s.Length && result.Count < maxChunks; )
        {
            int charLen = char.IsHighSurrogate(s[i]) && i + 1 < s.Length ? 2 : 1;
            int charBytes = enc.GetByteCount(s.ToCharArray(i, charLen));
            if (bytes + charBytes > maxBytes)
            {
                result.Add(s.Substring(start, i - start));
                start = i;
                bytes = 0;
                continue;
            }
            bytes += charBytes;
            i += charLen;
        }
        if (start < s.Length && result.Count < maxChunks)
            result.Add(s.Substring(start));
        return result;
    }

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
