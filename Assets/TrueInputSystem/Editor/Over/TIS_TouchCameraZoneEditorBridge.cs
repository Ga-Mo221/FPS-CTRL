#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;

namespace GaMo.TrueInputSystem.Editor
{
    [InitializeOnLoad]
    public static class TISTouchCameraZoneEditorBridge
    {
        static TISTouchCameraZoneEditorBridge()
        {
            TISTouchCameraZone.EditorUpdateOverride = HandleMouseFallback;
        }

        private static void HandleMouseFallback(TISTouchCameraZone zone)
        {
            // Truy cập các field private qua các internal helper methods
            // được expose qua EditorAccessor (xem bên dưới)
            var accessor = new ZoneAccessor(zone);

            var mouse = Mouse.current;
            if (mouse == null) return;

            float normX  = mouse.position.x.ReadValue() / Screen.width;
            bool  inZone = normX >= zone.ZoneStartRatio && normX <= zone.ZoneEndRatio;

            if (mouse.leftButton.wasPressedThisFrame && inZone)
            {
                accessor.SetMouseActive(true);
                accessor.SetLastMousePos(mouse.position.ReadValue());
                accessor.SetSmoothedDelta(Vector2.zero);
                accessor.SetHasInertia(false);
            }

            if (mouse.leftButton.isPressed && accessor.MouseActive)
            {
                Vector2 currentPos = mouse.position.ReadValue();
                Vector2 delta      = currentPos - accessor.LastMousePos;
                accessor.SetLastMousePos(currentPos);
                accessor.InvokeApplyDelta(delta);
            }

            // Editor: scroll wheel = zoom
            if (accessor.EnablePinchZoom)
            {
                float scroll = mouse.scroll.y.ReadValue();
                if (Mathf.Abs(scroll) > 0.1f)
                {
                    float zoomDelta = scroll * accessor.PinchZoomSpeed * 0.5f;
                    accessor.SetPinchZoomDelta(zoomDelta);
                    accessor.OverrideZoomInput(zoomDelta);
                }
                else
                {
                    accessor.SetPinchZoomDelta(0f);
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame && accessor.MouseActive)
            {
                accessor.SetMouseActive(false);
                accessor.SetHasInertia(accessor.SmoothedDeltaSqrMag > 0.001f);
            }

            if (!accessor.MouseActive)
                accessor.InvokeHandleInertia();
        }

        // ── Accessor dùng Reflection để đọc/ghi field private của Runtime DLL ──

        private readonly struct ZoneAccessor
        {
            private static readonly System.Reflection.FieldInfo  _fMouseActive     = typeof(TISTouchCameraZone).GetField("_mouseActive",     Flags);
            private static readonly System.Reflection.FieldInfo  _fLastMousePos    = typeof(TISTouchCameraZone).GetField("_lastMousePos",    Flags);
            private static readonly System.Reflection.FieldInfo  _fSmoothedDelta   = typeof(TISTouchCameraZone).GetField("_smoothedDelta",   Flags);
            private static readonly System.Reflection.FieldInfo  _fHasInertia      = typeof(TISTouchCameraZone).GetField("_hasInertia",      Flags);
            private static readonly System.Reflection.FieldInfo  _fEnablePinch     = typeof(TISTouchCameraZone).GetField("_enablePinchZoom", Flags);
            private static readonly System.Reflection.FieldInfo  _fPinchZoomSpeed  = typeof(TISTouchCameraZone).GetField("_pinchZoomSpeed",  Flags);
            private static readonly System.Reflection.PropertyInfo _pPinchZoomDelta = typeof(TISTouchCameraZone).GetProperty("PinchZoomDelta");
            private static readonly System.Reflection.MethodInfo _mApplyDelta      = typeof(TISTouchCameraZone).GetMethod("ApplyDelta",      Flags);
            private static readonly System.Reflection.MethodInfo _mHandleInertia   = typeof(TISTouchCameraZone).GetMethod("HandleInertia",   Flags);
            private static readonly System.Reflection.MethodInfo _mOverrideZoom    = typeof(TISTouchCameraZone).GetMethod("OverrideZoomViaAccessor", Flags);

            private const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            private readonly TISTouchCameraZone _zone;

            public ZoneAccessor(TISTouchCameraZone zone) => _zone = zone;

            public bool    MouseActive          => (bool)   (_fMouseActive?.GetValue(_zone) ?? false);
            public Vector2 LastMousePos         => (Vector2)(_fLastMousePos?.GetValue(_zone) ?? Vector2.zero);
            public float   SmoothedDeltaSqrMag  => ((Vector2)(_fSmoothedDelta?.GetValue(_zone) ?? Vector2.zero)).sqrMagnitude;
            public bool    EnablePinchZoom      => (bool)   (_fEnablePinch?.GetValue(_zone) ?? false);
            public float   PinchZoomSpeed       => (float)  (_fPinchZoomSpeed?.GetValue(_zone) ?? 0.01f);

            public void SetMouseActive(bool v)        => _fMouseActive?.SetValue(_zone, v);
            public void SetLastMousePos(Vector2 v)    => _fLastMousePos?.SetValue(_zone, v);
            public void SetSmoothedDelta(Vector2 v)   => _fSmoothedDelta?.SetValue(_zone, v);
            public void SetHasInertia(bool v)         => _fHasInertia?.SetValue(_zone, v);
            public void SetPinchZoomDelta(float v)    => _pPinchZoomDelta?.SetValue(_zone, v);

            public void InvokeApplyDelta(Vector2 delta)  => _mApplyDelta?.Invoke(_zone, new object[] { delta });
            public void InvokeHandleInertia()            => _mHandleInertia?.Invoke(_zone, null);
            public void OverrideZoomInput(float delta)   => _mOverrideZoom?.Invoke(_zone, new object[] { delta });
        }
    }
}
#endif
