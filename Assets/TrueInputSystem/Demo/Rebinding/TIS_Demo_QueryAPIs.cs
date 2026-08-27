using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrueInputSystem;

/// <summary>
/// Demo: FindConflict / GetActivePath / GetDisplayName
/// ──────────────────────────────────────────────────────────────────────────────────────
/// Minh hoạ nhóm API truy vấn — dùng để hiển thị binding lên Settings UI
/// và kiểm tra conflict trước khi cho phép lưu.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_QueryAPIs))]
public class TIS_Demo_QueryAPIs_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH :\n" +
            "   Test nhóm API truy vấn — dùng để populate Settings UI và kiểm tra conflict trước khi lưu.\n \n" +
            "===================================\n \n"+
            "CÁCH TEST :\n" +
            "   1.Nhấn Check Active Path → LblActivePath hiện cả raw path lẫn display name thân thiện (VD: <Keyboard>/space → \"Space\").\n"+
            "   2.Nhấn Check Conflict No → kiểm tra pathNoConflict (mặc định <Keyboard>/g), nếu chưa ai dùng → [OK] Không conflict.\n"+
            "   3.Nhấn Check Conflict Yes → kiểm tra pathWithConflict, kết quả hiện [X] CONFLICT — Dùng bởi: \"Jump\" hoặc tên action đang chiếm phím đó.\n"+
            "   4.Chạy cùng với demo ForceApply để thay đổi binding rồi query lại — xác nhận GetActivePath trả về override, không phải default.\n \n"+
            "===================================\n \n"+
            "SETUP\n" +
            "1. Gán script này lên bất kỳ GameObject nào trong scene.\n" +
            "2. Đảm bảo scene có TISInputBridge (singleton).\n" +
            "3. Tạo Canvas → gán đủ UI reference bên dưới.\n" +
            "   · BtnCheckActivePath  — Lấy path đang active của action.\n" +
            "   · BtnCheckConflictNo  — Kiểm tra path không conflict (<Keyboard>/g).\n" +
            "   · BtnCheckConflictYes — Kiểm tra path có conflict (cùng phím với Jump).\n" +
            "   · LblActivePath       — Text hiện GetActivePath + GetDisplayName.\n" +
            "   · LblConflict         — Text hiện kết quả FindConflict.\n\n" +
            "API ĐƯỢC TEST\n" +
            "  TISRebindingSystem.FindConflict(bridge, skipAction, target, path)\n" +
            "    → string? : tên action conflict, hoặc null nếu path còn trống.\n" +
            "  TISRebindingSystem.GetActivePath(bridge, actionName, target)\n" +
            "    → string  : override path nếu có, default binding nếu không.\n" +
            "  TISRebindingSystem.GetDisplayName(path)\n" +
            "    → string  : tên thân thiện. VD: \"<Keyboard>/space\" → \"Space\".\n\n" +
            "GHI CHÚ\n" +
            "  – GetActivePath dùng để populate label trong Settings UI.\n" +
            "  – FindConflict nên gọi trước StartRebinding / ForceApply.\n" +
            "  – GetDisplayName hoạt động cả khi path là override lẫn default.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/QueryAPIs")]
public class TIS_Demo_QueryAPIs : MonoBehaviour
{
    [Header("Action cần test")]
    public string       actionName        = "Jump";
    public RebindTarget target            = RebindTarget.PC;
    [Tooltip("Path để test FindConflict không có conflict.")]
    public string       pathNoConflict    = "<Keyboard>/g";
    [Tooltip("Path để test FindConflict CÓ conflict — phải giống binding hiện tại của một action khác.")]
    public string       pathWithConflict  = "<Keyboard>/space"; // giả sử Jump đang dùng space

    [Header("UI References")]
    public Button btnCheckActivePath;
    public Button btnCheckConflictNo;
    public Button btnCheckConflictYes;
    public TextMeshProUGUI   lblActivePath;
    public TextMeshProUGUI   lblConflict;

    private TISInputBridge _bridge;

    // ── Lifecycle ─────────────────────────────────────────────

    private void Start()
    {
        btnCheckActivePath.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Check Active Path";
        btnCheckConflictNo.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Check Conflict No";
        btnCheckConflictYes.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Check Conflict Yes";
        
        _bridge = TISInputBridge.Instance;

        if (btnCheckActivePath  != null) btnCheckActivePath.onClick.AddListener(OnClickActivePath);
        if (btnCheckConflictNo  != null) btnCheckConflictNo.onClick.AddListener(() => OnClickConflict(pathNoConflict));
        if (btnCheckConflictYes != null) btnCheckConflictYes.onClick.AddListener(() => OnClickConflict(pathWithConflict));

        if (lblActivePath != null) lblActivePath.text = "Nhấn 'Lấy Active Path'.";
        if (lblConflict   != null) lblConflict.text   = "Nhấn 'Kiểm tra Conflict'.";
    }

    // ── Button callbacks ──────────────────────────────────────

    private void OnClickActivePath()
    {
        // ← API: GetActivePath
        string path = TISRebindingSystem.GetActivePath(_bridge, actionName, target);

        // ← API: GetDisplayName
        string display = TISRebindingSystem.GetDisplayName(path);

        string msg = $"Action : {actionName} [{target}]\n" +
                     $"Raw    : {(string.IsNullOrEmpty(path) ? "(trống)" : path)}\n" +
                     $"Display: {display}";

        if (lblActivePath != null) lblActivePath.text = msg;
        Debug.Log($"[Demo_QueryAPIs] GetActivePath → {path} | Display → {display}");
    }

    private void OnClickConflict(string testPath)
    {
        // ← API: FindConflict
        string conflict = TISRebindingSystem.FindConflict(
            bridge:     _bridge,
            skipAction: actionName,
            target:     target,
            path:       testPath
        );

        string display = TISRebindingSystem.GetDisplayName(testPath);   // ← API: GetDisplayName

        string msg = conflict != null
            ? $"[X] CONFLICT\n  Path  : {display} ({testPath})\n  Dùng bởi: \"{conflict}\""
            : $"[OK] Không conflict\n  Path  : {display} ({testPath})\n  Có thể dùng cho \"{actionName}\"";

        if (lblConflict != null) lblConflict.text = msg;
        Debug.Log($"[Demo_QueryAPIs] FindConflict({testPath}) → {conflict ?? "null (ok)"}");
    }
}
