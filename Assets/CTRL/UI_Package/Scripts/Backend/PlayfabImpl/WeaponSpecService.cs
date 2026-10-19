// Load chi so sung tu PlayFab Title Data ('WeaponSpecs') va overlay len WeaponData luc runtime.
// - KHONG can build lai: sua sheet -> Apps Script day len Title Data -> tran sau tu dong ap.
// - Host-authoritative: damage duoc host doc thang tu WeaponData khi tinh hit, nen overlay o host = so co hieu luc.
//   Client cung overlay (tu Title Data) de feel dung (fireDelay/recoil/ADS) + hien UI. Damage van do host quyet.
// - Vi WeaponData la ScriptableObject dung chung, overlay 1 lan la moi instance sung deu nhan so moi.
using System;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

namespace Backend.PlayfabImpl
{
    public static class WeaponSpecService
    {
        public const string TitleKey = "WeaponSpecs";

        // Bang chi so da load gan nhat (cache). UI co the doc iconUrl/imageUrl tu day.
        public static WeaponSpecTable Loaded { get; private set; }

        // Sheet dung CODENAME -> map ve _iD that cua WeaponData (lead khop bang _iD.Contains).
        // Khong co trong map thi tra nguyen (nen ID that van chay). Nyx doi codename chi can sua o day + o sheet.
        private static readonly System.Collections.Generic.Dictionary<string,string> CodenameToId =
            new System.Collections.Generic.Dictionary<string,string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "Dao",    "Knife"     },   // ten cu; o Trang bi nay ghi "Knife" (= ID)
            { "Vesper", "9mm-Demon" },
            { "Umbra",  "M4"        },
            { "Corona", "M24"       },
            { "Nova",   "Remington" },
        };
        private static string ResolveKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return CodenameToId.TryGetValue(name.Trim(), out var id) ? id : name;
        }

        /// <summary>Load tu Title Data roi overlay len WeaponData. onApplied(so sung da ap).</summary>
        public static void LoadAndApply(Action<int> onApplied = null, Action<string> onError = null)
        {
            if (!PlayFabClientAPI.IsClientLoggedIn()) { Debug.LogWarning("[WeaponSpecs] Chua dang nhap -> bo qua."); if (onError != null) onError("not logged in"); return; }
            PlayFabClientAPI.GetTitleData(
                new GetTitleDataRequest { Keys = new List<string> { TitleKey } },
                res =>
                {
                    if (res.Data != null && res.Data.ContainsKey(TitleKey) && !string.IsNullOrEmpty(res.Data[TitleKey]))
                    {
                        WeaponSpecTable table = null;
                        try { table = JsonUtility.FromJson<WeaponSpecTable>(res.Data[TitleKey]); }
                        catch (Exception ex) { if (onError != null) onError("Parse WeaponSpecs loi: " + ex.Message); return; }

                        Loaded = table;
                        int n = Apply(table);
                        Debug.Log("<color=#22d3ee>[WeaponSpecs]</color> Da ap " + n + " sung tu Title Data.");
                        if (onApplied != null) onApplied(n);
                    }
                    else
                    {
                        Debug.Log("<color=#22d3ee>[WeaponSpecs]</color> Title Data chua co key '" + TitleKey + "' -> dung so mac dinh (SO).");
                        if (onApplied != null) onApplied(0);
                    }
                },
                e => { if (onError != null) onError(e.ErrorMessage); });
        }

        /// <summary>Ap bang chi so da cache (dong bo, khong goi mang) - dung khi vao tran.</summary>
        public static int ApplyCached()
        {
            return Apply(Loaded);
        }

        public static int Apply(WeaponSpecTable table)
        {
            if (table == null || table.weapons == null || GameManager.Instance == null) return 0;
            int applied = 0;
            foreach (var e in table.weapons)
            {
                if (e == null || string.IsNullOrEmpty(e.name)) continue;
                WeaponData data = GameManager.Instance.GetWeaponData(ResolveKey(e.name));
                if (data != null)
                {
                    data.ApplyRuntimeOverride(e);
                    applied++;
                    if (!string.IsNullOrEmpty(e.iconUrl))
                        Game.Shop.ShopSpriteLoader.Load(e.iconUrl, spr => { if (spr != null) data.SetRuntimeIcon(spr); });
                    if (!string.IsNullOrEmpty(e.imageUrl))
                        Game.Shop.ShopSpriteLoader.Load(e.imageUrl, spr => { if (spr != null) data.SetRuntimeImage(spr); });
                }
            }
            return applied;
        }

        /// <summary>Tai icon/anh cua sung 'name' vao Image (async). big=true -> imageUrl; khong co url thi giu nguyen sprite hien tai.</summary>
        public static void LoadInto(string name, UnityEngine.UI.Image target, bool big)
        {
            if (target == null) return;
            var e = GetEntry(name);
            if (e == null) return;
            string url = big ? e.imageUrl : e.iconUrl;
            if (string.IsNullOrEmpty(url)) return;
            Game.Shop.ShopSpriteLoader.Load(url, spr => { if (spr != null && target != null) { target.sprite = spr; target.enabled = true; } });
        }

        /// <summary>Tra entry theo ten (cho UI doc iconUrl/imageUrl/chi so hien thi).</summary>
        public static WeaponSpecEntry GetEntry(string name)
        {
            if (Loaded == null || Loaded.weapons == null || string.IsNullOrEmpty(name)) return null;
            foreach (var e in Loaded.weapons)
                if (e != null && !string.IsNullOrEmpty(e.name) && e.name == name) return e;
            return null;
        }
    }
}
