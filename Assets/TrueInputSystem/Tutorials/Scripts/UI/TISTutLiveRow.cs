// ============================================================
//  TrueInputSystem Tutorials — TISTutLiveRow.cs
//
//  Một dòng trong bảng "API đang chạy": tên API · giá trị · thanh mức (tuỳ chọn).
//  Bài học đăng ký bằng Watch("LookDelta", () => ..., () => 0..1).
// ============================================================

using System;
using TMPro;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Live Row")]
    public class TISTutLiveRow : MonoBehaviour
    {
        [SerializeField] TMP_Text      _name;
        [SerializeField] TMP_Text      _value;
        [SerializeField] RectTransform _barRoot;
        [SerializeField] TISTutShape   _barTrack;
        [SerializeField] TISTutShape   _barFill;

        Func<string> _getValue;
        Func<float>  _getBar;
        string       _lastText;
        float        _bar;

        public void Bind(string apiName, Func<string> value, Func<float> bar01 = null)
        {
            _getValue = value;
            _getBar   = bar01;
            _lastText = null;
            _bar      = 0f;
            if (_name != null) _name.text = apiName;
            if (_barRoot != null) _barRoot.gameObject.SetActive(bar01 != null);

            var th = TISTutTheme.Current;
            if (_name != null)     _name.color     = th.ink;
            if (_value != null)    _value.color    = th.ink;
            if (_barTrack != null) _barTrack.color = th.track;
            if (_barFill != null)  _barFill.color  = th.AccentStrong();
            Refresh(true);
        }

        /// <param name="force">bỏ qua làm mượt, ghi lại chữ</param>
        /// <param name="text">cập nhật chữ (controller giới hạn tần số); thanh mức luôn cập nhật</param>
        public void Refresh(bool force = false, bool text = true)
        {
            if ((text || force) && _getValue != null && _value != null)
            {
                string s;
                try { s = _getValue(); }
                catch (Exception) { s = "—"; }
                if (force || s != _lastText) { _value.text = s; _lastText = s; }
            }

            if (_getBar != null && _barFill != null)
            {
                float target;
                try { target = Mathf.Clamp01(_getBar()); }
                catch (Exception) { target = 0f; }
                _bar = force ? target : Mathf.Lerp(_bar, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
                var rt = _barFill.rectTransform;
                rt.anchorMax = new Vector2(Mathf.Max(0.001f, _bar), rt.anchorMax.y);
            }
        }
    }
}
