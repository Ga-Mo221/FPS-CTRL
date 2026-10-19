using System;
using System.Collections.Generic;

namespace Backend.Models
{
    /// <summary>
    /// [Aemy 2026-10-19] Mot tin nhan chat ngoai menu (to doi hoac nhan rieng ban be).
    /// Ten field ngan de blob JSON tren PlayFab gon (gioi han kich thuoc moi key).
    /// </summary>
    [Serializable]
    public class ChatMessage
    {
        public string f;  // PlayFabId nguoi gui
        public string n;  // ten hien thi nguoi gui
        public string o;  // PlayFabId nguoi nhan (nhan rieng) - "" voi tin to doi
        public string x;  // noi dung
        public long t;    // epoch mili-giay (UTC) luc gui

        public string Id { get { return f + "-" + t; } }
    }

    /// <summary>Hop thu di cua 1 nguoi choi: chi chu so huu ghi, nguoi khac chi doc (khong tranh ghi de nhau).</summary>
    [Serializable]
    public class ChatOutbox
    {
        public List<ChatMessage> m = new List<ChatMessage>();
    }
}
