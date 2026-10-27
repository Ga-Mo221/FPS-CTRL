using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Ve crosshair (center + 4 tay up/down/left/right) tu SettingData.
    /// Live preview trong panel Settings; cung dung duoc cho crosshair HUD trong game.
    /// Tu cap nhat khi SettingManager.OnSettingsApplied ban (keo slider -> doi ngay).
    /// Map: thickness->be ngang tay, length->do dai tay, gap->khoang cach tam, opacity->alpha,
    ///      colorIndex->palette, centerDot->hien cham giua, outline->vien den (UI Outline).
    ///
    /// LEAD - cach dung / cach wire:
    /// * Preview trong Settings: dat len object crosshair co san con ten center/up/down/left/right (tu tim).
    /// * HUD crosshair trong game: dat len object Crosshair cua HUD roi GAN TAY 5 refs:
    ///   _center = Background, _up/_down = 2 Vertical Divider (y>0 = up, y<0 = down),
    ///   _left/_right = 2 Horizontal Divider (x<0 = left, x>0 = right).
    /// * _palette PHAI khop DUNG THU TU 6 o mau cua CrosshairColorPicker
    ///   (#4FD6C9,#4DFF88,#FF4D4D,#F2C14E,#FF4DFF,#FFFFFF). Sai thu tu = chon mau ra sai mau.
    /// * Khong can goi gi them: tu subscribe SettingManager.OnSettingsApplied va ve lai khi doi setting.
    /// </summary>
    public class CrosshairController : MonoBehaviour
    {
        [Header("Parts (de trong = tu tim theo ten con: center/up/down/left/right)")]
        [SerializeField] private RectTransform _center;
        [SerializeField] private RectTransform _up;
        [SerializeField] private RectTransform _down;
        [SerializeField] private RectTransform _left;
        [SerializeField] private RectTransform _right;

        [Header("Bang mau (khop 6 o Mau sac)")]
        [SerializeField] private Color[] _palette;

        [Header("Scale gia tri -> pixel")]
        [SerializeField] private float _thicknessScale = 2f;
        [SerializeField] private float _lengthScale = 3f;
        [SerializeField] private float _gapScale = 3f;
        [SerializeField] private Color _outlineColor = Color.black;
        [SerializeField] private float _outlineSize = 1.5f;

        private Image[] _arms;
        private Outline[] _outlines;
        private bool _cached;

        // [Aemy 2026-10-26] Khoang cach (px) tu tam toi giua moi tay theo setting hien tai.
        // UICrosshair (gian tam khi ban) lay day lam goc thay vi vi tri mac dinh trong prefab.
        public bool HasApplied { get; private set; }
        public float ArmOffset { get; private set; }
        /// <summary>Ban sau moi lan ve lai theo setting (de lop khac cong them hieu ung len tren).</summary>
        public event System.Action Applied;

        private void Awake() { CacheParts(); }

        private void OnEnable()
        {
            var m = SettingManager.Instance;
            if (m != null)
            {
                m.OnSettingsApplied += ApplyFromData;
                if (m.SettingData != null) ApplyFromData(m.SettingData);
            }
        }
        private void OnDisable()
        {
            if (SettingManager.Instance != null) SettingManager.Instance.OnSettingsApplied -= ApplyFromData;
        }

        private void CacheParts()
        {
            if (_cached) return;
            if (_center == null) _center = transform.Find("center") as RectTransform;
            if (_up    == null) _up    = transform.Find("up")    as RectTransform;
            if (_down  == null) _down  = transform.Find("down")  as RectTransform;
            if (_left  == null) _left  = transform.Find("left")  as RectTransform;
            if (_right == null) _right = transform.Find("right") as RectTransform;
            var rts = new RectTransform[] { _center, _up, _down, _left, _right };
            _arms = new Image[5]; _outlines = new Outline[5];
            for (int i = 0; i < 5; i++)
            {
                if (rts[i] == null) continue;
                _arms[i] = rts[i].GetComponent<Image>();
                var ph = rts[i].Find("outline");            // an outline con placeholder
                if (ph != null) ph.gameObject.SetActive(false);
                var ol = rts[i].GetComponent<Outline>();
                if (ol == null) ol = rts[i].gameObject.AddComponent<Outline>();
                ol.effectColor = _outlineColor;
                ol.effectDistance = new Vector2(_outlineSize, -_outlineSize);
                ol.enabled = false;
                _outlines[i] = ol;
            }
            _cached = true;
        }

        /// <summary>Ve lai crosshair theo setting hien tai.</summary>
        public void ApplyFromData(SettingData d)
        {
            if (d == null) return;
            CacheParts();
            float th  = Mathf.Max(1f, d.CrosshairThickness * _thicknessScale);
            float len = Mathf.Max(0f, d.CrosshairSize * _lengthScale);
            float gap = Mathf.Max(0f, d.CrosshairGap * _gapScale);
            float off = gap + len * 0.5f;

            SetRect(_up,     new Vector2(th, len), new Vector2(0,  off));
            SetRect(_down,   new Vector2(th, len), new Vector2(0, -off));
            SetRect(_left,   new Vector2(len, th), new Vector2(-off, 0));
            SetRect(_right,  new Vector2(len, th), new Vector2( off, 0));
            SetRect(_center, new Vector2(th, th),  Vector2.zero);
            ArmOffset = off;
            HasApplied = true;

            Color col = Color.white;
            if (_palette != null && _palette.Length > 0)
                col = _palette[Mathf.Clamp(d.CrosshairColorIndex, 0, _palette.Length - 1)];
            col.a = Mathf.Clamp01(d.CrosshairOpacity / 100f);
            if (_arms != null) foreach (var img in _arms) if (img != null) img.color = col;

            if (_center != null) _center.gameObject.SetActive(d.CrosshairCenterDot);
            if (_outlines != null) foreach (var ol in _outlines) if (ol != null) ol.enabled = d.CrosshairOutline;
            Applied?.Invoke();
        }

        private void SetRect(RectTransform rt, Vector2 size, Vector2 pos)
        {
            if (rt == null) return;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }
    }
}
