using System;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using Backend.Bootstrap;
using Backend.Interfaces;
using Backend.Models;

namespace Backend.PlayfabImpl
{
    // [Aemy 2026-09-21] Party dua tren PlayFab SharedGroupData + CloudScript invite.
    public class PlayFabPartyService : IPartyService
    {
        private const string InviteKey = "PartyInvite";
        private const string LeaderKey = "leader";
        private const string MatchKey  = "match";
        private const string SettingsKey = "cfg"; // [Aemy 2026-10-17]

        private static string SelfId() { return BackendBootstrap.AuthService != null ? BackendBootstrap.AuthService.CurrentUserId : null; }
        private static string SelfName() { var p = ProfileCache.Cached; return (p != null && !string.IsNullOrEmpty(p.DisplayName)) ? p.DisplayName : "PLAYER"; }
        private static string SelfAvatar() { var p = ProfileCache.Cached; return (p != null && p.AvatarUrl != null) ? p.AvatarUrl : ""; }
        private static long Now() { return System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }

        // [PARTY-HB 2026-09-29] Heartbeat: ghi lai blob voi seen moi (giu ready hien tai).
        public void Touch(string code, bool ready, Action onOk, Action<AuthError> onError)
        {
            string self = SelfId();
            if (string.IsNullOrEmpty(self) || string.IsNullOrEmpty(code)) { if (onOk != null) onOk(); return; }
            var blob = JsonUtility.ToJson(new PartyMemberBlob { name = SelfName(), ready = ready, avatar = SelfAvatar(), seen = Now() });
            PlayFabClientAPI.UpdateSharedGroupData(new UpdateSharedGroupDataRequest
            { SharedGroupId = code, Permission = UserDataPermission.Public, Data = new Dictionary<string, string> { { self, blob } } },
            _ => { if (onOk != null) onOk(); }, e => { if (onError != null) onError(Map(e)); });
        }

        // [PARTY-HB] Leader da mot thanh vien mat ket noi ra khoi nhom.
        public void KickMember(string code, string playFabId, Action onOk, Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(playFabId)) { if (onOk != null) onOk(); return; }
            PlayFabClientAPI.RemoveSharedGroupMembers(new RemoveSharedGroupMembersRequest { SharedGroupId = code, PlayFabIds = new List<string> { playFabId } },
            _ => { if (onOk != null) onOk(); }, e => { if (onError != null) onError(Map(e)); });
        }
        private static AuthError Map(PlayFabError e) { return new AuthError { ErrorCode = e.Error.ToString(), ErrorMessage = e.ErrorMessage }; }
        private static AuthError Err(string msg) { return new AuthError { ErrorCode = "Local", ErrorMessage = msg }; }

        public void CreateParty(Action<string> onCode, Action<AuthError> onError)
        {
            string self = SelfId();
            if (string.IsNullOrEmpty(self)) { if (onError != null) onError(Err("Chua dang nhap.")); return; }
            string code = System.Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
            PlayFabClientAPI.CreateSharedGroup(new CreateSharedGroupRequest { SharedGroupId = code },
                _ =>
                {
                    var blob = JsonUtility.ToJson(new PartyMemberBlob { name = SelfName(), ready = true, avatar = SelfAvatar(), seen = Now() }); // leader ready mac dinh
                    PlayFabClientAPI.UpdateSharedGroupData(new UpdateSharedGroupDataRequest
                    {
                        SharedGroupId = code, Permission = UserDataPermission.Public,
                        Data = new Dictionary<string, string> { { LeaderKey, JS(self) }, { self, blob } }
                    },
                    __ => { if (onCode != null) onCode(code); },
                    e => { if (onError != null) onError(Map(e)); });
                },
                e => { if (onError != null) onError(Map(e)); });
        }

