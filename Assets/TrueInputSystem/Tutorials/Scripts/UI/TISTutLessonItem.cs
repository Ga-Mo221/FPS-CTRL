// ============================================================
//  TrueInputSystem Tutorials — TISTutLessonItem.cs
//  Dòng trong danh sách bài học (số · tên · trạng thái ▶ / ✓ / ○).
// ============================================================

using TMPro;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Lesson Item")]
    public class TISTutLessonItem : MonoBehaviour
    {
        [SerializeField] TISTutButton _button;
        [SerializeField] TMP_Text     _icon;

        public TISTutButton Button => _button;

        public void Setup(int number, string title)
        {
            if (_button != null) _button.Text = $"{number:00}  {title}";
        }

        /// <param name="current">bài đang mở</param>
        /// <param name="done">đã hoàn thành mọi nhiệm vụ</param>
        public void SetState(bool current, bool done)
        {
            var th = TISTutTheme.Current;
            if (_button != null) _button.IsOn = current;
            if (_icon == null) return;
            _icon.text  = current ? "▶" : done ? "✓" : "○";
            _icon.color = current ? th.Accent() : done ? th.success : th.inkMuted;
        }
    }
}
