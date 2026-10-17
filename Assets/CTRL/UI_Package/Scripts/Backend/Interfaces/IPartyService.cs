using System;
using Backend.Models;

namespace Backend.Interfaces
{
    // [Aemy 2026-09-21] Party + moi ban (SharedGroupData + CloudScript invite).
    public interface IPartyService
    {
        void CreateParty(Action<string> onCode, Action<AuthError> onError);       // tao party -> tra code
        void JoinParty(string code, Action onOk, Action<AuthError> onError);      // vao party theo code
        void LeaveParty(string code, Action onOk, Action<AuthError> onError);
        void SetReady(string code, bool ready, Action onOk, Action<AuthError> onError);
        void SetMatchId(string code, string matchId, Action onOk, Action<AuthError> onError); // leader ghi MatchId
        void GetParty(string code, Action<PartyState> onResult, Action<AuthError> onError);
        void SendInvite(string toPlayFabId, string code, Action onOk, Action<AuthError> onError); // qua CloudScript
        void CheckInvite(Action<PartyInvite> onInvite, Action<AuthError> onError); // onInvite(null) = khong co
        void ClearInvite(Action onOk, Action<AuthError> onError);
        void Touch(string code, bool ready, Action onOk, Action<AuthError> onError); // [PARTY-HB] heartbeat
        void KickMember(string code, string playFabId, Action onOk, Action<AuthError> onError); // [PARTY-HB] da member mat ket noi
    }
}
