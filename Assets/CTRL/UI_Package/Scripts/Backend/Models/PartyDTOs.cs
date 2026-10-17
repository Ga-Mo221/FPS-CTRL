using System;
using System.Collections.Generic;

namespace Backend.Models
{
    // [Aemy 2026-09-21] Party (dua tren PlayFab SharedGroupData).
    [Serializable] public class PartyMember { public string PlayFabId; public string Name; public bool Ready; public string AvatarUrl; public long Seen; }

    [Serializable] public class PartyState
    {
        public string Code;
        public string LeaderId;
        public string MatchId;
        public string SettingsJson; // [Aemy 2026-10-17] che do / map / moc ha guc do truong nhom chon (dong bo cho thanh vien)
        public List<PartyMember> Members = new List<PartyMember>();
        public bool HasMember(string playFabId)
        {
            if (Members == null || string.IsNullOrEmpty(playFabId)) return false;
            foreach (var m in Members) if (m != null && m.PlayFabId == playFabId) return true;
            return false;
        }
        public bool AllReady()
        {
            if (Members == null || Members.Count == 0) return false;
            foreach (var m in Members) if (!m.Ready) return false;
            return true;
        }
    }

    // [Aemy 2026-10-17] Cai dat tran cua truong nhom -> thanh vien chi xem (khong tu chon).
    [Serializable] public class PartySettings
    {
        public int modeIndex;     // index nut CHE DO trong PlayPanel
        public int mapIndex;      // index o BAN DO
        public int killTarget;    // Custom: moc ha guc (0 = mac dinh)
        public string customRule; // Custom: "TDM" / "FFA"
        public bool SameAs(PartySettings o)
        {
            return o != null && o.modeIndex == modeIndex && o.mapIndex == mapIndex && o.killTarget == killTarget && o.customRule == customRule;
        }
    }

    [Serializable] public class PartyInvite { public string Code; public string FromName; public string FromId; }

    // blob JSON luu cho tung member trong shared group data (key = playFabId).
    [Serializable] public class PartyMemberBlob { public string name; public bool ready; public string avatar; public long seen; }
}
