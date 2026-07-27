// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_API.TargetScanner.cs
//  Tab API Reference — module: TISTargetScanner
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        private void Draw_API_TargetScanner()
        {
            GUILayout.Space(8);
            // ── TISTargetScanner ───────────────────────────────────────────
            H2("TISTargetScanner — Properties & Methods");
            Note("Gắn lên Camera GameObject. Tự động scan target gần tâm màn hình nhất theo interval. Kết hợp với AimAssistProcess() để có aim assist tự động.");
            ApiHeader();
            ApiRow("CurrentTarget",    "Transform", "Target đang được lock. null nếu không có target trong phạm vi.", "green");
            ApiRow("CurrentAimPoint",  "Vector3",   "Vị trí world-space điểm ngắm trên CurrentTarget (theo AimPointMode).", "blue");
            ApiRow("CurrentAngle",     "float",     "Góc lệch (độ) giữa tâm màn hình và target. -1 nếu không có target.", "amber");
            ApiRow("HasTarget",        "bool",      "Shortcut: CurrentTarget != null.", "green");
            ApiRow("ForceScan()",      "void",      "Buộc quét lại ngay lập tức. Dùng sau khi spawn enemy.", "red");
            ApiRow("ClearTarget()",    "void",      "Xóa CurrentTarget về null (target chết, bị destroy).", "red");

        }

    }  // partial class TISDocWindow
}
#endif
