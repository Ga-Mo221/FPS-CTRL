using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GaMo.TrueInputSystem;

/// <summary>
/// Demo: LoadAndApply
/// ──────────────────────────────────────────────────────────────────────────────────────
/// Minh hoạ đọc override từ JSON file và apply lên bridge khi khởi động.
/// Thường gọi một lần duy nhất trong Awake() sau khi TISInputBridge init xong.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_LoadAndApply))]
public class TIS_Demo_LoadAndApply_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH :\n" +
            "   Test đọc override từ JSON file và apply lên bridge khi khởi động — đây là cách dùng chuẩn trong project thực.\n \n" +
            "===================================\n \n"+
            "CÁCH TEST :\n" +
            "   1.Vào Play Mode → Awake() tự động gọi LoadAndApply(), Console hiện log xác nhận.\n"+
            "   2.Nhấn Show Path → LblResult hiện binding đang active. Nếu có JSON thì hiện override, nếu không có thì hiện default.\n"+
            "   3.Nhấn Load (nút reload thủ công) → LoadAndApply() chạy lại, hữu ích để verify giữa chừng mà không cần restart.\n"+
            "   4.Luồng test đầy đủ: Chạy ForceApply → ghi nhớ path mới → thoát Play → vào lại Play → kiểm tra LblResult xem override có được load lại không.\n"+
            "   5.Test case không có file: Xoá JSON file từ persistentDataPath, vào Play → LoadAndApply() không lỗi, hiện \"dùng default binding\".\n \n"+
            "===================================\n \n"+
            "SETUP\n" +
            "1. Gán script này lên bất kỳ GameObject nào trong scene.\n" +
            "2. Đảm bảo scene có TISInputBridge (singleton).\n" +
            "3. Chạy TIS_Demo_ForceApply hoặc TIS_Demo_StartRebinding TRƯỚC để có JSON.\n" +
            "4. Tạo Canvas → gán UI reference bên dưới.\n" +
            "   · BtnLoad      — Gọi LoadAndApply thủ công (để test lại giữa chừng).\n" +
            "   · BtnShowPath  — Hiện binding đang active sau khi load.\n" +
            "   · LblResult    — Text hiện kết quả.\n\n" +
            "API ĐƯỢC TEST\n" +
            "  TISRebindingSystem.LoadAndApply(bridge)\n" +
            "    – Đọc file TIS_Bindings_{profileName}.json từ persistentDataPath.\n" +
            "    – Nạp tất cả override vào bộ nhớ.\n" +
            "    – Rebuild bridge một lần sau khi load xong.\n" +
            "    – Nếu không có file → dùng default binding, không lỗi.\n\n" +
            "GHI CHÚ\n" +
            "  – Trong project thực, chỉ gọi LoadAndApply() một lần trong Awake().\n" +
            "  – Nút BtnLoad trong demo này cho phép reload nhiều lần để verify.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/LoadAndApply")]
public class TIS_Demo_LoadAndApply : MonoBehaviour
{
    [Header("Action cần verify sau khi load")]
    public string       actionName = "Jump";
    public RebindTarget target     = RebindTarget.PC;

    [Header("UI References")]
    public Button btnLoad;
    public Button btnShowPath;
    public TextMeshProUGUI   lblResult;

    private TISInputBridge _bridge;

    // ── Lifecycle ─────────────────────────────────────────────

    private void Awake()
    {
        // Gọi sớm nhất có thể — trước Start() của các script khác
        _bridge = TISInputBridge.Instance;

        // ← API: LoadAndApply — đây là cách dùng chuẩn trong project thực
        TISRebindingSystem.LoadAndApply(_bridge);
        Debug.Log("[Demo_LoadAndApply] LoadAndApply() gọi trong Awake().");
    }

    private void Start()
    {
        btnLoad.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Load";
        btnShowPath.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Show Path";
        
        if (btnLoad     != null) btnLoad.onClick.AddListener(OnClickLoad);
        if (btnShowPath != null) btnShowPath.onClick.AddListener(OnClickShowPath);

        SetResult("LoadAndApply() đã chạy trong Awake().\nNhấn 'Hiện Binding' để xác nhận.");
    }

    // ── Button callbacks ──────────────────────────────────────

    private void OnClickLoad()
    {
        // ← API: LoadAndApply — cho phép reload thủ công trong demo
        TISRebindingSystem.LoadAndApply(_bridge);

        SetResult("[OK] LoadAndApply() đã chạy lại.\nNhấn 'Hiện Binding' để xem kết quả.");
        Debug.Log("[Demo_LoadAndApply] LoadAndApply() reload thủ công.");
    }

    private void OnClickShowPath()
    {
        // Dùng GetActivePath + GetDisplayName để verify kết quả load
        string path    = TISRebindingSystem.GetActivePath(_bridge, actionName, target);
        string display = TISRebindingSystem.GetDisplayName(path);

        string msg = $"Sau LoadAndApply:\n" +
                     $"  {actionName} [{target}]\n" +
                     $"  Raw    : {(string.IsNullOrEmpty(path) ? "(default / chưa có override)" : path)}\n" +
                     $"  Display: {display}";

        SetResult(msg);
        Debug.Log($"[Demo_LoadAndApply] GetActivePath → {path}");
    }

    // ── Helpers ───────────────────────────────────────────────

    private void SetResult(string message)
    {
        if (lblResult != null) lblResult.text = message;
    }
}
