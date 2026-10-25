// Danh muc nhan vat cho man CHON NHAN VAT 
// Nguon: PlayFab Title Data key "Characters" (JSON { "characters":[ {id,name,desc,previewUrl} ] }) - chinh qua Google Sheet nhu Avatars.
// Title Data trong/loi -> dung danh sach mac dinh built-in (Ghost/Olivia) de man chon luon chay.
// LUU Y: id phai KHOP voi NPlayer.Name cua prefab nhan vat (GameManager._character_Prefabs) de spawn dung.
using System;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

namespace Backend.PlayfabImpl
{
    [Serializable]
    public class CharacterInfo
    {
        public string id;          // = NPlayer.Name (vd "Ghost", "Olivia")
        public string name;        // ten hien thi
        public string desc;        // mo ta ngan (flavor, khong lien quan chi so)
        public string previewUrl;  // anh preview to (http URL hoac key PlayFab Content); trong = placeholder
    }

    [Serializable]
    public class CharacterCatalog
    {
        public List<CharacterInfo> characters = new List<CharacterInfo>();
    }

    public static class CharacterCatalogService
    {
        public const string TitleKey = "Characters";

        public static List<CharacterInfo> Loaded { get; private set; }

        /// <summary>Load danh muc nhan vat (cache -> Title Data -> mac dinh). Luon tra list khong rong.</summary>
        public static void LoadList(Action<List<CharacterInfo>> onDone)
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
                    List<CharacterInfo> list = null;
                    if (res != null && res.Data != null && res.Data.ContainsKey(TitleKey) && !string.IsNullOrEmpty(res.Data[TitleKey]))
                    {
                        try
                        {
                            var c = JsonUtility.FromJson<CharacterCatalog>(res.Data[TitleKey]);
                            if (c != null && c.characters != null && c.characters.Count > 0) list = c.characters;
                        }
                        catch (Exception e) { Debug.LogWarning("[CharacterCatalog] Parse loi: " + e.Message); }
                    }
                    if (list == null || list.Count == 0) list = DefaultList();
                    Loaded = list;
                    if (onDone != null) onDone(list);
                },
                err =>
                {
                    Loaded = DefaultList();
                    Debug.Log("<color=#22d3ee>[Characters]</color> Title Data loi/thieu -> dung mac dinh (" + err.ErrorMessage + ").");
                    if (onDone != null) onDone(Loaded);
                });
        }

        /// <summary>Tim info theo id (= NPlayer.Name), khong phan biet hoa thuong. Null neu khong co.</summary>
        public static CharacterInfo Find(string id)
        {
            if (Loaded == null || string.IsNullOrEmpty(id)) return null;
            foreach (var c in Loaded)
                if (c != null && !string.IsNullOrEmpty(c.id) && string.Equals(c.id, id, StringComparison.OrdinalIgnoreCase))
                    return c;
            // Du phong: bo khoang trang / gach (sheet ghi "AnhTeo", prefab ten "Anh Teo").
            string key = Normalize(id);
            foreach (var c in Loaded)
                if (c != null && !string.IsNullOrEmpty(c.id) && Normalize(c.id) == key)
                    return c;
            return null;
        }

        private static string Normalize(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var ch in s) if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }

        /// <summary>Mac dinh 2 nhan vat (khop CharacterName._GHOST/_OLIVIA). Khong co anh -> man chon dung placeholder.</summary>
        public static List<CharacterInfo> DefaultList()
        {
            return new List<CharacterInfo>
            {
                new CharacterInfo { id = "Ghost",  name = "GHOST",  desc = "Đặc vụ bóng đêm — lạnh lùng, bí ẩn.",   previewUrl = "" },
                new CharacterInfo { id = "Olivia", name = "OLIVIA", desc = "Tay súng linh hoạt — nhanh, quyết đoán.", previewUrl = "" },
            };
        }
    }
}
