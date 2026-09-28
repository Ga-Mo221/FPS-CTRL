// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_API.MobileInputSetup.cs
//  Tab API Reference — module: TISMobileInputSetup
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        private void Draw_API_MobileInputSetup()
        {
            GUILayout.Space(8);
            // ── TISMobileInputSetup ────────────────────────────────────────
            H2("TISMobileInputSetup — Properties, Methods & Events");
            ApiHeader();
            ApiRow("IsReady",              "bool",  "true sau khi toàn bộ mobile UI khởi tạo xong.", "green");
            ApiRow("BatTatXoayCamera()",   "void",  "Toggle camera rotation (batXoayCamera = !batXoayCamera).", "red");
            ApiRow("BatTatZoom()",         "void",  "Toggle pinch-to-zoom.", "red");
            ApiRow("BatTatJoystick()",     "void",  "Toggle joystick hiển thị.", "red");
            ApiRow("BatTatCameraZone()",   "void",  "Toggle camera zone.", "red");
            ApiRow("DatXoayCamera(bool)",  "void",  "Gán trực tiếp, áp dụng ngay.", "red");
            ApiRow("DatZoom(bool)",        "void",  "Gán batZoom.", "red");
            ApiRow("DatJoystick(bool)",    "void",  "Gán batJoystick.", "red");
            ApiRow("DatCameraZone(bool)",  "void",  "Gán batCamera.", "red");
            ApiRow("DatTyLeVung(float)",   "void",  "Gán tỉ lệ joystick/camera (clamp 0.25–0.75). Sync ngay.", "red");
            ApiRow("DongBoTyLe()",         "void",  "Sync tỉ lệ zone lên joystick, camera zone, gesture detector.", "red");
            ApiRow("ApDungCaiDatCamera()", "void",  "Áp dụng tất cả cài đặt camera zone từ Inspector.", "red");
            ApiHeader("Event", "Signature", "Khi nào fire");
            ApiRow("SetupCompleted", "event Action<TISMobileInputSetup>", "Fire sau khi toàn bộ mobile UI khởi tạo thành công. Param = instance.", "purple");

        }

    }  // partial class TISDocWindow
}
#endif
