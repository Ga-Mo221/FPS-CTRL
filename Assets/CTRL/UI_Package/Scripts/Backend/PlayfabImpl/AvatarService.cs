// [Aemy 2026-09-27] Danh sach avatar preset cho nguoi choi chon.
// Nguon: PlayFab Title Data key "Avatars" (JSON { "avatars":[ {id,name,url} ] }) - chinh qua Google Sheet nhu Weapons/Shop.
// Neu Title Data trong/loi -> dung danh sach mac dinh built-in (robohash) de picker luon co avatar de chon.
using System;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

namespace Backend.PlayfabImpl
{
    [Serializable]
    public class AvatarInfo
    {
        public string id;
        public string name;
        public string url;
    }

    [Serializable]
    public class AvatarCatalog
    {
        public List<AvatarInfo> avatars = new List<AvatarInfo>();
    }

    public static class AvatarService
    {
        public const string TitleKey = "Avatars";

        // Cache danh sach da load gan nhat.
        public static List<AvatarInfo> Loaded { get; private set; }

        /// <summary>Load danh sach avatar (uu tien cache -> Title Data -> mac dinh). Luon tra list khong rong.</summary>
        public static void LoadList(Action<List<AvatarInfo>> onDone)
        {
            if (Loaded != null && Loaded.Count > 0)
            {
                if (onDone != null) onDone(Loaded);
                return;
            }

            PlayFabClientAPI.GetTitleData(
                new GetTitleDataRequest { Keys = new List<string> { TitleKey } },
                res =>
                {
                    List<AvatarInfo> list = null;
                    if (res != null && res.Data != null && res.Data.ContainsKey(TitleKey) && !string.IsNullOrEmpty(res.Data[TitleKey]))
                    {
                        try
                        {
                            var c = JsonUtility.FromJson<AvatarCatalog>(res.Data[TitleKey]);
                            if (c != null && c.avatars != null && c.avatars.Count > 0) list = c.avatars;
                        }
                        catch (Exception e) { Debug.LogWarning("[AvatarService] Parse loi: " + e.Message); }
                    }
                    if (list == null || list.Count == 0) list = DefaultList();
                    Loaded = list;
                    if (onDone != null) onDone(list);
                },
                err =>
                {
                    Loaded = DefaultList();
                    Debug.Log("<color=#22d3ee>[Avatars]</color> Title Data loi/thieu -> dung mac dinh (" + err.ErrorMessage + ").");
                    if (onDone != null) onDone(Loaded);
                });
        }

        /// <summary>Bo avatar mac dinh (robohash PNG, khong can host). Nyx thay bang bo rieng qua Sheet sau.</summary>
        public static List<AvatarInfo> DefaultList()
        {
            string[] seeds = new string[] { "Vesper", "Umbra", "Dao", "Nova", "Apex", "Flare", "Dusk", "Comet", "Nyx", "Aemy", "Solaris", "Eclipse" };
            string[] sets = new string[] { "set1", "set2", "set3", "set4", "set5" };
            var list = new List<AvatarInfo>();
            for (int i = 0; i < seeds.Length; i++)
            {
                string set = sets[i % sets.Length];
                list.Add(new AvatarInfo
                {
                    id = "def_" + seeds[i].ToLowerInvariant(),
                    name = seeds[i],
                    url = "https://robohash.org/" + seeds[i] + ".png?set=" + set + "&size=256x256"
                });
            }
            return list;
        }
    }
}
