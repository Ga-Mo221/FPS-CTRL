using System;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using Backend.Interfaces;
using Backend.Models;

namespace Backend.PlayfabImpl
{
    /// <summary>
    /// [Aemy 2026-10-19] IChatService tren PlayFab, KHONG can CloudScript:
    ///  - To doi: SharedGroupData cua party, key "chat_&lt;PlayFabId&gt;" (moi thanh vien ghi key cua minh).
    ///  - Nhan rieng: UserData key "chat_dm" (Permission Public) cua nguoi gui; nguoi nhan GetUserData(PlayFabId).
    /// Luu y: du lieu Public -> ai biet PlayFabId cung doc duoc (phu hop chat ban be trong game, khong dung cho tin mat).
    /// </summary>
    public class PlayFabChatService : IChatService
    {
        public const string PartyKeyPrefix = "chat_";
        public const string DirectKey = "chat_dm";

        public static string PartyKey(string playFabId) { return PartyKeyPrefix + playFabId; }

        public void PublishPartyOutbox(string partyCode, List<ChatMessage> mine, Action onOk, Action<AuthError> onError)
        {
            string self = SelfId();
            if (string.IsNullOrEmpty(self) || string.IsNullOrEmpty(partyCode)) { if (onError != null) onError(Err("Chua dang nhap / chua co to doi.")); return; }
            PlayFabClientAPI.UpdateSharedGroupData(new UpdateSharedGroupDataRequest
            {
                SharedGroupId = partyCode,
                Permission = UserDataPermission.Public,
                Data = new Dictionary<string, string> { { PartyKey(self), ToJson(mine) } }
            },
            _ => { if (onOk != null) onOk(); },
            e => { if (onError != null) onError(Map(e)); });
        }

        public void FetchParty(string partyCode, IList<string> memberIds, Action<List<ChatMessage>> onResult, Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(partyCode) || memberIds == null || memberIds.Count == 0) { if (onResult != null) onResult(new List<ChatMessage>()); return; }
            var keys = new List<string>();
            foreach (var id in memberIds) if (!string.IsNullOrEmpty(id)) keys.Add(PartyKey(id));
            PlayFabClientAPI.GetSharedGroupData(new GetSharedGroupDataRequest { SharedGroupId = partyCode, Keys = keys, GetMembers = false },
                res =>
                {
                    var all = new List<ChatMessage>();
                    if (res.Data != null)
                        foreach (var kv in res.Data)
                            if (kv.Key.StartsWith(PartyKeyPrefix) && kv.Value != null) all.AddRange(FromJson(kv.Value.Value));
                    if (onResult != null) onResult(all);
                },
                e => { if (onError != null) onError(Map(e)); });
        }

        public void PublishDirectOutbox(List<ChatMessage> mine, Action onOk, Action<AuthError> onError)
        {
            PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest
            {
                Permission = UserDataPermission.Public,
                Data = new Dictionary<string, string> { { DirectKey, ToJson(mine) } }
            },
            _ => { if (onOk != null) onOk(); },
            e => { if (onError != null) onError(Map(e)); });
        }

        public void FetchDirectOutbox(string playFabId, Action<List<ChatMessage>> onResult, Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(playFabId)) { if (onResult != null) onResult(new List<ChatMessage>()); return; }
            PlayFabClientAPI.GetUserData(new GetUserDataRequest { PlayFabId = playFabId, Keys = new List<string> { DirectKey } },
                res =>
                {
                    var list = new List<ChatMessage>();
                    UserDataRecord rec;
                    if (res.Data != null && res.Data.TryGetValue(DirectKey, out rec) && rec != null) list = FromJson(rec.Value);
                    if (onResult != null) onResult(list);
                },
                e => { if (onError != null) onError(Map(e)); });
        }

        // ---------------------------------------------------------------- helpers
        private static string SelfId()
        {
            return Backend.Bootstrap.BackendBootstrap.AuthService != null ? Backend.Bootstrap.BackendBootstrap.AuthService.CurrentUserId : null;
        }

        private static string ToJson(List<ChatMessage> msgs)
        {
            var box = new ChatOutbox();
            if (msgs != null) box.m.AddRange(msgs);
            return JsonUtility.ToJson(box);
        }

        private static List<ChatMessage> FromJson(string json)
        {
            var list = new List<ChatMessage>();
            if (string.IsNullOrEmpty(json)) return list;
            try
            {
                var box = JsonUtility.FromJson<ChatOutbox>(json);
                if (box != null && box.m != null)
                    foreach (var msg in box.m)
                        if (msg != null && !string.IsNullOrEmpty(msg.f) && !string.IsNullOrEmpty(msg.x)) list.Add(msg);
            }
            catch (Exception ex) { Debug.LogWarning("[Chat] Blob chat hong: " + ex.Message); }
            return list;
        }

        private static AuthError Map(PlayFabError e) { return new AuthError { ErrorCode = e.Error.ToString(), ErrorMessage = e.ErrorMessage }; }
        private static AuthError Err(string msg) { return new AuthError { ErrorCode = "Local", ErrorMessage = msg }; }
    }
}
