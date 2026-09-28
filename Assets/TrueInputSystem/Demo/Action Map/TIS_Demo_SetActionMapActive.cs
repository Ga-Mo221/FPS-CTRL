using UnityEngine;
using GaMo.TrueInputSystem;

/// <summary>
/// Demo: SetActionMapActive(mapName, bool)
/// Platform: PC / Gamepad / Mobile
/// ──────────────────────────────────────────────
/// Bật/tắt toàn bộ một Action Map lúc runtime.
/// Dùng để tắt skill input khi vào cutscene, tắt movement khi mở menu.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_SetActionMapActive))]
public class TIS_Demo_SetActionMapActive_Editor : UnityEditor.Editor
{
        public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách bật/tắt toàn bộ một Action Map lúc runtime,\n" +
            "  cho phép chuyển đổi nhóm input giữa các trạng thái game khác nhau.\n" +
            "  Dùng để tắt movement khi mở menu, tắt skill input khi vào cutscene,\n" +
            "  hoặc vô hiệu hóa toàn bộ input trong các trình tự tự động (Timeline).\n\n" +

            "PLATFORM: PC · Gamepad · Mobile\n\n" +

            "SETUP\n" +
            "1. Trong TISInputProfileConfig → tạo ít nhất 2 Action Map:\n" +
            "   • 'Gameplay' (Movement + Skill)\n" +
            "   • 'UI'       (Navigate + Confirm + Cancel)\n" +
            "2. Điền tên map vào 'gameplayMap' và 'uiMap'.\n" +
            "3. Gán script này lên GameStateManager / UIController.\n\n" +

            "CHẾ ĐỘ HOẠT ĐỘNG\n" +
            "  [Gameplay Mode]\n" +
            "    Gameplay ON · UI OFF → nhân vật nhận input di chuyển và skill bình thường.\n\n" +
            "  [Menu Mode]\n" +
            "    Gameplay OFF · UI ON → chỉ nhận input điều hướng UI, chặn toàn bộ gameplay.\n\n" +
            "  [Cutscene Mode]\n" +
            "    Gameplay OFF · UI OFF → vô hiệu hóa tất cả map, gọi DisableAllInput().\n" +
            "    Có thể trigger qua UnityEvent, Timeline hoặc AnimationEvent.\n\n" +

            "API\n" +
            "  bridge.SetActionMapActive(\"Gameplay\", true/false)\n" +
            "  bridge.SetActionMapActive(\"UI\",       true/false)\n" +
            "  EnableGameplay()   → public, gọi được từ UnityEvent / Timeline\n" +
            "  EnableUIOnly()     → public, gọi được từ UnityEvent / Timeline\n" +
            "  DisableAllInput()  → public, gọi được từ UnityEvent / Timeline\n\n" +

            "GHI CHÚ\n" +
            "  Nhẹ hơn SwapProfile: chỉ enable/disable map, không rebuild asset.\n" +
            "  Tên map phải khớp chính xác (case-insensitive) với mapName trong Profile.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/SetActionMapActive")]
public class TIS_Demo_SetActionMapActive : MonoBehaviour
{
    [Header("Action Map Names")]
    public string gameplayMap = "Gameplay";
    public string uiMap       = "UI";

    [Header("Action mở menu")]
    public string openMenuAction = "Menu";

    private TISInputBridge _input;
    private bool _menuOpen;

    private void Start()
    {
        _input = TISInputBridge.Instance;
        SetGameplayMode();
    }

    private void Update()
    {
        if (_input.IsSkillDown(openMenuAction))
        {
            _menuOpen = !_menuOpen;
            if (_menuOpen) SetMenuMode();
            else            SetGameplayMode();
        }
        
        LogTest("Log");
    }

    void LogTest(string ActiveName)
    {
        if (_input.GetSkillAction(ActiveName))
        {
            if (!_menuOpen) Debug.Log("Đang sử dụng Map Gameplay");
            else Debug.Log("Đang sử dụng Map UI");
        }
    }

    private void SetGameplayMode()
    {
        _input.SetActionMapActive(gameplayMap, true);    // ← API chính
        _input.SetActionMapActive(uiMap, false);
        Debug.Log("[SetActionMapActive] Gameplay ON, UI OFF");
    }

    private void SetMenuMode()
    {
        _input.SetActionMapActive(gameplayMap, false);   // ← API chính
        _input.SetActionMapActive(uiMap, true);
        Debug.Log("[SetActionMapActive] Gameplay OFF, UI ON");
    }

    // Gọi từ UnityEvent (Timeline, AnimationEvent, v.v.)
    public void EnableGameplay()  => SetGameplayMode();
    public void EnableUIOnly()    => SetMenuMode();
    public void DisableAllInput()
    {
        _input.SetActionMapActive(gameplayMap, false);
        _input.SetActionMapActive(uiMap, false);
        Debug.Log("[SetActionMapActive] Tất cả map đã TẮT (cutscene mode).");
    }
}
