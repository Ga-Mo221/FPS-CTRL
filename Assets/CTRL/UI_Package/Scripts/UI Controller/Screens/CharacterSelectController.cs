// [Aemy 2026-10-05] Man CHON NHAN VAT kieu LMHT: preview 3D XOAY o giua + mui ten trai/phai + nut CHON.
// Dung bang code -> CharacterSelectController.Open(). Model lay tu child _FBX cua prefab nhan vat (GameManager.GetCharacterList()),
// render qua camera rieng -> RenderTexture -> RawImage. Xoay: auto + keo chuot + stick phai tay cam.
// Luu lua chon vao PlayerPrefs "SelectedCharacter" (+ GameManager.SetCharacter). Anh/mo ta tu CharacterCatalogService.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Backend.PlayfabImpl;
using CharacterInfo = Backend.PlayfabImpl.CharacterInfo;

namespace Game.UI
{
    public class CharacterSelectController : MonoBehaviour
    {
        // [Aemy 2026-10-17] Luu nhan vat theo TAI KHOAN (truoc day key chung -> tai khoan khac tren cung may bi lay nhan vat cua nguoi truoc).
        public static string PrefKey { get { return SessionCache.UserKey("SelectedCharacter"); } }
        private const float AutoSpinSpeed = 18f;

        private static CharacterSelectController _instance;

        private readonly List<CharacterInfo> _list = new List<CharacterInfo>();
        private readonly Dictionary<string, NPlayer> _prefabById = new Dictionary<string, NPlayer>(StringComparer.OrdinalIgnoreCase);
        private int _index;
        private string _selectedId = "";

        private Image _preview;        // fallback anh tinh
        private RawImage _preview3d;   // 3D render
        private TMP_Text _nameLabel, _descLabel, _placeholder, _selectLabel, _pageLabel;
        private Button _selectBtn;
        private TMP_FontAsset _font;
        private float _navCd;
        private bool _suppressed;

        // ---- 3D stage ----
        private RenderTexture _rt;
        private Camera _previewCam;
        private Transform _stage;
        private GameObject _modelInstance;
        private float _spin = 150f;
        private bool _dragging;
        private Vector2 _lastPointer;

        public static void Open()
        {
            if (_instance != null) { _instance.transform.SetAsLastSibling(); return; }
            var go = new GameObject("CharacterSelect");
            _instance = go.AddComponent<CharacterSelectController>();
            _instance.Build();
        }

        private void Build()
        {
            _font = FindFont();
            GaMo.TrueInputSystem.TISGamepadUIButton.ExternalSuppress++; _suppressed = true; // chan input menu phia sau
            _selectedId = PlayerPrefs.GetString(PrefKey, "");

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5200;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var dim = NewImage(transform, "Dim", new Color(0.02f, 0.03f, 0.05f, 0.84f));
            Stretch(dim.rectTransform);
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Close);

            var panel = NewImage(transform, "Panel", new Color(0.07f, 0.09f, 0.12f, 0.99f));
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(1240, 720); prt.anchoredPosition = Vector2.zero;

            NewText(panel.transform, "Title", "CHỌN NHÂN VẬT", 40, FontStyles.Bold, new Color(0.93f, 0.95f, 0.98f, 1f),
                TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(700, 56));

            NewButton(panel.transform, "Close", "✕", 30, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(56, 56), Close);

