// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_API.DirectionalRegistry.cs
//  Tab API Reference — module: TISDirectionalRegistry
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        private void Draw_API_DirectionalRegistry()
        {
            GUILayout.Space(8);
            H2("TISDirectionalRegistry — Static");
            ApiHeader();
            ApiRow("GetDirection(skillName)",           "Vector2", "Lấy direction sau cast (key=skillName+\"_Dir\"). zero nếu quick tap.", "green");
            ApiRow("SetVector2Override(name, v2)",      "void",    "Ghi đè action Vector2 real-time — bridge.GetVector2Action() đọc được.", "red");
            ApiRow("ClearVector2Override(name)",        "void",    "Xóa override action Vector2.", "red");
            ApiRow("GetVector2Override(name, out val)", "bool",    "Lấy override nếu có. true nếu tồn tại.", "green");

        }

    }  // partial class TISDocWindow
}
#endif
