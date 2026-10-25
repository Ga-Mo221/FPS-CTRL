using System;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

namespace Game.Shop
{
    /// <summary>Nguon cung cap danh sach item cho shop. Doi nguon = thay 1 impl, khong sua ShopController.</summary>
    public interface IShopCatalogSource
    {
        void Load(Action<List<ShopItemInfo>> onDone, Action<string> onError);
    }

    /// <summary>
    /// Nguon CHINH: doc JSON tren PlayFab Title Data (key mac dinh "ShopCatalog").
    /// Sua tren dashboard Game Manager la moi may cap nhat, KHONG build lai game.
    /// (Tool ShopCatalogSync co the tu day JSON nay len cung luc voi catalog.)
    /// </summary>
    public class PlayFabTitleDataCatalogSource : IShopCatalogSource
    {
        private readonly string _key;
        public PlayFabTitleDataCatalogSource(string key) { _key = string.IsNullOrEmpty(key) ? "ShopCatalog" : key; }

        public void Load(Action<List<ShopItemInfo>> onDone, Action<string> onError)
        {
            PlayFabClientAPI.GetTitleData(
                new GetTitleDataRequest { Keys = new List<string> { _key } },
                res =>
                {
                    string json;
                    if (res != null && res.Data != null && res.Data.TryGetValue(_key, out json) && !string.IsNullOrEmpty(json))
                    {
                        var items = ShopCatalogJson.Parse(json);
                        if (onDone != null) onDone(items);
                    }
                    else
                    {
                        if (onError != null) onError("Title Data key '" + _key + "' trong hoac khong ton tai.");
                    }
                },
                err => { if (onError != null) onError(err != null ? err.ErrorMessage : "GetTitleData loi."); });
        }
    }

    /// <summary>Nguon DU PHONG (offline/first-run): doc tu 1 TextAsset JSON bundle san trong game.</summary>
    public class LocalDocCatalogSource : IShopCatalogSource
    {
        private readonly TextAsset _doc;
        public LocalDocCatalogSource(TextAsset doc) { _doc = doc; }

        public void Load(Action<List<ShopItemInfo>> onDone, Action<string> onError)
        {
            if (_doc == null || string.IsNullOrEmpty(_doc.text))
            {
                if (onError != null) onError("Khong co file doc local.");
                return;
            }
            var items = ShopCatalogJson.Parse(_doc.text);
            if (onDone != null) onDone(items);
        }
    }

    /// <summary>
    /// Facade: thu nguon CHINH truoc (Title Data), that bai/rong thi fallback sang doc LOCAL.
    /// Tra ve callback (items, fromServer). Luon co gang co du lieu de shop khong bao gio trong.
    /// </summary>
    public static class ShopCatalogService
    {
        public static void LoadWithFallback(string titleKey, TextAsset localDoc, Action<List<ShopItemInfo>, bool> onDone)
        {
            Action<List<ShopItemInfo>> finishLocal = items =>
            {
                if (onDone != null) onDone(items ?? new List<ShopItemInfo>(), false);
            };

            var primary = new PlayFabTitleDataCatalogSource(titleKey);
            primary.Load(
                items =>
                {
                    if (items != null && items.Count > 0) { if (onDone != null) onDone(items, true); }
                    else FallbackLocal(localDoc, finishLocal); // server rong -> thu local
                },
                errMsg =>
                {
                    Debug.LogWarning("[ShopCatalogService] Title Data loi, dung doc local. (" + errMsg + ")");
                    FallbackLocal(localDoc, finishLocal);
                });
        }

        private static void FallbackLocal(TextAsset localDoc, Action<List<ShopItemInfo>> done)
        {
            var local = new LocalDocCatalogSource(localDoc);
            local.Load(
                items => done(items),
                errMsg =>
                {
                    Debug.LogWarning("[ShopCatalogService] Khong co du lieu shop (server + local deu trong). " + errMsg);
                    done(new List<ShopItemInfo>());
                });
        }
    }
}
