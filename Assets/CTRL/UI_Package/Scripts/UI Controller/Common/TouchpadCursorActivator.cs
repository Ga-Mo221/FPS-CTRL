// [Aemy 2026-10-19] FILE MOI. Touchpad tay cam PlayStation -> con tro o menu.
using UnityEngine;
using GaMo.TrueInputSystem;

namespace Game.UI
{
    /// <summary>
    /// Gan cung GameObject voi TISGamepadVirtualCursor (MenuShell).
    /// Nguoi choi cham / vuot touchpad (DualShock 4 / DualSense) -> bat con tro ao: vuot = di chuot,
    /// bam touchpad hoac A = click. Bam D-pad / LB / RB / LT / RT hoac dung chuot that -> TIS tu tat con tro
    /// va tra focus ve nut nhu cu. SRP: chi quyet dinh KHI NAO bat con tro; di chuyen / click do TIS lo.
    /// </summary>
    [RequireComponent(typeof(TISGamepadVirtualCursor))]
    public class TouchpadCursorActivator : MonoBehaviour
    {
        private TISGamepadVirtualCursor _cursor;
        private TISInputBridge _registeredTo;

        private void Awake() { _cursor = GetComponent<TISGamepadVirtualCursor>(); }

        private void Update()
        {
            var b = TISInputBridge.Instance;
            if (b == null || _cursor == null) return;

            // Bridge sinh sau / doi bridge -> dang ky lai (TIS chi dang ky trong Awake cua cursor).
            if (b != _registeredTo) { b.RegisterGamepadCursor(_cursor); _registeredTo = b; }

            if (_cursor.GetEnabled()) return;
            if (TISGamepadUIButton.ExternalSuppress > 0) return;   // overlay (chon nhan vat...) dang mo
            var m = b.Motion;
            if (m == null || !m.HasTouchpad) return;
            if (m.IsTouching || m.TouchpadPressedThisFrame) b.SetActiveGamepadCursor(true);
        }

        // Roi menu (vao tran) khi con tro dang bat -> tat han de bridge khong con nghi "cursor dang bat"
        // (gyro ngam bi chan khi cursor bat).
        private void OnDisable()
        {
            if (_cursor != null && _cursor.GetEnabled()) _cursor.SetActiveCursor(false);
        }
    }
}
