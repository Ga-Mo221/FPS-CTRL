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
        public List<PartyMember> Members = new List<PartyMember>();
        public bool AllReady()
        {
            if (Members == null || Members.Count == 0) return false;
            foreach (var m in Members) if (!m.Ready) return false;
            return true;
        }
    }

    [Serializable] public class PartyInvite { public string Code; public string FromName; public string FromId; }

    // blob JSON luu cho tung member trong shared group data (key = playFabId).
    [Serializable] public class PartyMemberBlob { public string name; public bool ready; public string avatar; public long seen; }
}
