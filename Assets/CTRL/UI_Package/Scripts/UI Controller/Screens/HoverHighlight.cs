using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// Bat/tat 1 object "highlight" (nen nut da chon) theo trang thai hover.
    /// Dung cho muc menu Pause: dua con tro vao -> hien Row highlight; roi ra -> an.
    /// CACH DUNG:
    ///   - Gan script nay vao vung click cua muc (object CO Graphic raycast, vd Row).
    ///   - Keo object nen-highlight (Row/bg cua nut da chon) vao truong _highlight.
    ///   - Object _highlight nen tat san (inactive) o trang thai mac dinh.
    /// Nut bam van hoat dong binh thuong vi script chi bat/tat _highlight, khong dung click.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class HoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("Object nen highlight se HIEN khi hover va AN khi roi chuot (vd Row).")]
        [SerializeField] private GameObject _highlight;

        // Con tro di VAO muc -> bat highlight.
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_highlight != null) _highlight.SetActive(true);
        }

        // Con tro RA khoi muc -> tat highlight.
        public void OnPointerExit(PointerEventData eventData)
        {
            if (_highlight != null) _highlight.SetActive(false);
        }

        // Khi mo panel: dam bao highlight tat (chua hover gi).
        private void OnEnable()
        {
            if (_highlight != null) _highlight.SetActive(false);
        }

        // Khi dong panel: reset highlight ve tat.
        private void OnDisable()
        {
            if (_highlight != null) _highlight.SetActive(false);
        }
    }
}
