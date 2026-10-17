// GaMoInspector.TopBarButtons.cs  -  part of CustomsEditor (GaMo Inspector)
// Thanh tren cung: nut Add Component + Lock + zoom
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
        // ---------- Thanh trên cùng: Add Component + Lock ----------
        void DrawTopBar()
        {
            GUILayout.Space(3);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4);
                using (new EditorGUI.DisabledScope(_go == null))
                    DrawIconSelector(_go);
                GUILayout.Space(2);

                using (new EditorGUI.DisabledScope(_go == null))
                {
                    var addContent = _addGC ?? (_addGC = new GUIContent(" Add Component", EditorGUIUtility.IconContent("Toolbar Plus").image));
                    var r = GUILayoutUtility.GetRect(addContent, TopBtnStyle, GUILayout.Height(24));

                    var oldIconSize = EditorGUIUtility.GetIconSize();
                    EditorGUIUtility.SetIconSize(new Vector2(IconPx, IconPx));
                    bool add = GUI.Button(r, addContent, TopBtnStyle);
                    EditorGUIUtility.SetIconSize(oldIconSize);

                    if (add)
                    {
                        // Ưu tiên cửa sổ Add Component GỐC của Unity (tìm kiếm / xếp hạng / New Script giống hệt Inspector gốc).
                        // Không gọi được (API nội bộ đổi theo bản Unity) thì dùng bản tự dựng bên dưới.
                        var gosAdd = ValidGos(); // thêm cho TẤT CẢ object đang chọn
                        if (TryShowNativeAddComponent(r, gosAdd)) GUIUtility.ExitGUI();
                        PopupWindow.Show(r, new AddComponentPopup(gosAdd, r.width));
                    }
                }

                const string lockTip = "Khóa GameObject đang xem, không đổi khi chọn object khác";
                GUIContent lockContent = _lock
                    ? (_lockOnGC ?? (_lockOnGC = new GUIContent(" Locked", EditorGUIUtility.IconContent("LockIcon-On").image, lockTip)))
                    : (_lockOffGC ?? (_lockOffGC = new GUIContent(" Lock", EditorGUIUtility.IconContent("LockIcon").image, lockTip)));

                var lr = GUILayoutUtility.GetRect(lockContent, TopBtnStyle, GUILayout.Width(80), GUILayout.Height(24));
                bool newLock;
                // Đặt icon size tường minh (giống nút Add Component) để icon luôn 16px bất kể kích thước gốc của texture.
                // (Đây KHÔNG phải nguyên nhân lỗi mất chữ của nút Locked; xem LockBtnStyle / DrawRounded.)
                var oldLockIconSize = EditorGUIUtility.GetIconSize();
                EditorGUIUtility.SetIconSize(new Vector2(IconPx, IconPx));
                if (_lock)
                {
                    // Nền cam + icon + chữ được vẽ trong CÙNG MỘT lần GUI.Toggle (y hệt nút Lock lúc chưa khóa, vốn hiện đúng):
                    // style là bản sao TopBtnStyle, chỉ thay nền bằng texture bo góc trắng rồi nhuộm cam bằng backgroundColor.
                    var oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = lr.Contains(Event.current.mousePosition) ? Color.Lerp(LockColor, Color.white, 0.15f) : LockColor;
                    newLock = GUI.Toggle(lr, _lock, lockContent, LockBtnStyle);
                    GUI.backgroundColor = oldBg;
                }
                else newLock = GUI.Toggle(lr, _lock, lockContent, TopBtnStyle);
                EditorGUIUtility.SetIconSize(oldLockIconSize);

                if (newLock != _lock)
                {
                    _lock = newLock;
                    _lockedObj = _lock ? (_asset != null ? _asset : (UObject)_go) : null;
                    if (!_lock) BindSelection();
                }

                // Lịch sử chọn: ‹ quay lại / › tiến tới (Alt+← / Alt+→)
                GUILayout.Space(2);
                using (new EditorGUI.DisabledScope(!CanStep(-1)))
                    if (GUILayout.Button(_backGC, TopBtnStyle, GUILayout.Width(18), GUILayout.Height(24)))
                        StepHistory(-1);
                using (new EditorGUI.DisabledScope(!CanStep(1)))
                    if (GUILayout.Button(_fwdGC, TopBtnStyle, GUILayout.Width(18), GUILayout.Height(24)))
                        StepHistory(1);

                // Zoom cỡ nút component: 2 nút nhỏ -/+ , gọn cho màn hình hẹp. Giữ Ctrl/Cmd + cuộn chuột
                // trên hàng nút component (xem DrawComponentBar) để chỉnh nhanh hơn không cần bấm nhiều lần.
                GUILayout.Space(2);
                if (_zoomOutGC == null)
                {
                    string pct = Mathf.RoundToInt(_tabScale * 100f) + "%";
                    _zoomOutGC = new GUIContent("−", "Thu nhỏ nút component (" + pct + ")");
                    _zoomInGC = new GUIContent("+", "Phóng to nút component (" + pct + ")");
                }
                using (new EditorGUI.DisabledScope(_tabScale <= MinTabScale + 0.001f))
                    if (GUILayout.Button(_zoomOutGC, TopBtnStyle, GUILayout.Width(18), GUILayout.Height(24)))
                        SetTabScale(_tabScale - 0.1f);
                using (new EditorGUI.DisabledScope(_tabScale >= MaxTabScale - 0.001f))
                    if (GUILayout.Button(_zoomInGC, TopBtnStyle, GUILayout.Width(18), GUILayout.Height(24)))
                        SetTabScale(_tabScale + 0.1f);
            }
            GUILayout.Space(3);
        }

        // ---------- Add Component gốc của Unity (reflection) ----------
        static MethodInfo _nativeAddShow;
        static bool _nativeAddInit, _nativeAddWarned;

        static bool TryShowNativeAddComponent(Rect r, GameObject[] gos)
        {
            if (!_nativeAddInit)
            {
                _nativeAddInit = true;
                try
                {
                    var t = typeof(EditorWindow).Assembly.GetType("UnityEditor.AddComponent.AddComponentWindow");
                    if (t != null)
                    {
                        // Show(Rect, GameObject[] , ...): chọn overload ít tham số nhất
                        foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            if (m.Name != "Show") continue;
                            var ps = m.GetParameters();
                            if (ps.Length < 2 || ps[0].ParameterType != typeof(Rect) || !ps[1].ParameterType.IsAssignableFrom(typeof(GameObject[]))) continue;
                            bool extraOk = true;
                            for (int i = 2; i < ps.Length; i++) if (!ps[i].IsOptional && ps[i].ParameterType.IsValueType) { extraOk = false; break; }
                            if (!extraOk) continue;
                            if (_nativeAddShow == null || ps.Length < _nativeAddShow.GetParameters().Length) _nativeAddShow = m;
                        }
                    }
                }
                catch (Exception) { _nativeAddShow = null; }
            }

            if (_nativeAddShow == null || gos == null || gos.Length == 0) return WarnNativeAddFallback();
            try
            {
                var ps = _nativeAddShow.GetParameters();
                var args = new object[ps.Length];
                args[0] = r;
                args[1] = gos;
                for (int i = 2; i < ps.Length; i++)
                {
                    object d = ps[i].IsOptional ? ps[i].DefaultValue : null;
                    args[i] = d is DBNull ? null : d;
                }
                object res = _nativeAddShow.Invoke(null, args);
                return !(res is bool ok) || ok;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is ExitGUIException) { return true; }
            catch (Exception) { _nativeAddShow = null; return WarnNativeAddFallback(); }
        }

        static bool WarnNativeAddFallback()
        {
            if (!_nativeAddWarned)
            {
                _nativeAddWarned = true;
                Debug.Log("[GaMo] Không gọi được Add Component gốc của Unity ở bản này, dùng bản tự dựng.");
            }
            return false;
        }

        GUIStyle _lockBtnStyle;

        GUIStyle LockBtnStyle
        {
            get
            {
                if (_lockBtnStyle == null)
                {
                    _lockBtnStyle = new GUIStyle(TopBtnStyle);
                    SetTextColor(_lockBtnStyle, Color.white);
                    _lockBtnStyle.fontStyle = FontStyle.Bold;
                    ApplyRoundBackground(_lockBtnStyle);
                }
                return _lockBtnStyle;
            }
        }

        void SetTabScale(float v)
        {
            v = Mathf.Clamp(Mathf.Round(v * 10f) / 10f, MinTabScale, MaxTabScale);
            if (Mathf.Approximately(v, _tabScale)) return;
            _tabScale = v;
            _scalePrefDirty = true; // ghi EditorPrefs trễ (OnInspectorUpdate / OnDisable), không ghi mỗi nấc cuộn
            Repaint();
        }

    }
}
#endif
