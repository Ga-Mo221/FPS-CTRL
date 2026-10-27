#if UNITY_EDITOR
using System.Collections.Generic;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Shop.EditorTools
{
    /// <summary>
    /// EDITOR-ONLY: tự động đồng bộ danh sách ShopItemData -> PlayFab.
    ///   1) Admin/SetCatalogItems  -> Catalog 'Main' (giá để validate lệnh mua, chống hack).
    ///   2) Admin/SetTitleData     -> key "ShopCatalog" chứa JSON hiển thị (ShopItemInfo) cho shop ĐỘNG.
    /// Cả hai đẩy từ CÙNG nguồn ShopItemData nên giá + hiển thị luôn khớp, không lệch.
    /// Thêm item mới trong Unity KHÔNG cần lên web PlayFab gõ tay.
    ///
    /// Nguồn secret key (theo thứ tự ưu tiên):
    ///   1) EditorPrefs (đặt qua menu "Set Secret Key…") — chỉ máy dev này, không vào repo/build.
    ///   2) Biến môi trường PLAYFAB_SECRET — cho CI/pipeline (không commit key).
    ///
    /// Menu: Eclipse Strike/Shop/... — Sync ngay, bật Auto-Sync khi Play, set/xoá key.
    /// Script ở folder Editor/ + #if UNITY_EDITOR nên KHÔNG bao giờ vào bản build game.
    /// </summary>
    [InitializeOnLoad]
    public static class ShopCatalogSync
    {
        private const string TitleId = "1D2C2B";
        private const string CatalogVersion = "Main";
        private const string TitleDataKey = "ShopCatalog";
        public const string KeyPref = "EclipseStrike.PlayFabSecretKey";
        private const string AutoPref = "EclipseStrike.ShopAutoSyncOnPlay";
        private const string EnvVar = "PLAYFAB_SECRET";
        private const string GoldCode = "GC";
        private const string ArenaCode = "AC";

        static ShopCatalogSync()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.ExitingEditMode && EditorPrefs.GetBool(AutoPref, false))
                SyncCatalog(true);
        }

        [MenuItem("Eclipse Strike/Shop/Sync Catalog → PlayFab")]
        public static void SyncMenu() { SyncCatalog(false); }

        [MenuItem("Eclipse Strike/Shop/Auto-Sync On Play")]
        private static void ToggleAuto() { EditorPrefs.SetBool(AutoPref, !EditorPrefs.GetBool(AutoPref, false)); }

        [MenuItem("Eclipse Strike/Shop/Auto-Sync On Play", true)]
        private static bool ToggleAutoValidate()
        {
            Menu.SetChecked("Eclipse Strike/Shop/Auto-Sync On Play", EditorPrefs.GetBool(AutoPref, false));
            return true;
        }

        [MenuItem("Eclipse Strike/Shop/Set Secret Key…")]
        private static void SetKey() { ShopSecretKeyWindow.Open(); }

        [MenuItem("Eclipse Strike/Shop/Clear Secret Key (this machine)")]
        private static void ClearKey()
        {
            EditorPrefs.DeleteKey(KeyPref);
            Debug.Log("[ShopCatalogSync] Đã xoá PlayFab secret key khỏi máy này.");
        }

        // Ưu tiên EditorPrefs (máy dev); trống thì lấy biến môi trường PLAYFAB_SECRET (CI/team).
        public static string ResolveSecret()
        {
            string k = EditorPrefs.GetString(KeyPref, string.Empty);
            if (!string.IsNullOrEmpty(k)) return k;
            string env = System.Environment.GetEnvironmentVariable(EnvVar);
            return string.IsNullOrEmpty(env) ? null : env.Trim();
        }

        /// <summary>Quét toàn bộ ShopItemData và đẩy lên PlayFab: Catalog (giá) + Title Data (hiển thị).</summary>
        public static void SyncCatalog(bool silent)
        {
            string secret = ResolveSecret();
            if (string.IsNullOrEmpty(secret))
            {
                Debug.LogError("[ShopCatalogSync] Chưa có PlayFab Secret Key. Đặt qua menu 'Set Secret Key…' hoặc biến môi trường " + EnvVar + ".");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:ShopItemData");
            var items = new List<ShopItemData>();
            foreach (var g in guids)
            {
                var it = AssetDatabase.LoadAssetAtPath<ShopItemData>(AssetDatabase.GUIDToAssetPath(g));
                if (it != null && !string.IsNullOrEmpty(it.itemId)) items.Add(it);
            }
            if (items.Count == 0) { Debug.LogWarning("[ShopCatalogSync] Không tìm thấy ShopItemData nào có itemId."); return; }

            // Đảm bảo 2 loại tiền ảo tồn tại (bỏ qua lỗi nếu đã có).
            Post("AddVirtualCurrencyTypes", "{\"VirtualCurrencies\":[{\"CurrencyCode\":\"" + GoldCode + "\",\"DisplayName\":\"Gold\",\"InitialDeposit\":0}]}", secret, true);
            Post("AddVirtualCurrencyTypes", "{\"VirtualCurrencies\":[{\"CurrencyCode\":\"" + ArenaCode + "\",\"DisplayName\":\"Arena Coin\",\"InitialDeposit\":0}]}", secret, true);

            // 1) Catalog (giá để validate mua).
            string body = BuildCatalogJson(items);
            string res = Post("SetCatalogItems", body, secret, false);

            // 2) Title Data (JSON hiển thị) — cùng nguồn ShopItemData nên khớp giá.
            var infos = new List<Game.Shop.ShopItemInfo>();
            foreach (var it in items) infos.Add(it.ToInfo());
            string titleJson = Game.Shop.ShopCatalogJson.Build(infos);
            string tdBody = "{\"Key\":\"" + TitleDataKey + "\",\"Value\":" + JsonStr(titleJson) + "}";
            string tdRes = Post("SetTitleData", tdBody, secret, false);

            if (res != null)
                Debug.Log("[ShopCatalogSync] Đã đồng bộ " + items.Count + " item → Catalog '" + CatalogVersion + "'"
                    + (tdRes != null ? (" + Title Data '" + TitleDataKey + "'.") : " (Title Data lỗi, xem log trên)."));
        }

        private static string BuildCatalogJson(List<ShopItemData> items)
        {
            var sb = new StringBuilder();
            sb.Append("{\"CatalogVersion\":\"").Append(CatalogVersion).Append("\",\"SetAsDefaultCatalog\":true,\"Catalog\":[");
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                string code = it.currency == ShopCurrency.Gold ? GoldCode : ArenaCode;
                int price = Mathf.Max(0, it.DisplayPrice);
                string name = string.IsNullOrEmpty(it.displayName) ? it.itemId : it.displayName;
                if (i > 0) sb.Append(',');
                sb.Append("{\"ItemId\":\"").Append(Esc(it.itemId)).Append("\",")
                  .Append("\"DisplayName\":\"").Append(Esc(name)).Append("\",")
                  .Append("\"IsStackable\":false,\"IsTradable\":false,")
                  .Append("\"VirtualCurrencyPrices\":{\"").Append(code).Append("\":").Append(price).Append("}}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
        }

        // Bọc chuỗi thành JSON string literal (quote + escape) để nhét JSON hiển thị vào trường Value.
        private static string JsonStr(string s)
        {
            if (s == null) return "\"\"";
            var b = new StringBuilder(s.Length + 2);
            b.Append('"');
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '\\': b.Append("\\\\"); break;
                    case '"': b.Append("\\\""); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (ch < ' ') b.Append("\\u").Append(((int)ch).ToString("x4"));
                        else b.Append(ch);
                        break;
                }
            }
            b.Append('"');
            return b.ToString();
        }

        private static string Post(string endpoint, string json, string secret, bool ignoreError)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string url = "https://" + TitleId + ".playfabapi.com/Admin/" + endpoint;
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                wc.Headers["X-SecretKey"] = secret;
                wc.Encoding = Encoding.UTF8;
                try
                {
                    return wc.UploadString(url, "POST", json);
                }
                catch (WebException ex)
                {
                    string detail = string.Empty;
                    try
                    {
                        if (ex.Response != null)
                            using (var r = new System.IO.StreamReader(ex.Response.GetResponseStream()))
                                detail = r.ReadToEnd();
                    }
                    catch { }
                    if (ignoreError) { Debug.Log("[ShopCatalogSync] " + endpoint + " (bỏ qua): " + detail); return null; }
                    Debug.LogError("[ShopCatalogSync] " + endpoint + " LỖI: " + ex.Message + " | " + detail);
                    return null;
                }
            }
        }
    }

    /// <summary>Cửa sổ nhỏ để nhập/lưu PlayFab secret key vào EditorPrefs (chỉ máy này).</summary>
    public class ShopSecretKeyWindow : EditorWindow
    {
        private string _key = string.Empty;

        public static void Open()
        {
            var w = GetWindow<ShopSecretKeyWindow>(true, "PlayFab Secret Key", true);
            w.minSize = new Vector2(440f, 140f);
            w._key = EditorPrefs.GetString(ShopCatalogSync.KeyPref, string.Empty);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Key CHỈ lưu trên máy này (EditorPrefs) — không vào repo/build.\n" +
                "CI/pipeline có thể dùng biến môi trường PLAYFAB_SECRET thay cho ô này.\n" +
                "Lấy key ở PlayFab: Settings → Secret Keys.", MessageType.Info);
            _key = EditorGUILayout.PasswordField("Secret Key", _key);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Lưu"))
            {
                EditorPrefs.SetString(ShopCatalogSync.KeyPref, (_key ?? string.Empty).Trim());
                Debug.Log("[ShopCatalogSync] Đã lưu secret key (máy này).");
                Close();
            }
            if (GUILayout.Button("Xoá"))
            {
                EditorPrefs.DeleteKey(ShopCatalogSync.KeyPref);
                _key = string.Empty;
                Debug.Log("[ShopCatalogSync] Đã xoá secret key.");
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
