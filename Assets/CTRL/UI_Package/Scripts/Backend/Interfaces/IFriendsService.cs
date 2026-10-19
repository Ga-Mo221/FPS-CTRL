using System;
using System.Collections.Generic;
using Backend.Models;

namespace Backend.Interfaces
{
    // Danh sách bạn bè của người chơi (PlayFab Friends).
    public interface IFriendsService
    {
        // ↓ LẤY VỀ: danh sách bạn bè (kèm tên hiển thị + avatar url).
        void GetFriends(Action<List<FriendInfo>> onResult, Action<AuthError> onError);

        //  Danh sach ban + presence (online/away/offline) suy tu LastSeen.
        void GetFriendsWithPresence(Action<List<FriendInfo>> onResult, Action<AuthError> onError);

        //Them ban theo Ten hien thi HOAC PlayFabId.
        void AddFriend(string displayNameOrId, Action onSuccess, Action<AuthError> onError);

        // Xoa ban theo PlayFabId.
        void RemoveFriend(string playFabId, Action onSuccess, Action<AuthError> onError);

        // Heartbeat: ghi LastSeen (epoch phut) de ban be thay minh online.
        void Heartbeat(Action onSuccess = null, Action<AuthError> onError = null);

        // ===== KET BAN 2 CHIEU (request -> accept) qua CloudScript =====
        // Dang ky ten minh vao chi muc de nguoi khac tim duoc (goi sau login / doi ten).
        void IndexMyName(Action onDone = null, Action<AuthError> onError = null);

        // Gui loi moi ket ban toi 'query' (ten hien thi HOAC PlayFabId). onDone tra message hien cho UI.
        void SendFriendRequest(string query, Action<string> onDone, Action<AuthError> onError);

        // Lay danh sach loi moi DEN minh (cho popup Accept/Decline).
        void GetIncomingRequests(Action<List<FriendRequestInfo>> onResult, Action<AuthError> onError);

        // Chap nhan -> ket ban 2 chieu.
        void AcceptFriendRequest(string fromId, Action onSuccess, Action<AuthError> onError);

        // Tu choi loi moi.
        void DeclineFriendRequest(string fromId, Action onSuccess, Action<AuthError> onError);
    }
}
