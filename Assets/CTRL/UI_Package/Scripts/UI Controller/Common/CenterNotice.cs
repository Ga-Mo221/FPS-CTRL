using System.Collections;
using UnityEngine;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// A single centered on-screen notice line (e.g. "đang chờ xác minh email…").
    /// Show/Hide toggle this GameObject.
    /// - Show(message): hiện đến khi Hide()/Show() khác thay thế (dùng cho tiến trình dài).
    /// - Show(message, autoHideSeconds): hiện rồi TỰ TẮT sau N giây (dùng cho báo lỗi login:
    ///   hiện 5 giây rồi tắt). Nếu có Show/Hide mới trong lúc chờ thì huỷ auto-hide cũ.
    /// </summary>
    public class CenterNotice : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;

        // Tăng mỗi lần Show/Hide -> coroutine auto-hide cũ biết mình đã lỗi thời và bỏ qua.
        private int _token;

        public void Show(string message)
        {
            _token++;
            if (_label != null) _label.text = message;
            gameObject.SetActive(true);
        }

        public void Show(string message, float autoHideSeconds)
        {
            Show(message);
            if (autoHideSeconds > 0f && isActiveAndEnabled)
                StartCoroutine(AutoHideAfter(_token, autoHideSeconds));
        }

        public void Hide()
        {
            _token++;
            gameObject.SetActive(false);
        }

        private IEnumerator AutoHideAfter(int token, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (token == _token && this != null) gameObject.SetActive(false);
        }
    }
}
