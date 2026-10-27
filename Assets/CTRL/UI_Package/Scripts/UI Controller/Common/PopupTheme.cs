// [Aemy 2026-10-11] FILE MOI. Bo nhan dien chung cho popup/overlay dung bang code (vd bang chon avatar).
// Asset: Resources/PopupTheme.asset (cung anh nen + khung voi ModalDialog/InvitePopup trong MenuShell).
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    [CreateAssetMenu(menuName = "Eclipse Strike/Popup Theme", fileName = "PopupTheme")]
    public class PopupTheme : ScriptableObject
    {
        [Header("Anh")]
        public Sprite background;                 // Popup_BG.jpg
        public Sprite frame;                      // Popup_Frame.png (9-slice)
        public float framePixelsPerUnit = 1.4f;   // giong PopupFrame cua ModalDialog
        [Header("Chu")]
        public TMP_FontAsset titleFont;           // ChakraPetch-Bold
        public TMP_FontAsset bodyFont;            // ChakraPetch-Regular
        public Color titleColor = Color.white;
        public Color bodyColor = new Color(0.80f, 0.82f, 0.85f, 1f);
        [Header("Mau")]
        public Color accent = new Color(0.31f, 0.84f, 0.79f, 1f);       // teal cua game
        public Color cellColor = new Color(0.05f, 0.09f, 0.10f, 0.85f);  // o/the ben trong popup
        public Color dim = new Color(0f, 0f, 0f, 0.72f);

        private static PopupTheme s_cached;
        /// <summary>Theme mac dinh (Resources/PopupTheme). Null neu chua co asset -> caller tu dung mau cu.</summary>
        public static PopupTheme Default
        {
            get { if (s_cached == null) s_cached = Resources.Load<PopupTheme>("PopupTheme"); return s_cached; }
        }

        /// <summary>Phu anh nen (cat vua khung, khong meo) + khung vien len 1 panel. Tra ve false neu thieu anh.</summary>
        public bool DressPanel(Image panel)
        {
            if (panel == null || background == null) return false;
            panel.sprite = null;
            panel.color = Color.black;   // nen lot duoi anh
            if (panel.GetComponent<RectMask2D>() == null) panel.gameObject.AddComponent<RectMask2D>();

            var bgGo = new GameObject("PopupBG", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            bgGo.transform.SetParent(panel.transform, false);
            bgGo.transform.SetAsFirstSibling();
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = background; bgImg.raycastTarget = false;
            var fit = bgGo.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = background.rect.width / Mathf.Max(1f, background.rect.height);

            if (frame != null)
            {
                var frGo = new GameObject("PopupFrame", typeof(RectTransform), typeof(Image));
                frGo.transform.SetParent(panel.transform, false);
                var rt = (RectTransform)frGo.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var fi = frGo.GetComponent<Image>();
                fi.sprite = frame; fi.type = Image.Type.Sliced; fi.pixelsPerUnitMultiplier = framePixelsPerUnit; fi.raycastTarget = false;
                frGo.transform.SetAsLastSibling();   // khung nam tren noi dung
            }
            return true;
        }

        public void StyleTitle(TMP_Text t) { if (t == null) return; if (titleFont != null) t.font = titleFont; t.color = titleColor; }
        public void StyleBody(TMP_Text t)  { if (t == null) return; if (bodyFont != null) t.font = bodyFont; t.color = bodyColor; }
    }
}
