using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrueInputSystem;

/// <summary>
/// Demo: ResetBinding / ResetAllBindings
/// ──────────────────────────────────────────────────────────────────────────────────────
/// Minh hoạ reset binding về default, bao gồm cả xoá JSON file.
/// Test cả hai case: reset một action đơn lẻ và reset toàn bộ profile.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_ResetBindings))]
public class TIS_Demo_ResetBindings_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "SETUP\n" +
            "1. Gán script này lên bất kỳ GameObject nào trong scene.\n" +
            "2. Đảm bảo scene có TISInputBridge (singleton).\n" +
            "3. Chạy TIS_Demo_ForceApply TRƯỚC để có override cần reset.\n" +
            "4. Tạo Canvas → gán đủ UI reference bên dưới.\n" +
            "   · BtnSetup      — ForceApply nhanh một override để có data reset.\n" +
            "   · BtnResetOne   — Reset action đơn lẻ (actionName bên dưới).\n" +
            "   · BtnResetAll   — Reset toàn bộ override của profile.\n" +
            "   · LblBefore     — Text hiện binding TRƯỚC khi reset.\n" +
            "   · LblAfter      — Text hiện binding SAU khi reset.\n\n" +
            "API ĐƯỢC TEST\n" +
            "  TISRebindingSystem.ResetBinding(bridge, actionName, target)\n" +
            "    – Xoá override khỏi _overrides dict.\n" +
            "    – Xoá entry khỏi JSON file (FIX: kể cả sau domain reload).\n" +
            "    – Rebuild bridge tự động.\n\n" +
            "  TISRebindingSystem.ResetAllBindings(bridge)\n" +
            "    – Xoá toàn bộ override của profile hiện tại.\n" +
            "    – Xoá toàn bộ JSON file của profile.\n" +
            "    – Rebuild bridge một lần duy nhất.\n\n" +
            "GHI CHÚ\n" +
            "  – ResetBinding kiểm tra cả JSON file, không chỉ in-memory dict.\n" +
            "  – Nếu không có gì để reset → không rebuild, tránh overhead.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/ResetBindings")]
public class TIS_Demo_ResetBindings : MonoBehaviour
{
    [Header("Action cần test")]
    public string       actionName     = "Jump";
    public RebindTarget target         = RebindTarget.PC;
    [Tooltip("Path sẽ được ForceApply khi nhấn nút Setup (để có data cần reset).")]
    public string       setupOverride  = "<Keyboard>/g";

    [Header("UI References")]
    public Button btnSetup;
    public Button btnResetOne;
    public Button btnResetAll;
    public TextMeshProUGUI   lblBefore;
    public TextMeshProUGUI   lblAfter;

    private TISInputBridge _bridge;

    // ── Lifecycle ─────────────────────────────────────────────

    private void Start()
    {
        btnResetOne.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Reset One";
        btnSetup.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Setup Override";
        btnResetAll.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Reset All";
        
        _bridge = TISInputBridge.Instance;

        if (btnSetup    != null) btnSetup.onClick.AddListener(OnClickSetup);
        if (btnResetOne != null) btnResetOne.onClick.AddListener(OnClickResetOne);
        if (btnResetAll != null) btnResetAll.onClick.AddListener(OnClickResetAll);

        RefreshBoth("Nhấn 'Setup Override' trước, rồi mới Reset.");
    }

    // ── Button callbacks ──────────────────────────────────────

    private void OnClickSetup()
    {
        // ForceApply để có override cần reset
        bool ok = TISRebindingSystem.ForceApply(_bridge, actionName, target, setupOverride);

        string path    = TISRebindingSystem.GetActivePath(_bridge, actionName, target);
        string display = TISRebindingSystem.GetDisplayName(path);
        string msg     = ok
            ? $"[OK] Override đặt:\n  {actionName} [{target}] = {display}"
            : $"[X] ForceApply thất bại.";

        if (lblBefore != null) lblBefore.text = msg;
        if (lblAfter  != null) lblAfter.text  = "Chưa reset.";
        Debug.Log($"[Demo_ResetBindings] Setup: {msg}");
    }

    private void OnClickResetOne()
    {
        // Snapshot trước
        string pathBefore = TISRebindingSystem.GetActivePath(_bridge, actionName, target);
        if (lblBefore != null)
            lblBefore.text = $"Trước:\n  {actionName} [{target}]\n  {TISRebindingSystem.GetDisplayName(pathBefore)}";

        // ← API: ResetBinding
        TISRebindingSystem.ResetBinding(_bridge, actionName, target);

        // Snapshot sau
        string pathAfter = TISRebindingSystem.GetActivePath(_bridge, actionName, target);
        if (lblAfter != null)
            lblAfter.text = $"Sau ResetBinding:\n  {actionName} [{target}]\n  {TISRebindingSystem.GetDisplayName(pathAfter)} (default)";

        Debug.Log($"[Demo_ResetBindings] ResetBinding → {pathBefore} → {pathAfter}");
    }

    private void OnClickResetAll()
    {
        // Snapshot trước
        string pathBefore = TISRebindingSystem.GetActivePath(_bridge, actionName, target);
        if (lblBefore != null)
            lblBefore.text = $"Trước ResetAll:\n  {actionName} [{target}]\n  {TISRebindingSystem.GetDisplayName(pathBefore)}";

        // ← API: ResetAllBindings
        TISRebindingSystem.ResetAllBindings(_bridge);

        // Snapshot sau
        string pathAfter = TISRebindingSystem.GetActivePath(_bridge, actionName, target);
        if (lblAfter != null)
            lblAfter.text = $"Sau ResetAllBindings:\n  {actionName} [{target}]\n  {TISRebindingSystem.GetDisplayName(pathAfter)} (default)\n" +
                            $"  (Toàn bộ override của profile đã bị xoá)";

        Debug.Log($"[Demo_ResetBindings] ResetAllBindings → {actionName} [{target}] = {pathAfter}");
    }

    // ── Helpers ───────────────────────────────────────────────

    private void RefreshBoth(string message)
    {
        if (lblBefore != null) lblBefore.text = message;
        if (lblAfter  != null) lblAfter.text  = message;
    }
}
