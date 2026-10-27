// [Aemy 2026-10-27] FILE MOI. Model xem truoc cua tung nhan vat (tab NHAN VAT o MenuShell).
using System;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Truoc day panel Nhan vat lay model tu prefab mang (NPlayer -> child _FBX) => keo theo Animator gameplay
    /// (Gost CTRL) + constraint khong chay ngoai tran -> sai tu the. Nay moi nhan vat co:
    /// - model = file FBX goc (khong script, khong constraint),
    /// - controller rieng chay clip POSE cua nhan vat do,
    /// - an cac mesh khong thuoc nhan vat (luu dan Flash / Frag dinh tren nguoi).
    /// Asset: Resources/CharacterPreviewCatalog. characterId = NPlayer.Name ("Anh Teo", "Ghost", "Olivia").
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Character Preview Catalog", fileName = "CharacterPreviewCatalog")]
    public class CharacterPreviewCatalog : ScriptableObject
    {
        public const string ResourcePath = "CharacterPreviewCatalog";

        [Serializable]
        public class Entry
        {
            [Tooltip("Trung NPlayer.Name cua prefab nhan vat (vd \"Anh Teo\").")]
            public string characterId;
            [Tooltip("File FBX goc cua nhan vat.")]
            public GameObject model;
            [Tooltip("Animator controller rieng cho man xem truoc (chay clip POSE).")]
            public RuntimeAnimatorController controller;
        }

        [SerializeField] private Entry[] _entries = new Entry[0];
        [Tooltip("Ten cac mesh (object) bi an tren moi model xem truoc.")]
        [SerializeField] private string[] _hiddenMeshes = { "Flash", "Frag" };

        private static CharacterPreviewCatalog _instance;
        private static bool _loaded;

        public static CharacterPreviewCatalog Instance
        {
            get
            {
                if (!_loaded) { _instance = Resources.Load<CharacterPreviewCatalog>(ResourcePath); _loaded = true; }
                return _instance;
            }
        }

        /// <summary>Tim entry theo id nhan vat (khop chinh xac, roi khop bo dau cach / hoa thuong: "AnhTeo" ~ "Anh Teo").</summary>
        public static Entry Find(string characterId)
        {
            var cat = Instance;
            if (cat == null || cat._entries == null || string.IsNullOrEmpty(characterId)) return null;
            foreach (var e in cat._entries)
                if (e != null && e.model != null && string.Equals(e.characterId, characterId, StringComparison.OrdinalIgnoreCase)) return e;
            string k = Norm(characterId);
            foreach (var e in cat._entries)
                if (e != null && e.model != null && Norm(e.characterId) == k) return e;
            return null;
        }

        /// <summary>Goi tren BAN SAO vua Instantiate tu entry.model: gan controller POSE, ap tu the ngay, an mesh thua.</summary>
        public static void Prepare(GameObject instance, Entry entry)
        {
            if (instance == null) return;
            var cat = Instance;
            if (cat != null && cat._hiddenMeshes != null)
                foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                    foreach (var n in cat._hiddenMeshes)
                        if (!string.IsNullOrEmpty(n) && r.gameObject.name == n) { r.gameObject.SetActive(false); break; }

            var anim = instance.GetComponentInChildren<Animator>(true);
            if (anim == null) return;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (entry != null && entry.controller != null) anim.runtimeAnimatorController = entry.controller;
            if (anim.isActiveAndEnabled) anim.Update(0f); // ap POSE ngay -> canh khung (bounds) theo dung tu the
        }

        private static string Norm(string s)
        {
            var sb = new System.Text.StringBuilder();
            if (s != null) foreach (var ch in s) if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }
    }
}
