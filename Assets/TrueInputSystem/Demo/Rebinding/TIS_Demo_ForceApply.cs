using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrueInputSystem;

/// <summary>
/// Demo: ForceApply
/// ──────────────────────────────────────────────────────────────────────────────────────
/// Minh hoạ apply binding trực tiếp bằng code — không cần người dùng nhấn phím.
/// Hữu dụng cho: load preset từ file, dropdown chọn phím sẵn, hotfix binding bằng code.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_ForceApply))]
public class TIS_Demo_ForceApply_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH :\n" +
            "   Test apply binding bằng code, không cần người dùng nhấn phím — dùng cho preset, dropdown chọn phím.\n \n" +
            "===================================\n \n"+
            "CÁCH TEST :\n" +
            "   1.Nhấn Apply G → binding \"Jump\" đổi sang <Keyboard>/g, LblResult hiện [OK].\n"+
            "   2.Nhấn Apply H → binding đổi sang <Keyboard>/h.\n"+
            "   3.Nhấn Apply Blacklisted → thử apply <Keyboard>/escape (bị blacklist) → LblResult hiện [X] ForceApply THẤT BẠI.\n"+
            "   4.Sau mỗi lần Apply thành công, vào Application.persistentDataPath để xác nhận file JSON được ghi.\n \n"+
            "===================================\n \n"+
            "SETUP\n" +
            "1. Gán script này lên bất kỳ GameObject nào trong scene.\n" +
            "2. Đảm bảo scene có TISInputBridge (singleton).\n" +
            "3. Tạo Canvas → gán đủ các UI reference bên dưới.\n" +
            "   · BtnApplyG   — Button để apply <Keyboard>/g.\n" +
            "   · BtnApplyH   — Button để apply <Keyboard>/h.\n" +
            "   · BtnApplyBlacklisted — Button test path bị blacklist (Escape).\n" +
            "   · LblResult   — Text hiện kết quả ForceApply.\n\n" +
            "API ĐƯỢC TEST\n" +
            "  TISRebindingSystem.ForceApply(bridge, actionName, target, newPath)\n" +
            "    → true  : apply thành công, JSON được lưu, bridge rebuild.\n" +
            "    → false : bridge null / path trống / path bị blacklist.\n\n" +
            "GHI CHÚ\n" +
            "  – Mỗi lần ForceApply thành công, JSON được ghi tự động.\n" +
            "  – Path blacklist mặc định: <Keyboard>/escape, <Keyboard>/printScreen.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/ForceApply")]
public class TIS_Demo_ForceApply : MonoBehaviour
{
    [Header("Action cần test")]
    public string       actionName = "Jump";
    public RebindTarget target     = RebindTarget.PC;

    [Header("UI References")]
    public Button btnApplyG;
    public Button btnApplyH;
    public Button btnApplyBlacklisted;
    public TextMeshProUGUI   lblResult;

    private TISInputBridge _bridge;

    // ── Lifecycle ─────────────────────────────────────────────

    private void Start()
    {
        btnApplyG.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Apply G";
        btnApplyH.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Apply H";
        btnApplyBlacklisted.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Apply Blacklisted";
        
        _bridge = TISInputBridge.Instance;

        if (btnApplyG          != null) btnApplyG.onClick.AddListener(() => OnClickForceApply("<Keyboard>/g"));
        if (btnApplyH          != null) btnApplyH.onClick.AddListener(() => OnClickForceApply("<Keyboard>/h"));
        if (btnApplyBlacklisted != null) btnApplyBlacklisted.onClick.AddListener(() => OnClickForceApply("<Keyboard>/escape"));

        SetResult("Nhấn một nút để ForceApply binding.");
    }

    // ── Button callback ───────────────────────────────────────

    private void OnClickForceApply(string newPath)
    {
        // ← API: ForceApply
        bool success = TISRebindingSystem.ForceApply(
            bridge:     _bridge,
            actionName: actionName,
            target:     target,
            newPath:    newPath
        );

        string msg = success
            ? $"[OK] ForceApply OK\n  {actionName} [{target}] = {newPath}"
            : $"[X] ForceApply THẤT BẠI\n  Path: {newPath}\n  (null bridge / path trống / blacklist?)";

        SetResult(msg);
        Debug.Log($"[Demo_ForceApply] {msg}");
    }

    // ── Helpers ───────────────────────────────────────────────

    private void SetResult(string message)
    {
        if (lblResult != null) lblResult.text = message;
    }
}
