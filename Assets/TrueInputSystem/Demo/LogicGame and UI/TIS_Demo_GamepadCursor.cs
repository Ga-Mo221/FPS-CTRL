using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: TISCameraTarget — GestureCentroidPosition / GestureGetFingerPosition
/// Platform: Mobile (iOS / Android)
/// ──────────────────────────────────────────────
/// Sử dụng vị trí centroid ngón tay trong camera zone để
/// xoay hoặc nhìn theo hướng ngón tay chỉ (look-at-touch).
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_GamepadCursor))]
public class TIS_Demo_GamepadCursor_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "PLATFORM: Mobile (iOS / Android)\n\n" +
            "SETUP\n" +
            "1. Scene cần có TISInputBridge + TISMobileInputSetup.\n" +
            "2. Gán script này GameObject trong scene.\n" +
            "API\n" +
            "  bridge.SetActiveGamepadCursor(bool) \n" +
            "  bridge.GetGamepadCursorEnable() →  (bool) enable\n",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/GamepadCursor")]
public class TIS_Demo_GamepadCursor : MonoBehaviour
{
    private TISInputBridge _input;

    private void Awake()
    {
        _input = TISInputBridge.Instance;
    }

    private void Update()
    {
        if (_input.IsRunning)
        {
            _input.SetActiveGamepadCursor(!_input.GetGamepadCursorEnable());
        }
    }
}