            var frame = NewImage(panel.transform, "PreviewFrame", new Color(0.03f, 0.05f, 0.08f, 1f));
            var frt = frame.rectTransform; frt.anchorMin = frt.anchorMax = new Vector2(0, 0.5f); frt.pivot = new Vector2(0, 0.5f);
            frt.sizeDelta = new Vector2(560, 600); frt.anchoredPosition = new Vector2(70, -20);
            _placeholder = NewText(frame.transform, "Placeholder", "", 150, FontStyles.Bold, new Color(0.3f, 0.36f, 0.44f, 1f),
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _preview = NewImage(frame.transform, "Preview", Color.white); Stretch(_preview.rectTransform, 16);
            _preview.preserveAspect = true; _preview.raycastTarget = false; _preview.enabled = false;
            // RawImage 3D
            var rawGo = new GameObject("Preview3D", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            rawGo.transform.SetParent(frame.transform, false);
            _preview3d = rawGo.GetComponent<RawImage>(); Stretch(_preview3d.rectTransform, 10);
            _preview3d.raycastTarget = true; _preview3d.enabled = false;
            // hint keo
            NewText(frame.transform, "DragHint", "Giữ chuột kéo để xoay", 18, FontStyles.Normal, new Color(0.55f,0.6f,0.68f,0.8f),
                TextAlignmentOptions.Center, new Vector2(0.5f,0), new Vector2(0.5f,0), new Vector2(0.5f,0), new Vector2(0,14), new Vector2(340,26));

            _nameLabel = NewText(panel.transform, "Name", "", 52, FontStyles.Bold, new Color(0.31f, 0.86f, 0.80f, 1f), TextAlignmentOptions.Left,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(700, -150), new Vector2(470, 70));
            _descLabel = NewText(panel.transform, "Desc", "", 26, FontStyles.Normal, new Color(0.72f, 0.77f, 0.84f, 1f), TextAlignmentOptions.TopLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(700, -235), new Vector2(470, 230));
            _descLabel.textWrappingMode = TextWrappingModes.Normal;

            _selectBtn = NewButton(panel.transform, "Select", "CHỌN", 30, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(700, -500), new Vector2(300, 78), SelectCurrent);
            _selectLabel = _selectBtn.GetComponentInChildren<TMP_Text>();

            NewButton(panel.transform, "Prev", "◄", 44, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, -20), new Vector2(62, 110), Prev);
            NewButton(panel.transform, "Next", "►", 44, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, -20), new Vector2(62, 110), Next);

            _pageLabel = NewText(panel.transform, "Page", "", 22, FontStyles.Normal, new Color(0.6f, 0.65f, 0.72f, 1f), TextAlignmentOptions.Center,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(300, 34));

            CreateStage();
            LoadData();
        }

