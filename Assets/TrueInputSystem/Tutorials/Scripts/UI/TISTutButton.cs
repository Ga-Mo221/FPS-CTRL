// ============================================================
//  TrueInputSystem Tutorials — TISTutButton.cs
//
//  Nút kiểu "DISCOVER OUR PLATFORM [→]":
//    • Thân (body) nghiêng một cạnh + ô mũi tên (cap) tách rời
//    • Bình thường : thân than chì · cap màu nhấn
//    • Hover/Chọn  : ĐẢO màu (thân màu nhấn · cap than chì), mũi tên nhích ra
//    • Nhấn        : co nhẹ 0.97
//
//  Kế thừa Selectable → điều hướng được bằng tay cầm / bàn phím (EventSystem).
//  Style Ghost + IsOn dùng cho tab: tắt = trắng viền, bật = than chì.
// ============================================================

using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Button")]
    public class TISTutButton : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public enum Style
        {
            Dark   = 0,   // thân than chì, cap màu nhấn
            Accent = 1,   // thân màu nhấn, cap than chì
            Ghost  = 2,   // thân trắng viền mảnh (tab, nút phụ)
        }

        [Header("Thành phần")]
        [SerializeField] TISTutShape _body;
        [SerializeField] TISTutShape _cap;
        [SerializeField] TMP_Text    _label;
        [SerializeField] TMP_Text    _capIcon;

        [Header("Kiểu")]
        [SerializeField] Style _style = Style.Dark;
        [SerializeField] bool  _isOn;
        [SerializeField, Tooltip("Mũi tên nhích theo hướng này khi hover (px).")]
        Vector2 _capNudge = new Vector2(6f, 0f);

        [Space]
        public UnityEvent onClick = new UnityEvent();

        float   _hot;          // 0 = thường, 1 = hover/chọn
        float   _hotTarget;
        float   _press = 1f;
        float   _pressTarget = 1f;
        Vector2 _capHome;
        bool    _capHomeSet;

        public TMP_Text Label => _label;

        /// <summary>Gắn các phần đã dựng (TISTutUI.Button gọi).</summary>
        public void Setup(TISTutShape body, TISTutShape cap, TMP_Text label, TMP_Text capIcon, Style style, Vector2 capNudge)
        {
            _body = body; _cap = cap; _label = label; _capIcon = capIcon;
            _style = style; _capNudge = capNudge;
            _capHomeSet = false;
            targetGraphic = body;
            transition = Transition.None;
            CacheCapHome();
            ApplyVisual();
        }

        public string Text
        {
            get => _label != null ? _label.text : "";
            set { if (_label != null) _label.text = value; }
        }

        public bool IsOn
        {
            get => _isOn;
            set { if (_isOn == value) return; _isOn = value; ApplyVisual(); }
        }

        public Style ButtonStyle
        {
            get => _style;
            set { _style = value; ApplyVisual(); }
        }

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;   // tự vẽ trạng thái
            // Khung hướng dẫn không dùng điều hướng tự động — cần analog dành cho sân chơi.
            // (Điều hướng UI bằng tay cầm có bài riêng với TISGamepadUIButton.)
            navigation = new Navigation { mode = Navigation.Mode.None };
            CacheCapHome();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _hot = _hotTarget = 0f;
            _press = _pressTarget = 1f;
            ApplyVisual();
        }

        void CacheCapHome()
        {
            if (_capHomeSet || _cap == null) return;
            _capHome = _cap.rectTransform.anchoredPosition;
            _capHomeSet = true;
        }

        // ── Trạng thái từ Selectable ───────────────────────────

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            _hotTarget   = state == SelectionState.Highlighted || state == SelectionState.Selected
                        || state == SelectionState.Pressed ? 1f : 0f;
            _pressTarget = state == SelectionState.Pressed ? 0.97f : 1f;
            if (instant || !Application.isPlaying)
            {
                _hot = _hotTarget;
                _press = _pressTarget;
            }
            ApplyVisual();
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            float dt = Time.unscaledDeltaTime;
            bool changed = false;
            if (!Mathf.Approximately(_hot, _hotTarget))     { _hot   = Mathf.MoveTowards(_hot, _hotTarget, dt * 7f);     changed = true; }
            if (!Mathf.Approximately(_press, _pressTarget)) { _press = Mathf.MoveTowards(_press, _pressTarget, dt * 2f); changed = true; }
            if (changed) ApplyVisual();
        }

        // ── Vẽ ─────────────────────────────────────────────────

        public void ApplyVisual()
        {
            var th = TISTutTheme.Current;
            Color ink = th.ink, accent = th.Accent(), paper = th.paper, card = th.card;
            float t = Smooth(_hot);
            bool interactableNow = IsInteractable();

            Color bodyA, bodyB, labelA, labelB, capA, capB, iconA, iconB, borderA, borderB;
            float borderW = 0f;

            switch (_style)
            {
                case Style.Accent:
                    bodyA = accent; labelA = ink;   capA = ink;    iconA = accent;
                    bodyB = ink;    labelB = paper; capB = accent; iconB = ink;
                    borderA = borderB = Color.clear;
                    break;

                case Style.Ghost:
                    if (_isOn)
                    {
                        bodyA = bodyB = ink; labelA = labelB = paper;
                        borderA = borderB = ink;
                    }
                    else
                    {
                        bodyA = card;   labelA = ink;
                        bodyB = accent; labelB = ink;
                        borderA = th.line; borderB = ink;
                        borderW = 1.5f;
                    }
                    capA = ink; capB = accent; iconA = accent; iconB = ink;
                    break;

                default: // Dark
                    bodyA = ink;    labelA = paper; capA = accent; iconA = ink;
                    bodyB = accent; labelB = ink;   capB = ink;    iconB = accent;
                    borderA = borderB = Color.clear;
                    break;
            }

            float alpha = interactableNow ? 1f : 0.4f;

            if (_body != null)
            {
                _body.color = WithA(Color.Lerp(bodyA, bodyB, t), alpha);
                _body.BorderWidth = borderW;
                _body.BorderColor = Color.Lerp(borderA, borderB, t);
            }
            if (_label != null)   _label.color   = WithA(Color.Lerp(labelA, labelB, t), alpha);
            if (_cap != null)
            {
                _cap.color = WithA(Color.Lerp(capA, capB, t), alpha);
                if (Application.isPlaying)
                {
                    CacheCapHome();
                    _cap.rectTransform.anchoredPosition = _capHome + _capNudge * t;
                }
            }
            if (_capIcon != null) _capIcon.color = WithA(Color.Lerp(iconA, iconB, t), alpha);

            transform.localScale = new Vector3(_press, _press, 1f);
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);
        static Color WithA(Color c, float a) { c.a *= a; return c; }

        // ── Click ──────────────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Press();
            // Bỏ chọn sau khi click: nếu không, Space / nút A (Submit) sẽ bấm lại nút này
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            Press();
            DoStateTransition(SelectionState.Pressed, false);
            CancelInvoke(nameof(ReleaseSubmit));
            Invoke(nameof(ReleaseSubmit), 0.1f);
        }

        void ReleaseSubmit() => DoStateTransition(currentSelectionState, false);

        void Press()
        {
            if (!IsActive() || !IsInteractable()) return;
            onClick.Invoke();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            transition = Transition.None;
            ApplyVisual();
        }
#endif
    }
}