        public void JoinParty(string code, Action onOk, Action<AuthError> onError)
        {
            string self = SelfId();
            if (string.IsNullOrEmpty(self) || string.IsNullOrEmpty(code)) { if (onError != null) onError(Err("Thieu code / chua dang nhap.")); return; }
            // [PARTY-FIX 2026-09-29] Client KHONG the tu them minh vao shared group (AddSharedGroupMembers chi cho
            // thanh vien hien tai goi). Nho CloudScript (server) them ho roi ghi blob.
            var blob = JsonUtility.ToJson(new PartyMemberBlob { name = SelfName(), ready = false, avatar = SelfAvatar(), seen = Now() });
            PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
            {
                FunctionName = "JoinPartyGroup",
                FunctionParameter = new { code = code, blob = blob },
                GeneratePlayStreamEvent = false
            },
            res =>
            {
                if (res.Error != null) { if (onError != null) onError(Err("CloudScript JoinPartyGroup: " + res.Error.Message)); return; }
                if (onOk != null) onOk();
            },
            e => { if (onError != null) onError(Map(e)); });
        }

        public void LeaveParty(string code, Action onOk, Action<AuthError> onError)
        {
            string self = SelfId();
            if (string.IsNullOrEmpty(self) || string.IsNullOrEmpty(code)) { if (onOk != null) onOk(); return; }
            PlayFabClientAPI.RemoveSharedGroupMembers(new RemoveSharedGroupMembersRequest { SharedGroupId = code, PlayFabIds = new List<string> { self } },
                _ => { if (onOk != null) onOk(); },
                e => { if (onError != null) onError(Map(e)); });
        }

        public void SetReady(string code, bool ready, Action onOk, Action<AuthError> onError)
        {
            string self = SelfId();
            if (string.IsNullOrEmpty(self) || string.IsNullOrEmpty(code)) { if (onError != null) onError(Err("Thieu code.")); return; }
            var blob = JsonUtility.ToJson(new PartyMemberBlob { name = SelfName(), ready = ready, avatar = SelfAvatar(), seen = Now() });
            PlayFabClientAPI.UpdateSharedGroupData(new UpdateSharedGroupDataRequest
            { SharedGroupId = code, Permission = UserDataPermission.Public, Data = new Dictionary<string, string> { { self, blob } } },
            _ => { if (onOk != null) onOk(); },
            e => { if (onError != null) onError(Map(e)); });
        }

        public void SetMatchId(string code, string matchId, Action onOk, Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(code)) { if (onError != null) onError(Err("Thieu code.")); return; }
            PlayFabClientAPI.UpdateSharedGroupData(new UpdateSharedGroupDataRequest
            { SharedGroupId = code, Permission = UserDataPermission.Public, Data = new Dictionary<string, string> { { MatchKey, JS(matchId != null ? matchId : "") } } },
            _ => { if (onOk != null) onOk(); },
            e => { if (onError != null) onError(Map(e)); });
        }

        // [Aemy 2026-10-17] Chuyen truong nhom (hoac thanh vien dau tien nhan lai khi truong nhom cu da roi).
        public void SetLeader(string code, string playFabId, Action onOk, Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(playFabId)) { if (onError != null) onError(Err("Thieu code / id.")); return; }
            PlayFabClientAPI.UpdateSharedGroupData(new UpdateSharedGroupDataRequest
            { SharedGroupId = code, Permission = UserDataPermission.Public, Data = new Dictionary<string, string> { { LeaderKey, JS(playFabId) } } },
            _ => { if (onOk != null) onOk(); },
            e => { if (onError != null) onError(Map(e)); });
        }

        // [Aemy 2026-10-17] Truong nhom ghi che do / map / moc ha guc -> thanh vien doc qua GetParty.
        public void SetSettings(string code, string settingsJson, Action onOk, Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(code)) { if (onError != null) onError(Err("Thieu code.")); return; }
            PlayFabClientAPI.UpdateSharedGroupData(new UpdateSharedGroupDataRequest
            { SharedGroupId = code, Permission = UserDataPermission.Public, Data = new Dictionary<string, string> { { SettingsKey, settingsJson ?? "" } } },
            _ => { if (onOk != null) onOk(); },
            e => { if (onError != null) onError(Map(e)); });
        }

        public void GetParty(string code, Action<PartyState> onResult, Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(code)) { if (onError != null) onError(Err("Thieu code.")); return; }
            PlayFabClientAPI.GetSharedGroupData(new GetSharedGroupDataRequest { SharedGroupId = code, GetMembers = true },
                res =>
                {
                    var st = new PartyState { Code = code };
                    if (res.Data != null)
                    {
                        if (res.Data.ContainsKey(LeaderKey)) st.LeaderId = UNJS(res.Data[LeaderKey].Value);
                        if (res.Data.ContainsKey(MatchKey)) st.MatchId = UNJS(res.Data[MatchKey].Value);
                        if (res.Data.ContainsKey(SettingsKey)) st.SettingsJson = res.Data[SettingsKey].Value;
                    }
                    if (res.Members != null)
                    {
                        foreach (var mid in res.Members)
                        {
                            var pm = new PartyMember { PlayFabId = mid, Name = mid, Ready = false };
                            if (res.Data != null && res.Data.ContainsKey(mid))
                            {
                                try { var b = JsonUtility.FromJson<PartyMemberBlob>(res.Data[mid].Value); if (b != null) { pm.Name = string.IsNullOrEmpty(b.name) ? mid : b.name; pm.Ready = b.ready; pm.AvatarUrl = b.avatar; pm.Seen = b.seen; } } catch { }
                            }
                            st.Members.Add(pm);
                        }
                    }
                    if (onResult != null) onResult(st);
                },
                e => { if (onError != null) onError(Map(e)); });
        }

        public void SendInvite(string toPlayFabId, string code, Action onOk, Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(toPlayFabId) || string.IsNullOrEmpty(code)) { if (onError != null) onError(Err("Thieu nguoi nhan / code.")); return; }
            PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
            {
                FunctionName = "SendPartyInvite",
                FunctionParameter = new { toId = toPlayFabId, code = code, fromName = SelfName(), fromId = SelfId() },
                GeneratePlayStreamEvent = false
            },
            res =>
            {
                if (res.Error != null) { if (onError != null) onError(Err("CloudScript: " + res.Error.Message)); return; }
                if (onOk != null) onOk();
            },
            e => { if (onError != null) onError(Map(e)); });
        }

        public void CheckInvite(Action<PartyInvite> onInvite, Action<AuthError> onError)
        {
            PlayFabClientAPI.GetUserData(new GetUserDataRequest { Keys = new List<string> { InviteKey } },
                res =>
                {
                    if (res.Data != null && res.Data.ContainsKey(InviteKey))
                    {
                        var inv = ParseInvite(res.Data[InviteKey].Value);
                        if (inv != null && !string.IsNullOrEmpty(inv.Code)) Debug.Log("[Party-DIAG] Nhan invite tu " + inv.FromName + " code=" + inv.Code);
                        if (onInvite != null) onInvite(inv);
                    }
                    else { if (onInvite != null) onInvite(null); }
                },
                e => { if (onError != null) onError(Map(e)); });
        }

        // [INVITE-FIX 2026-09-29 Aemy] Parse invite khoan dung ca 2 kieu ten field (Code/code) tu CloudScript.
        private static PartyInvite ParseInvite(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            PartyInvite inv = null;
            try { inv = JsonUtility.FromJson<PartyInvite>(json); } catch { inv = null; }
            if (inv != null && !string.IsNullOrEmpty(inv.Code)) return inv;
            try
            {
                var obj = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<Dictionary<string, object>>(json);
                if (obj == null) return inv;
                var r = new PartyInvite();
                r.Code = Pick(obj, "Code", "code");
                r.FromName = Pick(obj, "FromName", "fromName", "fromname");
                r.FromId = Pick(obj, "FromId", "fromId", "fromid");
                return r;
            }
            catch { return inv; }
        }
        private static string Pick(Dictionary<string, object> d, params string[] keys)
        {
            foreach (var k in keys) { object v; if (d.TryGetValue(k, out v) && v != null) return v.ToString(); }
            return null;
        }

        // [JSON-SAFE 2026-09-29] Bọc/gỡ chuỗi thô -> JSON hợp lệ (khi title bật 'Require valid JSON').
        private static string JS(string s)
        {
            if (s == null) s = "";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
        private static string UNJS(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.Length >= 2 && s[0] == '\"' && s[s.Length - 1] == '\"')
                return s.Substring(1, s.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
            return s; // tuong thich du lieu cu (chuoi tho)
        }

        public void ClearInvite(Action onOk, Action<AuthError> onError)
        {
            PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest { KeysToRemove = new List<string> { InviteKey } },
                _ => { if (onOk != null) onOk(); },
                e => { if (onError != null) onError(Map(e)); });
        }
    }
}