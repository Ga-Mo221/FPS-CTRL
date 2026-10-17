// [Aemy 2026-10-11] FILE MOI. Am thanh cho 1 nut (UISfxManager tu gan; gan tay de doi loai am cho nut dac biet).
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Audio
{
    public class UIButtonSfx : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        [Tooltip("Am khi BAM (Button/Toggle). None = im lang.")]
        public UISfxType clickType = UISfxType.Click;
        [Tooltip("Am nho khi re chuot vao / tay cam di toi.")]
        public bool hoverSound = true;

        private Selectable _sel;
        private bool _hooked;

        private void Awake() { Hook(); }

        private void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            _sel = GetComponent<Selectable>();
            var b = _sel as Button;
            if (b != null) b.onClick.AddListener(OnClick);
            var tg = _sel as Toggle;
            if (tg != null) tg.onValueChanged.AddListener(_ => OnClick());
        }

        private void OnDestroy()
        {
            var b = _sel as Button;
            if (b != null) b.onClick.RemoveListener(OnClick);
        }

        private void OnClick() { UISfxManager.Play(clickType); }

        private bool Interactable { get { return _sel == null || _sel.IsInteractable(); } }

        private static bool UsingGamepad
        {
            get { var br = GaMo.TrueInputSystem.TISInputBridge.Instance; return br != null && br.IsGamepad; }
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (hoverSound && Interactable && !UsingGamepad) UISfxManager.PlayHover();
        }

        public void OnSelect(BaseEventData e)
        {
            if (hoverSound && Interactable && UsingGamepad) UISfxManager.PlayHover();
        }
    }
}
