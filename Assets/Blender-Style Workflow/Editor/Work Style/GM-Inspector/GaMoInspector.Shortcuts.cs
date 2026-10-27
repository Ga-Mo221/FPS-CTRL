// GaMoInspector.Shortcuts.cs  -  part of CustomsEditor (GaMo Inspector)
// Lich su chon Back/Forward (dieu khien bang Shift + lan chuot, xem OnRootWheel)
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UEditor = UnityEditor.Editor;
using UIE = UnityEngine.UIElements;
using UEUI = UnityEditor.UIElements;
using UObject = UnityEngine.Object;

namespace GaMo.BlenderEditorTools
{
    public partial class CustomsEditor
    {
        double _histWheelAt; // chặn nhảy quá nhiều object khi Shift+cuộn nhanh

        void PushHistory(UObject o)
        {
            if (o == null) return;
            if (_histIndex >= 0 && _histIndex < _history.Count && _history[_histIndex] == o) return; // đang đứng ở đúng mục này

            if (_histIndex < _history.Count - 1)
                _history.RemoveRange(_histIndex + 1, _history.Count - _histIndex - 1); // bỏ nhánh "tiến tới" cũ
            _history.Add(o);
            if (_history.Count > MaxHistory) _history.RemoveAt(0);
            _histIndex = _history.Count - 1;
        }

        bool CanStep(int dir)
        {
            for (int i = _histIndex + dir; i >= 0 && i < _history.Count; i += dir)
                if (_history[i] != null) return true;
            return false;
        }

        void StepHistory(int dir)
        {
            for (int i = _histIndex + dir; i >= 0 && i < _history.Count; i += dir)
            {
                var o = _history[i];
                if (o == null) continue; // đã bị xóa => bỏ qua
                _histIndex = i;
                Selection.activeObject = o;
                if (_lock) // đang khóa thì OnSel không bind => tự bind
                {
                    BindToObject(o);
                    _lockedObj = o;
                    GUIUtility.ExitGUI();
                }
                return;
            }
        }

        // =====================================================
        //  Menu component
        // =====================================================
    }
}
#endif