using UnityEngine;
using GaMo.TrueInputSystem;

/// <summary>
/// Demo: SwapProfile(newProfile) + OnProfileSwapped event
/// Platform: PC / Gamepad / Mobile
/// ──────────────────────────────────────────────
/// Đổi toàn bộ input profile lúc runtime — vehicle mode, cutscene, UI mode.
/// Tự động clear cache, rebuild asset, reset mobile registry.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_SwapProfile))]
public class TIS_Demo_SwapProfile_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách đổi toàn bộ input profile lúc runtime,\n" +
            "  thay thế hoàn toàn bộ action map, binding và mobile button layout.\n" +
            "  Dùng khi chuyển sang trạng thái có input hoàn toàn khác biệt:\n" +
            "  lái xe, cutscene, UI mode, hoặc nhân vật thứ hai với skill khác.\n\n" +

            "PLATFORM: PC · Gamepad · Mobile\n\n" +

            "SETUP\n" +
            "1. Tạo nhiều TISInputProfileConfig.asset trong Project:\n" +
            "   • FootProfile    (WASD/Joystick bình thường)\n" +
            "   • VehicleProfile (WASD lái xe + không có skill button)\n" +
            "2. Assign các Profile asset vào 'footProfile' và 'vehicleProfile'.\n" +
            "3. Gán script này lên PlayerController / GameManager.\n\n" +

            "CHẾ ĐỘ HOẠT ĐỘNG\n" +
            "  [Foot Mode]\n" +
            "    Dùng footProfile — layout di chuyển và skill thông thường.\n" +
            "    Nhấn EnterVehicle action → chuyển sang Vehicle Mode.\n\n" +
            "  [Vehicle Mode]\n" +
            "    Dùng vehicleProfile — layout lái xe, ẩn skill button không cần thiết.\n" +
            "    Nhấn ExitVehicle action → quay lại Foot Mode.\n\n" +
            "  [OnProfileSwapped Event]\n" +
            "    Fired ngay sau khi SwapProfile() hoàn tất rebuild.\n" +
            "    Subscribe để cập nhật UI, animation state, hoặc log.\n\n" +

            "API\n" +
            "  bridge.SwapProfile(newProfileAsset)\n" +
            "  bridge.OnProfileSwapped += OnSwapped   ← subscribe event\n\n" +

            "GHI CHÚ\n" +
            "  SwapProfile rebuild toàn bộ InputActionAsset → gây ~1 frame stall.\n" +
            "  Không gọi trong Update() — chỉ gọi khi thực sự chuyển trạng thái.\n" +
            "  Nhẹ hơn: dùng SetActionMapActive() nếu chỉ cần bật/tắt một map.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/SwapProfile")]
public class TIS_Demo_SwapProfile : MonoBehaviour
{
    [Header("Profiles")]
    public TISInputProfileConfig footProfile;
    public TISInputProfileConfig vehicleProfile;

    [Header("Action kích hoạt đổi profile")]
    public string enterVehicleAction = "EnterVehicle";
    public string exitVehicleAction  = "ExitVehicle";

    private TISInputBridge _input;
    private bool _inVehicle;

    private void Start()
    {
        _input = TISInputBridge.Instance;
        _input.OnProfileSwapped += HandleProfileSwapped;
    }

    private void OnDestroy()
    {
        if (_input != null)
            _input.OnProfileSwapped -= HandleProfileSwapped;
    }

    private void Update()
    {
        if (!_inVehicle && _input.IsSkillDown(enterVehicleAction))
            EnterVehicle();
        else if (_inVehicle && _input.IsSkillDown(exitVehicleAction))
            ExitVehicle();
    }

    private void EnterVehicle()
    {
        if (vehicleProfile == null) return;
        _inVehicle = true;
        _input.SwapProfile(vehicleProfile);         // ← API chính
    }

    private void ExitVehicle()
    {
        if (footProfile == null) return;
        _inVehicle = false;
        _input.SwapProfile(footProfile);            // ← API chính
    }

    private void HandleProfileSwapped(TISInputProfileConfig profile)
    {
        Debug.Log($"[SwapProfile] Profile mới: '{profile.profileName}' — inVehicle={_inVehicle}");
    }
}
