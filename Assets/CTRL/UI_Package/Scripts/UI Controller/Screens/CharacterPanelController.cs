// [Aemy 2026-10-10] FILE MOI. Tab NHAN VAT (thay overlay cu): ben trai danh sach nhan vat, ben phai model 3D xoay duoc tren nen kho.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Backend.PlayfabImpl;
using GaMo.TrueInputSystem;
using CharacterInfo = Backend.PlayfabImpl.CharacterInfo;

namespace Game.UI
{
    /// <summary>
    /// Panel Nhan vat (cung bo cuc voi Trang bi). Du lieu: prefab nhan vat tu GameManager.GetCharacterList()
    /// + ten/mo ta tu CharacterCatalogService (Title Data "Characters"). Chon = luu PlayerPrefs "SelectedCharacter" + GameManager.SetCharacter.
    /// - Item danh sach: nhan ban tu _itemTemplate (1 nut trong scene, se bi an). Chuot bam / tay cam di toi -> xem truoc.
    /// - Nut CHON: dung nhan vat dang xem.
    /// </summary>
    public class CharacterPanelController : MonoBehaviour
    {
        [Header("Danh sach (trai)")]
        [SerializeField] private RectTransform _listRoot;
        [SerializeField] private TISGamepadUIButton _itemTemplate;   // co: TMP ten (font lon) + TMP phu (font nho) + Image nen
        [SerializeField] private float _itemSpacing = 91f;
        [SerializeField] private Sprite _itemNormal, _itemSelected;
        [Tooltip("Ten Image con trong item de hien avatar (anh previewUrl tren sheet Characters) - giong icon o Trang bi.")]
        [SerializeField] private string _avatarChildName = "Avatar";

        [Header("Xem truoc (phai)")]
        [SerializeField] private ModelPreviewStage _preview;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _descLabel;
        [SerializeField] private TMP_Text _hintLabel;

        [Header("Chon")]
        [SerializeField] private TISGamepadUIButton _selectButton;
        [SerializeField] private TMP_Text _selectLabel;

        private readonly List<CharacterInfo> _list = new List<CharacterInfo>();
        private readonly Dictionary<string, NPlayer> _prefabById = new Dictionary<string, NPlayer>(StringComparer.OrdinalIgnoreCase);
        private readonly List<TISGamepadUIButton> _items = new List<TISGamepadUIButton>();
        private int _index = -1;
        private string _selectedId = "";
        private bool _loaded;

        private void Awake()
        {
            if (_itemTemplate != null) _itemTemplate.gameObject.SetActive(false);
            if (_selectButton != null) _selectButton.onClick.AddListener(SelectCurrent);
        }

        private void OnEnable()
        {
            _selectedId = PlayerPrefs.GetString(CharacterSelectController.PrefKey, "");
            if (_loaded) { ShowIndex(Mathf.Max(0, FindIndex(_selectedId)), true); FocusForGamepad(); return; }
            LoadData();
        }

        private void LoadData()
        {
            CharacterCatalogService.LoadList(cat =>
            {
                if (this == null) return;
                _list.Clear(); _prefabById.Clear();
                var gm = GameManager.Instance;
                NPlayer[] prefs = gm != null ? gm.GetCharacterList() : null;
                if (prefs != null)
                    foreach (var pf in prefs)
                    {
                        if (pf == null || string.IsNullOrEmpty(pf.Name)) continue;
                        // [Aemy 2026-10-11] id CHUAN = ten prefab (NPlayer.Name, vd "Anh Teo") - dung de tim model + luu lua chon.
                        // Sheet co the ghi id khac (vd "AnhTeo") -> chi lay ten/mo ta/anh tu sheet, KHONG dung id cua sheet.
                        var found = CharacterCatalogService.Find(pf.Name);
                        var info = new CharacterInfo
                        {
                            id = pf.Name,
                            name = found != null && !string.IsNullOrEmpty(found.name) ? found.name : pf.Name.ToUpperInvariant(),
                            desc = found != null ? found.desc : "",
                            previewUrl = found != null ? found.previewUrl : ""
                        };
                        _list.Add(info); _prefabById[pf.Name] = pf;
                    }
                if (_list.Count == 0 && cat != null) _list.AddRange(cat);
                BuildItems();
                _loaded = true;
                if (_selectedId == "" && _list.Count > 0 && gm != null) _selectedId = gm.GetSetupData().CharacterName ?? "";
                CanonicalizeSelected();
                ShowIndex(Mathf.Max(0, FindIndex(_selectedId)), true);
                FocusForGamepad();
            });
        }

