// [Aemy 2026-10-26] FILE MOI.
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace Game.Boot
{
    /// <summary>
    /// "Nhan phim bat ky": ban phim, nut chuot, tay cam, cham man hinh (Input System onAnyButtonPress).
    /// Chi bao 1 lan cho moi lan Listen(). Khong biet gi ve man tieu de / scene (SRP).
    /// </summary>
    public class AnyKeyListener : MonoBehaviour
    {
        private IDisposable _sub;
        private Action _callback;

        public void Listen(Action onPressed)
        {
            Stop();
            _callback = onPressed;
            _sub = InputSystem.onAnyButtonPress.CallOnce(OnPressed);
        }

        public void Stop()
        {
            _sub?.Dispose();
            _sub = null;
            _callback = null;
        }

        private void OnPressed(InputControl control)
        {
            Debug.Log("[Boot] Nhan phim: " + (control != null ? control.path : "(code)"));
            var cb = _callback;
            _callback = null;
            _sub = null;
            cb?.Invoke();
        }

        private void OnDisable() => Stop();
    }
}
