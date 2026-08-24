using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrueInputSystem;

/// <summary>
/// Demo: ApplyPreset
/// ──────────────────────────────────────────────────────────────────────────────────────
/// Minh hoạ apply nhiều binding cùng lúc từ một Dictionary (preset).
/// Bridge chỉ rebuild một lần duy nhất sau khi toàn bộ preset được xử lý.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_ApplyPreset))]
public class TIS_Demo_ApplyPreset_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "SETUP\n" +
            "1. Gán script này lên bất kỳ GameObject nào trong scene.\n" +
            "2. Đảm bảo scene có TISInputBridge (singleton).\n" +
            "3. Tạo Canvas → gán đủ UI reference bên dưới.\n" +
            "   · BtnPresetA  — Apply preset WASD chuẩn (Jump=Space, Attack=F).\n" +
            "   · BtnPresetB  — Apply preset thay thế (Jump=G, Attack=H).\n" +
            "   · BtnConflict — Apply preset có conflict cố ý (2 action cùng 1 phím).\n" +
            "   · LblResult   — Text hiện kết quả.\n\n" +
            "API ĐƯỢC TEST\n" +
            "  TISRebindingSystem.ApplyPreset(bridge, preset, target, onConflict?)\n" +
            "    → int : số binding được apply thành công.\n" +
            "    · onConflict null    : log warning, bỏ qua action conflict.\n" +
            "    · onConflict != null : gọi callback để xử lý tùy ý.\n\n" +
            "GHI CHÚ\n" +
            "  – Chỉ rebuild bridge 1 lần sau khi xử lý toàn bộ preset.\n" +
            "  – Path bị blacklist bị bỏ qua, không làm crash preset còn lại.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/ApplyPreset")]
public class TIS_Demo_ApplyPreset : MonoBehaviour
{
    [Header("UI References")]
    public Button            btnPresetA;
    public Button            btnPresetB;
    public Button            btnConflict;
    public TextMeshProUGUI   lblResult;

    private TISInputBridge _bridge;

    // ── Preset data ───────────────────────────────────────────

    // Preset A — layout chuẩn
    private static readonly Dictionary<string, string> PresetA = new Dictionary<string, string>
    {
        { "Jump",   "<Keyboard>/space" },
        { "Attack", "<Keyboard>/f"     },
        { "Run",  "<Keyboard>/leftShift" },
    };

    // Preset B — layout thay thế
    private static readonly Dictionary<string, string> PresetB = new Dictionary<string, string>
    {
        { "Jump",   "<Keyboard>/g" },
        { "Attack", "<Keyboard>/h" },
        { "Run",  "<Keyboard>/c" },
    };

    // Preset conflict — Jump và Attack cùng dùng phím /j
    private static readonly Dictionary<string, string> PresetConflict = new Dictionary<string, string>
    {
        { "Jump",   "<Keyboard>/j" },
        { "Attack", "<Keyboard>/j" },   // ← conflict với Jump
    };

    // ── Lifecycle ─────────────────────────────────────────────

    private void Start()
    {
        btnPresetA.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Preset A";
        btnPresetB.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Preset B";
        btnConflict.transform.GetComponentInChildren<TextMeshProUGUI>().text = "Conflict";
        
        _bridge = TISInputBridge.Instance;

        if (btnPresetA  != null) btnPresetA.onClick.AddListener(() => OnClickPreset(PresetA,      "Preset A (chuẩn)"));
        if (btnPresetB  != null) btnPresetB.onClick.AddListener(() => OnClickPreset(PresetB,      "Preset B (thay thế)"));
        if (btnConflict != null) btnConflict.onClick.AddListener(OnClickConflictPreset);

        SetResult("Nhấn một nút để ApplyPreset.");
    }

    // ── Button callbacks ──────────────────────────────────────

    private void OnClickPreset(Dictionary<string, string> preset, string label)
    {
        // ← API: ApplyPreset (không có onConflict → log warning tự động)
        int applied = TISRebindingSystem.ApplyPreset(
            bridge:  _bridge,
            preset:  preset,
            target:  RebindTarget.PC
        );

        string msg = $"[OK] {label}\n  Applied: {applied}/{preset.Count} binding.";
        SetResult(msg);
        Debug.Log($"[Demo_ApplyPreset] {msg}");
    }

    private void OnClickConflictPreset()
    {
        var log = new System.Text.StringBuilder();
        log.AppendLine("Preset Conflict:");

        // ← API: ApplyPreset với onConflict callback tuỳ chỉnh
        int applied = TISRebindingSystem.ApplyPreset(
            bridge:     _bridge,
            preset:     PresetConflict,
            target:     RebindTarget.PC,
            onConflict: (actionName, conflictWith, path) =>
            {
                string line = $"  [X] \"{actionName}\" conflict với \"{conflictWith}\" (path={path})";
                log.AppendLine(line);
                Debug.LogWarning($"[Demo_ApplyPreset] {line}");
            }
        );

        log.AppendLine($"Applied: {applied}/{PresetConflict.Count}");
        SetResult(log.ToString());
    }

    // ── Helpers ───────────────────────────────────────────────

    private void SetResult(string message)
    {
        if (lblResult != null) lblResult.text = message;
    }
}
