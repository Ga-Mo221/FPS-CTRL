using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrueInputSystem;

/// <summary>
/// Demo: StartRebinding / CancelRebinding / State / OnRebindStarted / OnRebindCompleted
/// ──────────────────────────────────────────────────────────────────────────────────────
/// Minh hoạ luồng rebinding tương tác đầy đủ:
///   nhấn nút → lắng nghe phím → lưu kết quả → hiện lên UI.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_StartRebinding))]
public class TIS_Demo_StartRebinding_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "SETUP\n" +
            "1. Gán script này lên bất kỳ GameObject nào trong scene.\n" +
            "2. Đảm bảo scene có TISInputBridge (singleton).\n" +
            "3. Tạo Canvas → gán đủ 4 UI reference bên dưới.\n" +
            "   · BtnRebind   — Button để bắt đầu lắng nghe phím.\n" +
            "   · BtnCancel   — Button để huỷ giữa chừng.\n" +
            "   · LblStatus   — Text hiện trạng thái / kết quả.\n" +
            "   · LblState    — Text hiện TISRebindingSystem.State mỗi frame.\n\n" +
            "API ĐƯỢC TEST\n" +
            "  TISRebindingSystem.StartRebinding()   — bắt đầu lắng nghe phím\n" +
            "  TISRebindingSystem.CancelRebinding()  — huỷ phiên đang chạy\n" +
            "  TISRebindingSystem.State              — trạng thái hiện tại\n" +
            "  TISRebindingSystem.OnRebindStarted    — event khi bắt đầu\n" +
            "  TISRebindingSystem.OnRebindCompleted  — event khi kết thúc\n\n" +
            "GHI CHÚ\n" +
            "  – Action mặc định được test là \"Jump\" trên PC.\n" +
            "  – Đổi _actionName / _target bên dưới để test action khác.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/StartRebinding")]
public class TIS_Demo_StartRebinding : MonoBehaviour
{
    [Header("Action cần test")]
    public string       actionName = "Jump";
    public RebindTarget target     = RebindTarget.PC;

    [Header("UI References")]
    public Button btnRebind;
    public Button btnCancel;
    public TextMeshProUGUI   lblStatus;
    public TextMeshProUGUI   lblState;

    private TISInputBridge _bridge;

    // ── Lifecycle ────────────────────────────────────────────

    private void Awake()
    {
        // Subscribe events trước khi scene load xong
        TISRebindingSystem.OnRebindStarted   += HandleStarted;
        TISRebindingSystem.OnRebindCompleted += HandleCompleted;
    }

    private void Start()
    {
        btnCancel.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Cancel";
        btnRebind.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Rebind";
        
        _bridge = TISInputBridge.Instance;

        // Gán listener cho UI Button
        if (btnRebind != null) btnRebind.onClick.AddListener(OnClickRebind);
        if (btnCancel != null) btnCancel.onClick.AddListener(OnClickCancel);

        RefreshUI("Sẵn sàng. Nhấn 'Bắt đầu Rebind'.");
    }

    private void OnDestroy()
    {
        // Luôn unsubscribe khi object bị huỷ
        TISRebindingSystem.OnRebindStarted   -= HandleStarted;
        TISRebindingSystem.OnRebindCompleted -= HandleCompleted;
    }

    // ── Update — hiện State mỗi frame ────────────────────────

    private void Update()
    {
        if (lblState != null)
            lblState.text = $"State: {TISRebindingSystem.State}";   // ← API: State
    }

    // ── Button callbacks ──────────────────────────────────────

    private void OnClickRebind()
    {
        // ← API: StartRebinding
        TISRebindingSystem.StartRebinding(
            bridge:     _bridge,
            actionName: actionName,
            target:     target,
            onComplete: result =>
            {
                // callback inline — cũng kích hoạt OnRebindCompleted event
                string msg = result.Success
                    ? $"[OK] Rebind thành công!\n  {actionName} [{target}]\n  {result.OldPath} → {result.NewPath}"
                    : $"[X] Thất bại: {result.FailReason}";
                Debug.Log($"[Demo_StartRebinding] {msg}");
            },
            cancelPath: "<Keyboard>/escape"
        );
    }

    private void OnClickCancel()
    {
        // ← API: CancelRebinding
        TISRebindingSystem.CancelRebinding();
        Debug.Log("[Demo_StartRebinding] Đã gọi CancelRebinding().");
    }

    // ── Event handlers ────────────────────────────────────────

    private void HandleStarted(string action, RebindTarget tgt)
    {
        // ← Event: OnRebindStarted
        RefreshUI($"[...] Đang lắng nghe...\n  Action: {action}  [{tgt}]\n  Nhấn phím bất kỳ (Esc = huỷ)");
        if (btnRebind != null) btnRebind.interactable = false;
        if (btnCancel != null) btnCancel.interactable = true;
        Debug.Log($"[Demo_StartRebinding] OnRebindStarted → {action} [{tgt}]");
    }

    private void HandleCompleted(RebindResult result)
    {
        // ← Event: OnRebindCompleted
        string statusText = result.Success
            ? $"[OK] {result.ActionName} [{result.Target}]\n  Phím mới: {result.NewPath}"
            : $"[X] {result.FailReason}";
        RefreshUI(statusText);

        if (btnRebind != null) btnRebind.interactable = true;
        if (btnCancel != null) btnCancel.interactable = false;
        Debug.Log($"[Demo_StartRebinding] OnRebindCompleted → Success={result.Success}, NewPath={result.NewPath}");
    }

    // ── Helpers ───────────────────────────────────────────────

    private void RefreshUI(string message)
    {
        if (lblStatus != null) lblStatus.text = message;
    }
}