        // Dang dung tay cam -> dat focus vao nhan vat dang xem (D-pad di len/xuong ngay).
        private void FocusForGamepad()
        {
            var br = TISInputBridge.Instance; if (br == null || !br.IsGamepad) return;
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && _index >= 0 && _index < _items.Count && _items[_index] != null) es.SetSelectedGameObject(_items[_index].gameObject);
        }

        private int FindIndex(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _list.Count; i++) if (string.Equals(_list[i].id, id, StringComparison.OrdinalIgnoreCase)) return i;
            // [Aemy 2026-10-11] du phong: "AnhTeo" (id cu tu sheet) ~ "Anh Teo"
            string k = Norm(id);
            for (int i = 0; i < _list.Count; i++) if (Norm(_list[i].id) == k) return i;
            return -1;
        }

        private static string Norm(string s)
        {
            var sb = new System.Text.StringBuilder();
            if (s != null) foreach (var ch in s) if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }

        // Lua chon da luu bang id lech (vd "AnhTeo") -> doi ve id prefab chuan + luu lai, de vao tran spawn dung nhan vat.
        private void CanonicalizeSelected()
        {
            int i = FindIndex(_selectedId);
            if (i < 0 || string.Equals(_list[i].id, _selectedId, StringComparison.Ordinal)) return;
            _selectedId = _list[i].id;
            PlayerPrefs.SetString(CharacterSelectController.PrefKey, _selectedId); PlayerPrefs.Save();
            if (GameManager.Instance != null) GameManager.Instance.SetCharacter(_selectedId);
        }

        private void BuildItems()
        {
            foreach (var it in _items) if (it != null) Destroy(it.gameObject);
            _items.Clear();
            if (_itemTemplate == null || _listRoot == null) return;
            var tpl = (RectTransform)_itemTemplate.transform;
            for (int i = 0; i < _list.Count; i++)
            {
                var go = Instantiate(_itemTemplate.gameObject, _listRoot);
                go.name = "Char_" + _list[i].id; go.SetActive(true);
                var rt = (RectTransform)go.transform;
                rt.anchoredPosition = tpl.anchoredPosition + new Vector2(0f, -_itemSpacing * i);
                var btn = go.GetComponent<TISGamepadUIButton>();
                SetItemTexts(go, DisplayName(_list[i]), Subtitle(_list[i]));
                LoadAvatar(go, _list[i]);
                int idx = i;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => ShowIndex(idx, true));
                btn.onSelect.AddListener(() => ShowIndex(idx, false)); // tay cam di toi -> xem truoc
                _items.Add(btn);
            }
            // noi nav tay cam: doc theo danh sach, phai -> nut CHON
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].navUp = i > 0 ? _items[i - 1] : null;
                _items[i].navDown = i < _items.Count - 1 ? _items[i + 1] : null;
                _items[i].navRight = _selectButton;
                _items[i].navLeft = null;
                _items[i].isFirstSelected = i == 0;
            }
            if (_selectButton != null) _selectButton.navLeft = _items.Count > 0 ? _items[0] : null;
            var ctx = GetComponentInParent<TISUINavigationContext>();
            if (ctx != null) { foreach (var b in _items) b.SetContext(ctx); }
        }

        // [Aemy 2026-10-11] Avatar trong o: tai previewUrl (URL http hoac key PlayFab Content) qua ShopSpriteLoader (cache chung voi Shop/icon sung).
        private void LoadAvatar(GameObject item, CharacterInfo c)
        {
            var t = FindDeep(item.transform, _avatarChildName);
            var img = t != null ? t.GetComponent<Image>() : null;
            if (img == null) return;
            img.enabled = false; // chua co anh -> chi hien khung
            if (c == null || string.IsNullOrEmpty(c.previewUrl)) return;
            RequestAvatar(img, c.previewUrl, 1);
        }

        // Tai anh; mang chap chon -> thu lai them 1 lan sau 2 giay.
        private void RequestAvatar(Image img, string url, int retries)
        {
            Game.Shop.ShopSpriteLoader.Load(url, spr =>
            {
                if (img == null) return;
                if (spr == null) { if (retries > 0 && isActiveAndEnabled) StartCoroutine(RetryAvatar(img, url, retries - 1)); return; }
                img.sprite = spr; img.enabled = true;
                var fit = img.GetComponent<AspectRatioFitter>();
                if (fit != null && spr.rect.height > 0f) fit.aspectRatio = spr.rect.width / spr.rect.height;
            });
        }

        private System.Collections.IEnumerator RetryAvatar(Image img, string url, int retries)
        {
            yield return new WaitForSecondsRealtime(2f);
            RequestAvatar(img, url, retries);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            foreach (Transform c in root)
            {
                if (c.name == name) return c;
                var r = FindDeep(c, name); if (r != null) return r;
            }
            return null;
        }

        private static string DisplayName(CharacterInfo c) { return string.IsNullOrEmpty(c.name) ? c.id : c.name; }
        private static string Subtitle(CharacterInfo c)
        {
            if (string.IsNullOrEmpty(c.desc)) return "Đặc nhiệm";
            var d = c.desc.Trim(); int cut = d.IndexOfAny(new[] { '.', '·', '\n' });
            if (cut > 0 && cut < 40) d = d.Substring(0, cut);
            return d.Length > 38 ? d.Substring(0, 36) + "…" : d;
        }

        private static void SetItemTexts(GameObject item, string title, string sub)
        {
            var tmps = item.GetComponentsInChildren<TMP_Text>(true);
            TMP_Text big = null, small = null;
            foreach (var t in tmps) { if (big == null || t.fontSize > big.fontSize) big = t; }
            foreach (var t in tmps) { if (t != big && (small == null || t.fontSize > small.fontSize)) small = t; }
            if (big != null) big.text = title;
            if (small != null) small.text = sub;
        }

        private void ShowIndex(int i, bool updateFocusVisual)
        {
            if (_list.Count == 0) return;
            i = Mathf.Clamp(i, 0, _list.Count - 1);
            bool changed = i != _index;
            _index = i;
            var c = _list[i];
            if (_nameLabel != null) _nameLabel.text = DisplayName(c);
            if (_descLabel != null) _descLabel.text = string.IsNullOrEmpty(c.desc) ? "" : c.desc;
            if (changed && _preview != null)
            {
                NPlayer pf; GameObject src = null;
                if (_prefabById.TryGetValue(c.id, out pf) && pf != null)
                {
                    var anim = pf.GetComponentInChildren<Animator>(true);
                    src = anim != null ? anim.gameObject : (pf.transform.childCount > 0 ? pf.transform.GetChild(0).gameObject : null);
                }
                _preview.Show(src, false);
            }
            for (int k = 0; k < _items.Count; k++)
            {
                var img = _items[k] != null ? _items[k].GetComponent<Image>() : null;
                if (img != null && _itemSelected != null && _itemNormal != null) img.sprite = k == _index ? _itemSelected : _itemNormal;
            }
            UpdateSelectButton();
        }

        private void UpdateSelectButton()
        {
            if (_selectLabel == null || _index < 0 || _index >= _list.Count) return;
            bool current = string.Equals(_list[_index].id, _selectedId, StringComparison.OrdinalIgnoreCase);
            _selectLabel.text = current ? "ĐANG DÙNG" : "CHỌN";
            if (_selectButton != null) _selectButton.interactable = !current;
        }

        private void SelectCurrent()
        {
            if (_index < 0 || _index >= _list.Count) return;
            _selectedId = _list[_index].id;
            PlayerPrefs.SetString(CharacterSelectController.PrefKey, _selectedId); PlayerPrefs.Save();
            if (GameManager.Instance != null) GameManager.Instance.SetCharacter(_selectedId);
            UpdateSelectButton();
        }
    }
}
