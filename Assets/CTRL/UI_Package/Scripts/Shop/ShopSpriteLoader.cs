using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using PlayFab;
using PlayFab.ClientModels;

namespace Game.Shop
{
    /// <summary>
    /// TAI ANH DONG cho shop. Nhan 1 'keyOrUrl':
    ///   - Bat dau bang http(s)://  -> tai truc tiep tu URL do.
    ///   - Nguoc lai coi la KEY tren PlayFab Content -> xin URL qua GetContentDownloadUrl roi tai.
    ///
    /// CACHE 2 tang: bo nho (session) + dia (Application.persistentDataPath/shop_art_cache).
    /// Lan dau tai ve, lan sau doc tu dia -> khong ton mang, mo shop muot. Doi anh tren server
    /// thi doi luon key/URL (vd them '?v=2') de bust cache.
    ///
    /// CACH DUNG:
    ///   ShopSpriteLoader.Load("shop/hk14_model.png", spr => img.sprite = spr);
    /// Tra ve null neu tai loi/offline -> caller nen fallback ve anh default cua registry.
    /// Singleton tu tao (DontDestroyOnLoad), khong can keo vao scene.
    /// </summary>
    public class ShopSpriteLoader : MonoBehaviour
    {
        private static ShopSpriteLoader _instance;
        public static ShopSpriteLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("~ShopSpriteLoader");
                    DontDestroyOnLoad(go);
                    go.hideFlags = HideFlags.HideAndDontSave;
                    _instance = go.AddComponent<ShopSpriteLoader>();
                }
                return _instance;
            }
        }

        private readonly Dictionary<string, Sprite> _memCache = new Dictionary<string, Sprite>();
        // Cac callback dang cho cung 1 key -> chi tai 1 lan.
        private readonly Dictionary<string, List<Action<Sprite>>> _pending = new Dictionary<string, List<Action<Sprite>>>();

        private string CacheDir
        {
            get
            {
                string dir = Path.Combine(Application.persistentDataPath, "shop_art_cache");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>Tai (hoac lay tu cache) anh cho keyOrUrl. onDone(null) neu that bai.</summary>
        public static void Load(string keyOrUrl, Action<Sprite> onDone)
        {
            if (onDone == null) return;
            if (string.IsNullOrEmpty(keyOrUrl)) { onDone(null); return; }
            if (!Application.isPlaying) { onDone(null); return; }
            Instance.LoadInternal(keyOrUrl, onDone);
        }

        private void LoadInternal(string keyOrUrl, Action<Sprite> onDone)
        {
            Sprite cached;
            if (_memCache.TryGetValue(keyOrUrl, out cached)) { onDone(cached); return; }

            // Gop callback neu dang tai cung key.
            List<Action<Sprite>> waiters;
            if (_pending.TryGetValue(keyOrUrl, out waiters)) { waiters.Add(onDone); return; }
            _pending[keyOrUrl] = new List<Action<Sprite>> { onDone };

            // Thu doc tu cache dia truoc.
            string file = DiskPath(keyOrUrl);
            if (File.Exists(file))
            {
                StartCoroutine(LoadFromDisk(keyOrUrl, file));
                return;
            }

            // Chua co -> resolve URL roi tai.
            if (keyOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                keyOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(Download(keyOrUrl, keyOrUrl, file));
            }
            else
            {
                ResolvePlayFabContent(keyOrUrl, file);
            }
        }

        private void ResolvePlayFabContent(string key, string file)
        {
            if (!PlayFabClientAPI.IsClientLoggedIn())
            {
                Debug.LogWarning("[ShopSpriteLoader] Chua dang nhap PlayFab, khong xin duoc URL Content cho key: " + key);
                Complete(key, null);
                return;
            }
            PlayFabClientAPI.GetContentDownloadUrl(
                new GetContentDownloadUrlRequest { Key = key, HttpMethod = "GET", ThruCDN = true },
                res =>
                {
                    if (res == null || string.IsNullOrEmpty(res.URL)) { Complete(key, null); return; }
                    StartCoroutine(Download(key, res.URL, file));
                },
                err =>
                {
                    Debug.LogWarning("[ShopSpriteLoader] GetContentDownloadUrl loi (" + key + "): " + err.ErrorMessage);
                    Complete(key, null);
                });
        }

        private IEnumerator LoadFromDisk(string key, string file)
        {
            byte[] bytes = null;
            try { bytes = File.ReadAllBytes(file); }
            catch (Exception e) { Debug.LogWarning("[ShopSpriteLoader] Doc cache loi: " + e.Message); }
            yield return null;
            Complete(key, BytesToSprite(bytes));
        }

        private IEnumerator Download(string key, string url, string file)
        {
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
#if UNITY_2020_1_OR_NEWER
                bool ok = req.result == UnityWebRequest.Result.Success;
#else
                bool ok = !req.isNetworkError && !req.isHttpError;
#endif
                if (!ok)
                {
                    Debug.LogWarning("[ShopSpriteLoader] Tai anh loi (" + key + "): " + req.error);
                    Complete(key, null);
                    yield break;
                }
                var tex = DownloadHandlerTexture.GetContent(req); if (tex != null) tex = Game.UI.ImageScale.Fit(tex, 256); // [#5] thu nho 64px
                // Luu cache dia (PNG) de lan sau khoi tai.
                try { if (tex != null) File.WriteAllBytes(file, tex.EncodeToPNG()); }
                catch (Exception e) { Debug.LogWarning("[ShopSpriteLoader] Ghi cache loi: " + e.Message); }
                Complete(key, TextureToSprite(tex));
            }
        }

        private void Complete(string key, Sprite sprite)
        {
            if (sprite != null) _memCache[key] = sprite;
            List<Action<Sprite>> waiters;
            if (_pending.TryGetValue(key, out waiters))
            {
                _pending.Remove(key);
                foreach (var cb in waiters) { if (cb != null) cb(sprite); }
            }
        }

        private static Sprite BytesToSprite(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes)) return null;
            return TextureToSprite(tex);
        }

        private static Sprite TextureToSprite(Texture2D tex)
        {
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private string DiskPath(string keyOrUrl)
        {
            return Path.Combine(CacheDir, Hash(keyOrUrl) + ".png");
        }

        private static string Hash(string s)
        {
            using (var md5 = MD5.Create())
            {
                byte[] h = md5.ComputeHash(Encoding.UTF8.GetBytes(s ?? string.Empty));
                var sb = new StringBuilder(h.Length * 2);
                foreach (var b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