        private void CreateStage()
        {
            _rt = new RenderTexture(560, 600, 24, RenderTextureFormat.ARGB32);
            _rt.Create();
            if (_preview3d != null) _preview3d.texture = _rt;

            var stageGo = new GameObject("CharPreviewStage");
            _stage = stageGo.transform;
            _stage.position = new Vector3(0f, -6000f, 0f);

            var camGo = new GameObject("PreviewCam");
            camGo.transform.SetParent(_stage, false);
            _previewCam = camGo.AddComponent<Camera>();
            _previewCam.clearFlags = CameraClearFlags.SolidColor;
            _previewCam.backgroundColor = new Color(0.03f, 0.05f, 0.08f, 1f);
            _previewCam.fieldOfView = 28f;
            _previewCam.nearClipPlane = 0.03f; _previewCam.farClipPlane = 60f;
            _previewCam.cullingMask = ~0;
            _previewCam.targetTexture = _rt;

            var lightGo = new GameObject("PreviewLight");
            lightGo.transform.SetParent(_stage, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.25f; light.color = new Color(1f, 0.98f, 0.94f);
            lightGo.transform.localRotation = Quaternion.Euler(35f, 150f, 0f);
            var lightGo2 = new GameObject("PreviewFill");
            lightGo2.transform.SetParent(_stage, false);
            var fill = lightGo2.AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 0.5f; fill.color = new Color(0.6f, 0.7f, 0.9f);
            lightGo2.transform.localRotation = Quaternion.Euler(20f, -40f, 0f);
        }

        private void LoadData()
        {
            CharacterCatalogService.LoadList(cat =>
            {
                _list.Clear(); _prefabById.Clear();
                GameManager gm = GameManager.Instance;
                NPlayer[] prefs = gm != null ? gm.GetCharacterList() : null;
                if (prefs != null && prefs.Length > 0)
                {
                    foreach (var pf in prefs)
                    {
                        if (pf == null || string.IsNullOrEmpty(pf.Name)) continue;
                        var info = CharacterCatalogService.Find(pf.Name);
                        if (info == null) info = new CharacterInfo { id = pf.Name, name = pf.Name.ToUpperInvariant(), desc = "", previewUrl = "" };
                        _list.Add(info);
                        _prefabById[pf.Name] = pf;
                    }
                }
                else if (cat != null) _list.AddRange(cat);

                if (_list.Count == 0) { Close(); return; }
                _index = 0;
                for (int i = 0; i < _list.Count; i++)
                    if (string.Equals(_list[i].id, _selectedId, StringComparison.OrdinalIgnoreCase)) { _index = i; break; }
                ShowIndex(_index);
            });
        }

        private void ShowIndex(int i)
        {
            if (_list.Count == 0) return;
            _index = (i % _list.Count + _list.Count) % _list.Count;
            var c = _list[_index];
            string disp = string.IsNullOrEmpty(c.name) ? c.id : c.name;
            if (_nameLabel) _nameLabel.text = disp;
            if (_descLabel) _descLabel.text = c.desc != null ? c.desc : "";
            if (_pageLabel) _pageLabel.text = (_index + 1) + " / " + _list.Count;

            bool model = ShowModel(c.id);
            if (!model)
            {
                if (_preview)
                {
                    _preview.sprite = null; _preview.enabled = false;
                    if (_placeholder) { _placeholder.text = disp.Length > 0 ? disp.Substring(0, 1).ToUpperInvariant() : "?"; _placeholder.gameObject.SetActive(true); }
                    if (!string.IsNullOrEmpty(c.previewUrl))
                    {
                        string want = c.id;
                        Game.Shop.ShopSpriteLoader.Load(c.previewUrl, s =>
                        {
                            if (_preview == null || _list.Count == 0) return;
                            if (!string.Equals(_list[_index].id, want, StringComparison.OrdinalIgnoreCase)) return;
                            if (s != null) { _preview.sprite = s; _preview.enabled = true; if (_placeholder) _placeholder.gameObject.SetActive(false); }
                        });
                    }
                }
            }
            UpdateSelectBtn();
        }

        // Instantiate model _FBX cua nhan vat vao stage. Tra true neu dung duoc model 3D.
        private bool ShowModel(string id)
        {
            if (_modelInstance != null) { Destroy(_modelInstance); _modelInstance = null; }
            NPlayer prefab;
            if (_stage == null || !_prefabById.TryGetValue(id, out prefab) || prefab == null)
            { if (_preview3d) _preview3d.enabled = false; return false; }

            var anim = prefab.GetComponentInChildren<Animator>(true);
            GameObject modelSrc = anim != null ? anim.gameObject
                : (prefab.transform.childCount > 0 ? prefab.transform.GetChild(0).gameObject : null);
            if (modelSrc == null) { if (_preview3d) _preview3d.enabled = false; return false; }

            _modelInstance = Instantiate(modelSrc, _stage);
            _modelInstance.transform.localPosition = Vector3.zero;
            _modelInstance.transform.localRotation = Quaternion.Euler(0f, _spin, 0f);
            SetLayer(_modelInstance, 0);
            FrameModel();

            if (_preview3d) _preview3d.enabled = true;
            if (_preview) _preview.enabled = false;
            if (_placeholder) _placeholder.gameObject.SetActive(false);
            return true;
        }

        private void FrameModel()
        {
            if (_modelInstance == null || _previewCam == null) return;
            var rends = _modelInstance.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            Vector3 center = b.center;
            float h = Mathf.Max(b.size.y, 0.5f);
            float dist = h * 1.75f + 0.3f;
            var camT = _previewCam.transform;
            camT.position = center + new Vector3(0f, h * 0.06f, dist);
            camT.LookAt(center + new Vector3(0f, -h * 0.02f, 0f));
        }

        private void UpdateSelectBtn()
        {
            if (_selectLabel == null || _list.Count == 0) return;
            bool isCurrent = string.Equals(_list[_index].id, _selectedId, StringComparison.OrdinalIgnoreCase);
            _selectLabel.text = isCurrent ? "✓ ĐANG DÙNG" : "CHỌN";
            var img = _selectBtn != null ? _selectBtn.GetComponent<Image>() : null;
            if (img) img.color = isCurrent ? new Color(0.18f, 0.34f, 0.30f, 1f) : new Color(0.31f, 0.84f, 0.79f, 1f);
            _selectLabel.color = isCurrent ? new Color(0.7f, 0.78f, 0.76f, 1f) : new Color(0.04f, 0.07f, 0.09f, 1f);
        }

        private void Prev() { ShowIndex(_index - 1); }
        private void Next() { ShowIndex(_index + 1); }

        private void SelectCurrent()
        {
            if (_list.Count == 0) return;
            _selectedId = _list[_index].id;
            PlayerPrefs.SetString(PrefKey, _selectedId); PlayerPrefs.Save();
            if (GameManager.Instance != null) GameManager.Instance.SetCharacter(_selectedId);
            UpdateSelectBtn();
        }

        private void Close()
        {
            if (_instance == this) _instance = null;
            if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
            if (_stage != null) { Destroy(_stage.gameObject); _stage = null; }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_suppressed) { GaMo.TrueInputSystem.TISGamepadUIButton.ExternalSuppress = Mathf.Max(0, GaMo.TrueInputSystem.TISGamepadUIButton.ExternalSuppress - 1); _suppressed = false; }
            if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
            if (_stage != null) { Destroy(_stage.gameObject); _stage = null; }
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (gp != null && gp.buttonEast.wasPressedThisFrame) { Close(); return; } // B = thoat
            if (Time.unscaledTime >= _navCd)
            {
                int dir = 0;
                if (kb != null)
                {
                    if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) dir = -1;
                    else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) dir = 1;
                }
                if (dir == 0 && gp != null)
                {
                    float x = gp.dpad.ReadValue().x; if (Mathf.Abs(x) < 0.3f) x = gp.leftStick.ReadValue().x;
                    if (x > 0.5f) dir = 1; else if (x < -0.5f) dir = -1;
                }
                if (dir != 0) { ShowIndex(_index + dir); _navCd = Time.unscaledTime + 0.22f; }
            }
            if ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) ||
                (gp != null && gp.buttonSouth.wasPressedThisFrame))
                SelectCurrent();

            // xoay model: keo chuot / stick phai / auto
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && _preview3d != null)
            {
                Vector2 mp = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame && RectTransformUtility.RectangleContainsScreenPoint(_preview3d.rectTransform, mp, null))
                { _dragging = true; _lastPointer = mp; }
                if (_dragging && mouse.leftButton.isPressed) { _spin -= (mp.x - _lastPointer.x) * 0.4f; _lastPointer = mp; }
                if (mouse.leftButton.wasReleasedThisFrame) _dragging = false;
            }
            if (_modelInstance != null)
            {
                if (!_dragging)
                {
                    float gx = gp != null ? gp.rightStick.ReadValue().x : 0f;
                    if (Mathf.Abs(gx) > 0.2f) _spin -= gx * 120f * Time.unscaledDeltaTime;
                    else _spin += AutoSpinSpeed * Time.unscaledDeltaTime;
                }
                _modelInstance.transform.localRotation = Quaternion.Euler(0f, _spin, 0f);
            }
        }

        // ---------- helpers ----------
        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
        }
        private TMP_FontAsset FindFont()
        {
            var any = GameObject.FindFirstObjectByType<TMP_Text>();
            return any != null ? any.font : null;
        }
        private static void Stretch(RectTransform rt, float pad = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
        }
        private Image NewImage(Transform parent, string name, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = col; return img;
        }
        private TMP_Text NewText(Transform parent, string name, string text, float size, FontStyles style, Color col, TextAlignmentOptions align,
            Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            var t = go.GetComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.fontStyle = style; t.color = col; t.alignment = align; t.raycastTarget = false;
            if (_font != null) t.font = _font;
            return t;
        }
        private Button NewButton(Transform parent, string name, string label, float fontSize, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var img = NewImage(parent, name, new Color(0.14f, 0.17f, 0.22f, 1f));
            var rt = img.rectTransform; rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(onClick);
            NewText(img.transform, "Label", label, fontSize, FontStyles.Bold, new Color(0.93f, 0.95f, 0.98f, 1f),
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return btn;
        }
    }
}
