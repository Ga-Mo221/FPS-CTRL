// ============================================================
//  TrueInputSystem Tutorials — TISTutObjectiveRow.cs
//  Dòng nhiệm vụ "Thử ngay" — ô tích bật lên khi hoàn thành.
// ============================================================

using TMPro;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Objective Row")]
    public class TISTutObjectiveRow : MonoBehaviour
    {
        [SerializeField] TISTutShape _check;
        [SerializeField] TMP_Text    _checkIcon;
        [SerializeField] TMP_Text    _text;

        float _pop;

        public void Setup(string text)
        {
            if (_text != null) _text.text = text;
            SetDone(false, true);
        }

        public void SetDone(bool done, bool instant = false)
        {
            var th = TISTutTheme.Current;
            if (_check != null)
            {
                _check.color       = done ? th.AccentStrong() : th.card;
                _check.BorderColor = done ? th.AccentStrong() : th.line;
            }
            if (_checkIcon != null) { _checkIcon.text = done ? "✓" : ""; _checkIcon.color = th.ink; }
            if (_text != null)      _text.color = done ? th.inkSoft : th.ink;
            if (done && !instant)   _pop = 1f;
        }

        void Update()
        {
            if (_pop <= 0f || _check == null) return;
            _pop = Mathf.MoveTowards(_pop, 0f, Time.unscaledDeltaTime * 4f);
            float s = 1f + 0.35f * Mathf.Sin(_pop * Mathf.PI);
            _check.rectTransform.localScale = new Vector3(s, s, 1f);
        }
    }
}
