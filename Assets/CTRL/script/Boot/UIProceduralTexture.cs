// [Aemy 2026-10-26] FILE MOI.
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Gan texture ve bang code (ProceduralTextures) vao RawImage luc chay. Mau = RawImage.color.</summary>
    [RequireComponent(typeof(RawImage))]
    public class UIProceduralTexture : MonoBehaviour
    {
        public enum Shape { Corona, Disc, Vignette, Triangle }

        [SerializeField] private Shape _shape = Shape.Corona;
        [SerializeField] private int _size = 512;
        [Tooltip("Corona/Disc: ban kinh (0..0.5). Vignette: bat dau toi (0..1).")]
        [SerializeField] private float _a = 0.34f;
        [Tooltip("Corona: do day vien. Disc: mep mem. Vignette: toi han (0..1).")]
        [SerializeField] private float _b = 0.012f;
        [Tooltip("Corona: do toa quang. Vignette: luy thua.")]
        [SerializeField] private float _c = 0.08f;
        [Tooltip("Corona: goc hat kim cuong (do).")]
        [SerializeField] private float _beadDeg = 40f;

        private Texture2D _tex;

        private void Awake()
        {
            switch (_shape)
            {
                case Shape.Corona: _tex = Game.Boot.ProceduralTextures.Corona(_size, _a, _b, _c, _beadDeg); break;
                case Shape.Disc: _tex = Game.Boot.ProceduralTextures.Disc(_size, _a, _b); break;
                case Shape.Triangle: _tex = Game.Boot.ProceduralTextures.TriangleOutline(_size, _a); break; // _a = do day
                default: _tex = Game.Boot.ProceduralTextures.Vignette(_size, _a, _b, _c); break;
            }
            var img = GetComponent<RawImage>();
            img.texture = _tex;
            img.raycastTarget = false;
        }

        private void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }
    }
}
