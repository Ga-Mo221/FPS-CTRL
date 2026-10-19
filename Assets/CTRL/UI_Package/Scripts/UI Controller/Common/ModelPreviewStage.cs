// [Aemy 2026-10-10] FILE MOI. Khung xem model 3D dung chung (Nhan vat, Skin sung).
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// Gan len 1 RawImage. Tao "san khau" rieng o xa (camera + 2 den) render vao RenderTexture NEN TRONG SUOT
    /// -> anh nen (Image dat phia sau RawImage) nhin xuyen qua duoc.
    /// - Show(src, bakeStatic): bakeStatic=true -> chi chep hinh (nuong SkinnedMesh thanh mesh tinh), KHONG chay script nao
    ///   (an toan voi prefab co NetworkObject/Fusion nhu sung). false -> Instantiate nguyen (giu Animator, dung cho nhan vat).
    /// - Xoay: keo chuot trai tren khung, stick phai tay cam, khong dung thi tu quay cham. Cuon chuot = zoom nhe.
    /// - Tu canh camera theo bounds model (to nho khac nhau van vua khung).
    /// - Tat panel (OnDisable) -> giai phong RenderTexture + san khau.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class ModelPreviewStage : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IScrollHandler
    {
        [Tooltip("Vi tri san khau trong the gioi (moi khung 1 vi tri khac nhau, xa map).")]
        [SerializeField] private Vector3 _stageOrigin = new Vector3(0f, -6000f, 0f);
        [SerializeField] private float _fieldOfView = 26f;
        [Tooltip("Xoay goc ban dau cua model (vd sung: 0,90,0 de nhin ngang).")]
        [SerializeField] private Vector3 _baseEuler = Vector3.zero;
        [SerializeField] private float _autoSpinSpeed = 16f;
        [SerializeField] private float _dragSpeed = 0.35f;
        [Tooltip("He so khoang cach camera (lon = model nho hon trong khung).")]
        [SerializeField] private float _framePadding = 1.15f;
        [SerializeField] private float _minZoom = 0.7f, _maxZoom = 1.5f;
        [SerializeField] private float _keyLight = 1.3f, _fillLight = 0.55f;

        [Header("Neo dat (Aemy 2026-10-11) - dung cho nhan vat")]
        [Tooltip("Bat: chan model luon o 1 do cao co dinh trong khung (khop mat san cua anh nen), khong canh giua.")]
        [SerializeField] private bool _groundAnchor = false;
        [Tooltip("Vi tri mat dat tinh tu DAY khung (0 = sat day, 0.5 = giua).")]
        [Range(0f, 0.5f)] [SerializeField] private float _groundViewportY = 0.09f;
        [Tooltip("Khoang trong tren dau (ty le chieu cao khung).")]
        [Range(0f, 0.3f)] [SerializeField] private float _headroom = 0.07f;
        [Tooltip("Bong do duoi chan (UI, tu tao neu de trong). Ve TRUOC RawImage nen model de len.")]
        [SerializeField] private RectTransform _groundShadow;
        [SerializeField] private Color _groundShadowColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] private float _groundShadowScale = 1.25f;

        private RawImage _raw;
        private RenderTexture _rt;
        private Camera _cam;
        private Transform _stage, _pivot;
        private GameObject _model;
        private float _yaw, _zoom = 1f, _baseDist = 3f;
        private float _footRadius = 0.3f; // ban kinh ngang model (bong do)
        private bool _dragging;
        private float _idleTimer;

        public bool HasModel { get { return _model != null; } }

        private void Awake() { _raw = GetComponent<RawImage>(); _raw.raycastTarget = true; }
        private void OnEnable() { EnsureStage(); }
        private void OnDisable() { TearDown(); if (_groundShadow != null) _groundShadow.gameObject.SetActive(false); }
        private void OnDestroy() { TearDown(); }

        private void EnsureStage()
        {
            if (_stage != null) return;
            var rect = ((RectTransform)transform).rect;
            int w = Mathf.Clamp(Mathf.RoundToInt(rect.width), 256, 2048), h = Mathf.Clamp(Mathf.RoundToInt(rect.height), 256, 2048);
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "ModelPreviewRT", antiAliasing = 4 };
            _rt.Create();
            _raw.texture = _rt; _raw.color = Color.white;

            _stage = new GameObject("~ModelPreviewStage").transform;
            _stage.position = _stageOrigin;
            _pivot = new GameObject("Pivot").transform; _pivot.SetParent(_stage, false);

            _cam = new GameObject("PreviewCam").AddComponent<Camera>();
            _cam.transform.SetParent(_stage, false);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // trong suot -> thay anh nen
            _cam.fieldOfView = _fieldOfView; _cam.nearClipPlane = 0.02f; _cam.farClipPlane = 80f;
            _cam.targetTexture = _rt; _cam.allowHDR = false;

            var key = new GameObject("Key").AddComponent<Light>(); key.transform.SetParent(_stage, false);
            key.type = LightType.Directional; key.intensity = _keyLight; key.color = new Color(1f, 0.97f, 0.92f);
            key.transform.localRotation = Quaternion.Euler(35f, 150f, 0f);
            var fill = new GameObject("Fill").AddComponent<Light>(); fill.transform.SetParent(_stage, false);
            fill.type = LightType.Directional; fill.intensity = _fillLight; fill.color = new Color(0.55f, 0.85f, 0.85f);
            fill.transform.localRotation = Quaternion.Euler(15f, -45f, 0f);
        }

        private void TearDown()
        {
            if (_model != null)
            {
                // mesh nuong tao moi -> tu huy de khong ro ri bo nho moi lan doi skin
                foreach (var mf in _model.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && mf.sharedMesh.name == BakedMeshName) Destroy(mf.sharedMesh);
                Destroy(_model); _model = null;
            }
            if (_stage != null) { Destroy(_stage.gameObject); _stage = null; _pivot = null; _cam = null; }
            if (_rt != null) { if (_raw != null && _raw.texture == _rt) _raw.texture = null; _rt.Release(); Destroy(_rt); _rt = null; }
        }

        /// <summary>Hien model tu nguon. Tra false neu nguon rong / khong co hinh.</summary>
        public bool Show(GameObject source, bool bakeStatic) { return Show(source, bakeStatic, Vector3.zero); }

        /// <summary>[Aemy 2026-10-19] Nhu tren + xoay them rieng cho model nay (truc local cua model, sau _baseEuler).</summary>
        public bool Show(GameObject source, bool bakeStatic, Vector3 extraEuler)
        {
            EnsureStage();
            Clear();
            if (source == null) { _raw.enabled = false; SetShadowVisible(false); return false; }
            _model = bakeStatic ? BakeCopy(source) : Instantiate(source);
            if (_model == null) { _raw.enabled = false; SetShadowVisible(false); return false; }
            _model.name = "Model";
            _model.transform.SetParent(_pivot, false);
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = Quaternion.Euler(_baseEuler) * Quaternion.Euler(extraEuler);
            SetLayerRecursive(_model, 0);
            _yaw = 0f; _zoom = 1f; _pivot.localRotation = Quaternion.identity;
            Frame();
            _raw.enabled = true;
            return true;
        }

        public void Clear()
        {
            if (_model != null)
            {
                // mesh nuong tao moi -> tu huy de khong ro ri bo nho moi lan doi skin
                foreach (var mf in _model.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && mf.sharedMesh.name == BakedMeshName) Destroy(mf.sharedMesh);
                Destroy(_model); _model = null;
            }
        }

        // Chep CHI hinh: moi Renderer -> 1 GameObject MeshFilter+MeshRenderer (SkinnedMesh duoc nuong o tu the hien tai).
        private const string BakedMeshName = "~AemyBaked";
        private static GameObject BakeCopy(GameObject source)
        {
            var holder = new GameObject("~bake"); holder.SetActive(false); holder.transform.position = new Vector3(0f, -9000f, 0f);
            var temp = Instantiate(source, holder.transform); // nam duoi cha inactive -> khong Awake script nao
            temp.transform.localPosition = Vector3.zero; temp.transform.localRotation = Quaternion.identity; temp.transform.localScale = source.transform.localScale;
            // [Aemy] SkinnedMeshRenderer chi BakeMesh dung khi dang ACTIVE -> go het script (Fusion...) roi moi bat len.
            StripBehaviours(temp);
            holder.SetActive(true);
            var root = new GameObject("BakedModel");
            int count = 0;
            foreach (var r in temp.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                var smr = r as SkinnedMeshRenderer;
                bool baked = false;
                if (smr != null)
                {
                    if (smr.sharedMesh == null) continue;
                    // Xuong (bones) hong/thieu (vd prefab sung roi: 50/53 bone null) -> BakeMesh ra mesh sai -> dung mesh goc + scale cua transform.
                    if (BonesValid(smr)) { mesh = new Mesh(); mesh.name = BakedMeshName; smr.BakeMesh(mesh, true); mesh.RecalculateBounds(); baked = true; }
                    else mesh = smr.sharedMesh;
                }
                else { var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || !(r is MeshRenderer)) continue; mesh = mf.sharedMesh; }
                var part = new GameObject(r.name);
                part.transform.SetParent(root.transform, false);
                // dat theo vi tri tuong doi so voi goc prefab
                part.transform.localPosition = temp.transform.InverseTransformPoint(r.transform.position);
                part.transform.localRotation = Quaternion.Inverse(temp.transform.rotation) * r.transform.rotation;
                part.transform.localScale = baked ? Vector3.one : r.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
                count++;
            }
            holder.SetActive(false);
            Destroy(holder);
            if (count == 0) { Destroy(root); return null; }
            return root;
        }

        private static bool BonesValid(SkinnedMeshRenderer smr)
        {
            var bones = smr.bones;
            if (bones == null || bones.Length == 0) return false;
            foreach (var b in bones) if (b == null) return false;
            return true;
        }

        // Xoa moi MonoBehaviour / Rigidbody / Collider trong ban sao (nhieu vong vi RequireComponent).
        private static void StripBehaviours(GameObject go)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                bool left = false;
                foreach (var c in go.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform || c is Renderer || c is MeshFilter || c is Animator) continue;
                    if (c is MonoBehaviour || c is Rigidbody || c is Collider || c is Joint || c is AudioSource)
                    {
                        try { DestroyImmediate(c); } catch { left = true; }
                        if (c != null) left = true;
                    }
                }
                if (!left) break;
            }
        }

        // Dua tam bounds ve goc pivot + dat camera vua khung.
        private void Frame()
        {
            if (_model == null || _cam == null) return;
            var rends = _model.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds; for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            if (_groundAnchor) { FrameGrounded(b); return; }
            _model.transform.position -= (b.center - _pivot.position);           // xoay quanh tam model
            float radius = Mathf.Max(b.extents.magnitude, 0.05f);
            float vFov = _fieldOfView * Mathf.Deg2Rad * 0.5f;
            float hFov = Mathf.Atan(Mathf.Tan(vFov) * _cam.aspect);
            float fov = Mathf.Min(vFov, hFov);
            _baseDist = radius / Mathf.Sin(fov) * _framePadding;
            ApplyCamera();
        }

        // [Aemy 2026-10-11] Neo dat: pivot = giua 2 chan; camera nhin ngang, nang/ha de chan nam dung _groundViewportY.
        private void FrameGrounded(Bounds b)
        {
            var p = _pivot.position;
            _model.transform.position -= new Vector3(b.center.x - p.x, b.min.y - p.y, b.center.z - p.z);
            float height = Mathf.Max(b.size.y, 0.05f);
            _footRadius = Mathf.Max(b.extents.x, b.extents.z);
            float tanV = Mathf.Tan(_fieldOfView * Mathf.Deg2Rad * 0.5f);
            float tanH = tanV * _cam.aspect;
            float usable = Mathf.Max(0.2f, 1f - _headroom - _groundViewportY);
            float dH = height / (usable * 2f * tanV);
            float dW = _footRadius / (tanH * 0.9f);
            _baseDist = Mathf.Max(dH, dW) * _framePadding + _footRadius;
            ApplyCamera();
            SetShadowVisible(true);
        }

        private void ApplyCamera()
        {
            if (_cam == null) return;
            if (_groundAnchor)
            {
                float d = _baseDist * _zoom;
                float tanV = Mathf.Tan(_fieldOfView * Mathf.Deg2Rad * 0.5f);
                float camY = -(_groundViewportY - 0.5f) * 2f * d * tanV; // so voi mat dat (pivot)
                _cam.transform.position = _pivot.position + new Vector3(0f, camY, d);
                _cam.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
                UpdateShadow();
                return;
            }
            _cam.transform.position = _pivot.position + new Vector3(0f, 0f, _baseDist * _zoom);
            _cam.transform.LookAt(_pivot.position);
        }

        // ---- Bong do duoi chan (UI) ----
        private static Sprite _shadowSprite;
        private static Sprite ShadowSprite()
        {
            if (_shadowSprite != null) return _shadowSprite;
            const int W = 128, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "~GroundShadow", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f) / W * 2f - 1f, dy = (y + 0.5f) / H * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r); a = a * a * (3f - 2f * a); // smoothstep
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            _shadowSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            return _shadowSprite;
        }

        private void EnsureShadow()
        {
            if (_groundShadow != null || !_groundAnchor) return;
            var go = new GameObject("GroundShadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _groundShadow = (RectTransform)go.transform;
            _groundShadow.SetParent(transform.parent, false);
            _groundShadow.SetSiblingIndex(transform.GetSiblingIndex()); // ngay TRUOC RawImage -> model ve de len
            var img = go.GetComponent<Image>(); img.raycastTarget = false;
        }

        private void SetShadowVisible(bool on)
        {
            if (!_groundAnchor) return;
            EnsureShadow();
            if (_groundShadow == null) return;
            var img = _groundShadow.GetComponent<Image>();
            if (img != null) { img.sprite = ShadowSprite(); img.color = _groundShadowColor; }
            _groundShadow.gameObject.SetActive(on);
            if (on) UpdateShadow();
        }

        private void UpdateShadow()
        {
            if (_groundShadow == null || _cam == null || _pivot == null) return;
            var rt = (RectTransform)transform;
            Vector3 c = _cam.WorldToViewportPoint(_pivot.position);
            Vector3 r = _cam.WorldToViewportPoint(_pivot.position + Vector3.right * _footRadius);
            Rect rect = rt.rect;
            Vector2 local = new Vector2(rect.xMin + c.x * rect.width, rect.yMin + c.y * rect.height);
            Vector3 world = rt.TransformPoint(local);
            var parent = (RectTransform)_groundShadow.parent;
            _groundShadow.anchorMin = _groundShadow.anchorMax = new Vector2(0.5f, 0.5f);
            _groundShadow.pivot = new Vector2(0.5f, 0.5f);
            _groundShadow.anchoredPosition = (Vector2)parent.InverseTransformPoint(world) - parent.rect.center;
            float w = Mathf.Abs(r.x - c.x) * rect.width * 2f * _groundShadowScale;
            _groundShadow.sizeDelta = new Vector2(w, w * 0.22f);
        }

        // ---- Input ----
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) { _dragging = true; _idleTimer = 0f; } }
        public void OnPointerUp(PointerEventData e) { _dragging = false; }
        public void OnDrag(PointerEventData e) { if (_dragging) { _yaw -= e.delta.x * _dragSpeed; _idleTimer = 0f; } }
        public void OnScroll(PointerEventData e) { _zoom = Mathf.Clamp(_zoom - e.scrollDelta.y * 0.06f, _minZoom, _maxZoom); ApplyCamera(); }

        private void Update()
        {
            if (_model == null || _pivot == null) return;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            float gx = gp != null ? gp.rightStick.ReadValue().x : 0f;
            if (Mathf.Abs(gx) > 0.2f) { _yaw -= gx * 140f * Time.unscaledDeltaTime; _idleTimer = 0f; }
            else if (!_dragging) { _idleTimer += Time.unscaledDeltaTime; if (_idleTimer > 1.5f) _yaw += _autoSpinSpeed * Time.unscaledDeltaTime; }
            _pivot.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }
    }
}
