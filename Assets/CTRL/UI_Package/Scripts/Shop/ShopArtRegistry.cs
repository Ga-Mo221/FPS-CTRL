using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Shop
{
    /// <summary>
    /// KHO ANH LOCAL cua shop. Vi anh la asset Unity (khong the tai qua JSON/PlayFab Title Data),
    /// registry nay lam 2 viec:
    ///   1) 'defaultModel' / 'defaultArt': anh MAC DINH bake san trong game — hien khi item
    ///      chua co anh rieng, dang tai, hoac tai loi/offline. Nho vay card KHONG bao gio trong.
    ///   2) 'localArt': map itemId -> anh art/model co san trong project (fallback offline / first-run,
    ///      hoac cho item khong dung anh tai dong).
    ///
    /// Luong runtime: ShopController hoi registry lay anh local truoc (hien ngay), roi neu item co
    /// artUrl/modelUrl thi ShopSpriteLoader tai ve va thay the. Thieu tat ca -> defaultModel/defaultArt.
    ///
    /// Tao asset: Create > Eclipse > Shop Art Registry. Gan vao ShopController._artRegistry.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopArtRegistry", menuName = "Eclipse/Shop Art Registry", order = 1)]
    public class ShopArtRegistry : ScriptableObject
    {
        [Header("Anh mac dinh (bake san lam fallback/placeholder)")]
        [Tooltip("Anh MODEL sung mac dinh — dung khi item khong co anh model rieng / dang tai / loi.")]
        public Sprite defaultModel;
        [Tooltip("Anh NEN card mac dinh (tuy chon). Trong -> card tu dung nen theo do hiem.")]
        public Sprite defaultArt;

        [Serializable]
        public struct Entry
        {
            public string itemId;
            public Sprite art;
            public Sprite modelArt;
        }

        [Header("Anh local theo itemId (fallback offline / khong tai dong)")]
        public List<Entry> localArt = new List<Entry>();

        private Dictionary<string, Entry> _map;

        private void BuildMap()
        {
            _map = new Dictionary<string, Entry>();
            if (localArt == null) return;
            foreach (var e in localArt)
                if (!string.IsNullOrEmpty(e.itemId) && !_map.ContainsKey(e.itemId))
                    _map[e.itemId] = e;
        }

        /// <summary>Anh nen local cho itemId (null neu khong co -> caller tu xu ly default/rarity).</summary>
        public Sprite GetArt(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (_map == null || !Application.isPlaying) BuildMap();
            Entry e;
            if (_map.TryGetValue(itemId, out e) && e.art != null) return e.art;
            return null;
        }

        /// <summary>Anh model local cho itemId; khong co -> tra defaultModel (co the null).</summary>
        public Sprite GetModel(string itemId)
        {
            if (!string.IsNullOrEmpty(itemId))
            {
                if (_map == null || !Application.isPlaying) BuildMap();
                Entry e;
                if (_map.TryGetValue(itemId, out e) && e.modelArt != null) return e.modelArt;
            }
            return defaultModel;
        }

        /// <summary>Tien ich: them/cap nhat 1 entry (dung khi auto-populate tu ShopItemData trong Editor).</summary>
        public void SetLocal(string itemId, Sprite art, Sprite modelArt)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            if (localArt == null) localArt = new List<Entry>();
            for (int i = 0; i < localArt.Count; i++)
            {
                if (localArt[i].itemId == itemId)
                {
                    localArt[i] = new Entry { itemId = itemId, art = art, modelArt = modelArt };
                    _map = null;
                    return;
                }
            }
            localArt.Add(new Entry { itemId = itemId, art = art, modelArt = modelArt });
            _map = null;
        }
    }
}
