using System;
using System.Collections.Generic;
using Backend.Models;

namespace Backend.Interfaces
{
    /// <summary>
    /// Chat ngoai menu (to doi + nhan rieng ban be).
    /// Mo hinh "hop thu di": moi nguoi chi ghi hop thu CUA MINH, nguoi nhan doc (polling) hop thu cua ben kia.
    ///  - To doi: moi thanh vien 1 key trong SharedGroup cua party.
    ///  - Nhan rieng: 1 key Public trong UserData cua nguoi gui (ban be doc duoc).
    /// </summary>
    public interface IChatService
    {
        /// <summary>Ghi de hop thu di cua minh trong party (danh sach tin gan nhat minh da gui vao party).</summary>
        void PublishPartyOutbox(string partyCode, List<ChatMessage> mine, Action onOk, Action<AuthError> onError);

        /// <summary>Doc hop thu di cua cac thanh vien party (gom ca minh).</summary>
        void FetchParty(string partyCode, IList<string> memberIds, Action<List<ChatMessage>> onResult, Action<AuthError> onError);

        /// <summary>Ghi de hop thu nhan rieng cua minh (tin minh gui cho moi ban, moi tin co truong o = nguoi nhan).</summary>
        void PublishDirectOutbox(List<ChatMessage> mine, Action onOk, Action<AuthError> onError);

        /// <summary>Doc hop thu nhan rieng cua 1 nguoi choi (ban be, hoac chinh minh de khoi phuc tin da gui).</summary>
        void FetchDirectOutbox(string playFabId, Action<List<ChatMessage>> onResult, Action<AuthError> onError);
    }
}
