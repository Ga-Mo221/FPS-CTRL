#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem
{
    /// <summary>
    /// Vẽ icon của các component TIS ngay trên tên GameObject trong cửa sổ Hierarchy,
    /// tương tự cách Magica Cloth 2 làm (DrawIconInHierarchy).
    ///
    /// Icon hiển thị ở đây chính là icon đã được gán cho script (MonoScript) thông qua
    /// ScriptIconSetter / ComponentIconApplier, nên không cần tìm icon lại — chỉ cần lấy
    /// mini thumbnail hiện tại của component bằng AssetPreview.GetMiniThumbnail().
    /// </summary>
    [InitializeOnLoad]
    public static class TIS_DrawIconInHierarchy
    {
        private const int IconSize = 16;

        // Danh sách các loại component TIS muốn hiển thị icon trong Hierarchy.
        // Lưu ý: chỉ liệt kê type kế thừa từ Component (MonoBehaviour). Các ScriptableObject
        // như TISInputProfileConfig không bao giờ nằm trên GameObject nên KHÔNG được đưa
        // vào đây — GameObject.GetComponent() sẽ ném ArgumentException nếu type không phải
        // Component/interface.
        private static readonly System.Type[] _trackedTypes =
        {
            typeof(TISDirectionalSkillButton),
            typeof(TISGamepadUIButton),
            typeof(TISGamepadUIButtonGroup),
            typeof(TISGamepadVirtualCursor),
            typeof(TISInputBridge),
            typeof(TISMobileInputSetup),
            typeof(TISMobileSkillButton),
            typeof(TISUINavigationContext),
            typeof(TISTargetScanner),
        };

        // Bản đã lọc an toàn: chỉ giữ lại type thực sự kế thừa Component, tránh
        // ArgumentException nếu sau này ai đó lỡ thêm nhầm một ScriptableObject vào _trackedTypes.
        private static readonly System.Type[] _safeTrackedTypes;

        static TIS_DrawIconInHierarchy()
        {
            var list = new System.Collections.Generic.List<System.Type>();
            foreach (var t in _trackedTypes)
            {
                if (typeof(Component).IsAssignableFrom(t))
                    list.Add(t);
            }
            _safeTrackedTypes = list.ToArray();

            EditorApplication.hierarchyWindowItemOnGUI -= DrawIcon;
            EditorApplication.hierarchyWindowItemOnGUI += DrawIcon;
        }

        private static void DrawIcon(int instanceId, Rect selectionRect)
        {
#pragma warning disable CS0618 // Unity 6.3: InstanceIDToObject obsolete — hierarchyWindowItemOnGUI vẫn trả int
            GameObject obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
#pragma warning restore CS0618
            if (obj == null)
                return;

            Rect rect = selectionRect;
            rect.width = IconSize;
            rect.x += EditorStyles.label.CalcSize(obj.name).x;
            rect.x += IconSize + 4;
            rect.y -= 1;

            foreach (var type in _safeTrackedTypes)
            {
                Component component = obj.GetComponent(type);
                if (component == null)
                    continue;

                Texture icon = AssetPreview.GetMiniThumbnail(component);
                if (icon == null)
                    continue;

                GUI.Label(rect, icon);
                rect.x += IconSize;
            }
        }
    }

    /// <summary>
    /// Tiện ích tính kích thước text (dùng để canh vị trí icon sau tên GameObject).
    /// </summary>
    internal static class TIS_GUIStyleExtensions
    {
        public static Vector2 CalcSize(this GUIStyle self, string text)
        {
            var content = new GUIContent(text);
            return self.CalcSize(content);
        }
    }
}
#endif