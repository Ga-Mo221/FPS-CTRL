// CreateObjectPieMenu.cs
// Đặt file này vào: Assets/Editor/CreateObjectPieMenu.cs
//
// ── Pie menu tạo object 3D nhanh ─────────────────────────────────────────────
//
//  Mặc định phím tắt: Shift + Alt + A  (có thể đổi tại Edit → Preferences → Blender Editor)
//
//  [HOLD MODE]   Nhấn GIỮ phím tắt → menu hiện
//                Lia chuột về phía object muốn tạo → slot đó highlight
//                THẢ phím → tạo object đang highlight & đóng menu
//                Thả mà không lia chuột (dead-zone) = không làm gì
//
//  [TOGGLE MODE] Nhấn rồi THẢ NHANH (< HoldThreshold giây)
//                → menu mở cố định, click để chọn
//                Nhấn lại phím tắt, Escape, hoặc chuột phải → đóng
//
//  Phím 1–8 chọn nhanh khi menu đang mở.
//
//  Parent logic:
//    • Không có gì được chọn trong Hierarchy → tạo object ở root (không có cha)
//    • Đang chọn một GameObject → tạo object là con của object đó
//
//  Cấu hình phím tắt tại: Edit → Preferences → Blender Editor → Create Object Pie Menu
// ─────────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
using GaMo.BlenderEditorTools;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CreateObjectPieMenu
{
    // ════════════════════════════════════════════════════════════════════════
    //  Hằng số animation
    // ════════════════════════════════════════════════════════════════════════
    private const float ANIM_SPEED = 10f;

    // ── Shorthand đọc Settings ───────────────────────────────────────────────
    private static float  MenuRadius   => BlenderEditorPreferences.PieMenuRadius;
    private static float  ButtonSize   => BlenderEditorPreferences.PieMenuButtonSize;
    private static float  HoldThresh   => BlenderEditorPreferences.PieMenuHoldThreshold;
    private static float  DeadZone     => BlenderEditorPreferences.PieMenuDeadZone;
    private static float  BackdropR    => BlenderEditorPreferences.PieMenuBackdropRadius;
    private static float  CenterR      => BlenderEditorPreferences.PieMenuCenterRadius;
    private static int    CenterFont   => BlenderEditorPreferences.PieMenuCenterFontSize;
    private static int    BtnLabelFont => BlenderEditorPreferences.PieMenuBtnLabelFontSize;
    private static int    BtnSubFont   => BlenderEditorPreferences.PieMenuBtnSubFontSize;
    private static int    TooltipFont  => BlenderEditorPreferences.PieMenuTooltipFontSize;
    private static int    HintFont     => BlenderEditorPreferences.PieMenuHintFontSize;
    private static float  HoverScale   => BlenderEditorPreferences.PieMenuHoverScale;
    private static float  BeamArrow    => BlenderEditorPreferences.PieMenuBeamArrowSize;

    // ════════════════════════════════════════════════════════════════════════
    //  EditorPrefs Keys cho phím tắt riêng của menu này
    // ════════════════════════════════════════════════════════════════════════
    private const string K_COPM_ENABLED   = "COPM_Enabled";
    private const string K_COPM_KEY_CODE  = "COPM_KeyCode";
    private const string K_COPM_SHIFT     = "COPM_UseShift";
    private const string K_COPM_ALT       = "COPM_UseAlt";
    private const string K_COPM_CTRL      = "COPM_UseCtrl";

    // Defaults: Shift + Alt + A
    public static bool    Enabled   { get => EditorPrefs.GetBool  (K_COPM_ENABLED,  true);                  set => EditorPrefs.SetBool  (K_COPM_ENABLED,  value); }
    public static KeyCode HotKey    { get => (KeyCode)EditorPrefs.GetInt(K_COPM_KEY_CODE, (int)KeyCode.A);  set => EditorPrefs.SetInt   (K_COPM_KEY_CODE, (int)value); }
    public static bool    UseShift  { get => EditorPrefs.GetBool  (K_COPM_SHIFT,    true);                  set => EditorPrefs.SetBool  (K_COPM_SHIFT,    value); }
    public static bool    UseAlt    { get => EditorPrefs.GetBool  (K_COPM_ALT,      true);                  set => EditorPrefs.SetBool  (K_COPM_ALT,      value); }
    public static bool    UseCtrl   { get => EditorPrefs.GetBool  (K_COPM_CTRL,     false);                 set => EditorPrefs.SetBool  (K_COPM_CTRL,     value); }

    public static bool MatchesHotKey(Event e)
    {
        if (e == null) return false;
        bool modOk = (e.shift   == UseShift) &&
                     (e.alt     == UseAlt)   &&
                     (e.control == UseCtrl || e.command == UseCtrl);
        return modOk && e.keyCode == HotKey;
    }

    public static string HotKeyDisplayString()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (UseCtrl)  parts.Add("Ctrl");
        if (UseShift) parts.Add("Shift");
        if (UseAlt)   parts.Add("Alt");
        parts.Add(HotKey.ToString());
        return string.Join(" + ", parts);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Định nghĩa 8 slot object
    // ════════════════════════════════════════════════════════════════════════
    private struct ObjectEntry
    {
        public string Label;
        public string SubLabel;
        public Color  IconColor;
        public float  AngleDeg;
    }

    // 8 slot đặt đều theo vòng tròn (mỗi slot cách nhau 45°)
    // Góc 0° = phải, tăng thuận chiều kim đồng hồ trong tọa độ GUI (y xuống)
    private static readonly ObjectEntry[] s_Objects = new ObjectEntry[]
    {
        // 0 – Phải (0°): Cube
        new ObjectEntry { Label="Cube",      SubLabel="3D",         IconColor=new Color(0.91f,0.60f,0.26f), AngleDeg=0f    },
        // 1 – Dưới phải (45°): Sphere
        new ObjectEntry { Label="Sphere",    SubLabel="3D",         IconColor=new Color(0.30f,0.69f,0.91f), AngleDeg=45f   },
        // 2 – Dưới (90°): Capsule
        new ObjectEntry { Label="Capsule",   SubLabel="3D",         IconColor=new Color(0.55f,0.85f,0.45f), AngleDeg=90f   },
        // 3 – Dưới trái (135°): Cylinder
        new ObjectEntry { Label="Cylinder",  SubLabel="3D",         IconColor=new Color(0.75f,0.45f,0.85f), AngleDeg=135f  },
        // 4 – Trái (180°): Plane
        new ObjectEntry { Label="Plane",     SubLabel="3D",         IconColor=new Color(0.35f,0.75f,0.65f), AngleDeg=180f  },
        // 5 – Trên trái (-135° = 225°): Quad
        new ObjectEntry { Label="Quad",      SubLabel="3D",         IconColor=new Color(0.85f,0.35f,0.55f), AngleDeg=-135f },
        // 6 – Trên (-90°): Text (TextMeshPro 3D)
        new ObjectEntry { Label="Text",      SubLabel="TMP 3D",     IconColor=new Color(0.95f,0.85f,0.25f), AngleDeg=-90f  },
        // 7 – Trên phải (-45°): Empty
        new ObjectEntry { Label="Empty",     SubLabel="Object",     IconColor=new Color(0.72f,0.72f,0.72f), AngleDeg=-45f  },
    };

    // ════════════════════════════════════════════════════════════════════════
    //  Trạng thái nội bộ
    // ════════════════════════════════════════════════════════════════════════
    private enum MenuMode { Closed, Hold, Toggle }

    private static MenuMode s_Mode        = MenuMode.Closed;
    private static bool     s_KeyHeld     = false;
    private static double   s_KeyDownTime = 0.0;

    private static float    s_AnimT       = 0f;
    private static Vector2  s_Center;
    private static int      s_HoveredIdx  = -1;
    private static Vector2  s_MouseDelta;
    private static float    s_DeltaMag;

    // ════════════════════════════════════════════════════════════════════════
    //  Bootstrap
    // ════════════════════════════════════════════════════════════════════════
    static CreateObjectPieMenu()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Entry point
    // ════════════════════════════════════════════════════════════════════════
    private static void OnSceneGUI(SceneView sv)
    {
        if (!Enabled) return;

        Event e = Event.current;
        HandleKeyboard(e, sv);

        bool menuVisible = s_Mode != MenuMode.Closed;

        if (!menuVisible && s_AnimT < 0.001f) return;

        float animTarget = menuVisible ? 1f : 0f;
        s_AnimT = Mathf.Lerp(s_AnimT, animTarget, 1f - Mathf.Exp(-ANIM_SPEED * 0.016f));
        if (!menuVisible && s_AnimT < 0.002f) s_AnimT = 0f;

        if (e != null) UpdateDirection(e);

        DrawMenu(sv);

        if (s_Mode == MenuMode.Toggle && e != null)
            HandleToggleClick(e, sv);

        if (s_AnimT > 0.001f || menuVisible) sv.Repaint();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Keyboard
    // ════════════════════════════════════════════════════════════════════════
    private static void HandleKeyboard(Event e, SceneView sv)
    {
        if (e == null) return;

        bool isHotKey = MatchesHotKey(e);

        // ── KeyDown ──────────────────────────────────────────────────────────
        if (e.type == EventType.KeyDown && isHotKey && !s_KeyHeld)
        {
            e.Use();
            s_KeyHeld     = true;
            s_KeyDownTime = EditorApplication.timeSinceStartup;

            if (s_Mode == MenuMode.Toggle) { ForceClose(); return; }

            OpenMenu(e, sv);
            s_Mode = MenuMode.Hold;
            return;
        }

        // ── KeyUp ─────────────────────────────────────────────────────────────
        if (e.type == EventType.KeyUp && e.keyCode == HotKey && s_KeyHeld)
        {
            e.Use();
            s_KeyHeld = false;

            if (s_Mode == MenuMode.Hold)
            {
                double held = EditorApplication.timeSinceStartup - s_KeyDownTime;
                if (held < HoldThresh)
                    s_Mode = MenuMode.Toggle;   // nhấn nhanh → Toggle Mode
                else
                {
                    if (s_HoveredIdx >= 0)
                        SpawnObject(s_HoveredIdx);
                    ForceClose();
                }
            }
            return;
        }

        // ── Escape ────────────────────────────────────────────────────────────
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && s_Mode != MenuMode.Closed)
        {
            e.Use();
            ForceClose();
            return;
        }

        // ── Phím số 1–8 ────────────────────────────────────────────────────
        if (s_Mode != MenuMode.Closed && e.type == EventType.KeyDown)
        {
            int idx = e.keyCode switch
            {
                KeyCode.Alpha1 => 0, KeyCode.Alpha2 => 1,
                KeyCode.Alpha3 => 2, KeyCode.Alpha4 => 3,
                KeyCode.Alpha5 => 4, KeyCode.Alpha6 => 5,
                KeyCode.Alpha7 => 6, KeyCode.Alpha8 => 7,
                _ => -1
            };
            if (idx >= 0) { e.Use(); SpawnObject(idx); ForceClose(); }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Mở / đóng
    // ════════════════════════════════════════════════════════════════════════
    private static void OpenMenu(Event e, SceneView sv)
    {
        s_Center = (e != null)
            ? e.mousePosition
            : new Vector2(sv.position.width * 0.5f, sv.position.height * 0.5f);
        s_HoveredIdx = -1;
        s_MouseDelta = Vector2.zero;
        s_DeltaMag   = 0f;
        sv.Repaint();
    }

    private static void ForceClose()
    {
        s_Mode       = MenuMode.Closed;
        s_KeyHeld    = false;
        s_HoveredIdx = -1;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Direction
    // ════════════════════════════════════════════════════════════════════════
    private static void UpdateDirection(Event e)
    {
        if (s_Mode == MenuMode.Closed) return;
        s_MouseDelta = e.mousePosition - s_Center;
        s_DeltaMag   = s_MouseDelta.magnitude;
        s_HoveredIdx = GetIndexByAngle(s_MouseDelta, s_DeltaMag);
    }

    private static int GetIndexByAngle(Vector2 delta, float mag)
    {
        if (mag < DeadZone) return -1;
        float angleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        int   best     = -1;
        float bestDiff = float.MaxValue;
        for (int i = 0; i < s_Objects.Length; i++)
        {
            float diff = Mathf.Abs(NormalizeAngle(s_Objects[i].AngleDeg - angleDeg));
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }
        return best;
    }

    private static float NormalizeAngle(float a) => ((a + 540f) % 360f) - 180f;

    // ════════════════════════════════════════════════════════════════════════
    //  Toggle click
    // ════════════════════════════════════════════════════════════════════════
    private static void HandleToggleClick(Event e, SceneView sv)
    {
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (s_HoveredIdx >= 0)
            { e.Use(); SpawnObject(s_HoveredIdx); ForceClose(); }
            else if (s_DeltaMag > BackdropR)
            { e.Use(); ForceClose(); }
        }
        if (e.type == EventType.MouseDown && e.button == 1)
        { e.Use(); ForceClose(); }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Spawn object
    // ════════════════════════════════════════════════════════════════════════
    private static void SpawnObject(int index)
    {
        // Xác định parent: nếu đang chọn GameObject trong Hierarchy thì làm cha
        Transform parent = Selection.activeTransform;

        GameObject go = null;

        switch (index)
        {
            case 0: go = CreatePrimitive(PrimitiveType.Cube,     "Cube",     parent); break;
            case 1: go = CreatePrimitive(PrimitiveType.Sphere,   "Sphere",   parent); break;
            case 2: go = CreatePrimitive(PrimitiveType.Capsule,  "Capsule",  parent); break;
            case 3: go = CreatePrimitive(PrimitiveType.Cylinder, "Cylinder", parent); break;
            case 4: go = CreatePrimitive(PrimitiveType.Plane,    "Plane",    parent); break;
            case 5: go = CreateQuad(parent);                                          break;
            case 6: go = CreateTextMeshPro3D(parent);                                 break;
            case 7: go = CreateEmpty(parent);                                         break;
        }

        if (go == null) return;

        // Reset local transform (vị trí tại gốc của parent, hoặc Vector3.zero nếu root)
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one;

        // Đăng ký Undo
        Undo.RegisterCreatedObjectUndo(go, $"Create {s_Objects[index].Label}");

        // Chọn object vừa tạo trong Hierarchy
        Selection.activeGameObject = go;

        // Focus Hierarchy để người dùng có thể đổi tên ngay (F2)
        EditorGUIUtility.PingObject(go);
    }

    // ── Helpers tạo object ──────────────────────────────────────────────────

    private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (parent != null)
            go.transform.SetParent(parent, worldPositionStays: false);
        return go;
    }

    private static GameObject CreateQuad(Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Quad";
        if (parent != null)
            go.transform.SetParent(parent, worldPositionStays: false);
        return go;
    }

    private static GameObject CreateEmpty(Transform parent)
    {
        GameObject go = new GameObject("Empty");
        if (parent != null)
            go.transform.SetParent(parent, worldPositionStays: false);
        return go;
    }

    private static GameObject CreateTextMeshPro3D(Transform parent)
    {
        // Thử tạo TextMeshPro 3D bằng MenuItem nội bộ (yêu cầu TMP package)
        // Nếu TMP không có sẵn thì fallback về Text thuần (legacy)
        GameObject go = null;

#if TMP_PRESENT || TEXTMESHPRO_3_2_OR_NEWER
        // Dùng reflection để tạo TMP 3D mà không cần hard reference TMP assembly
        try
        {
            var tmpType = System.Type.GetType(
                "TMPro.TextMeshPro, Unity.TextMeshPro") ??
                System.Type.GetType("TMPro.TextMeshPro, com.unity.textmeshpro");

            if (tmpType != null)
            {
                go = new GameObject("Text (TMP 3D)");
                go.AddComponent(tmpType);
            }
        }
        catch { go = null; }
#endif

        // Fallback: thử qua reflection mà không cần define symbol
        if (go == null)
        {
            var tmpType = System.Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro") ??
                          System.Type.GetType("TMPro.TextMeshPro, com.unity.textmeshpro");

            if (tmpType != null)
            {
                go = new GameObject("Text (TMP 3D)");
                go.AddComponent(tmpType);
            }
        }

        // Cuối cùng: nếu TMP thật sự không có → tạo legacy Text3D (TextMesh)
        if (go == null)
        {
            go = new GameObject("Text (TextMesh)");
            go.AddComponent<TextMesh>();
            Debug.LogWarning("[CreateObjectPieMenu] TextMeshPro package not found. " +
                             "Created legacy TextMesh instead. " +
                             "Import TMP via Package Manager for full support.");
        }

        if (parent != null)
            go.transform.SetParent(parent, worldPositionStays: false);

        return go;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Vẽ menu
    // ════════════════════════════════════════════════════════════════════════
    private static void DrawMenu(SceneView sv)
    {
        float t = s_AnimT;
        if (t < 0.002f) return;

        Handles.BeginGUI();

        // Backdrop
        DrawCircle(s_Center, BackdropR * t,
            new Color(0.10f, 0.10f, 0.10f, 0.62f * t),
            new Color(0.55f, 0.55f, 0.55f, 0.20f * t), 1f);

        // Direction beam (Hold mode)
        if (s_Mode == MenuMode.Hold && s_DeltaMag > DeadZone && s_HoveredIdx >= 0)
            DrawDirectionBeam(t);

        // Spoke lines
        for (int i = 0; i < s_Objects.Length; i++)
        {
            float   rad = s_Objects[i].AngleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 end = s_Center + dir * (MenuRadius - ButtonSize * 0.5f - 4f) * t;
            Color   lc  = s_Objects[i].IconColor;
            lc.a        = (i == s_HoveredIdx ? 0.65f : 0.22f) * t;
            Handles.color = lc;
            Handles.DrawLine(s_Center, end);
        }

        // Nút object
        for (int i = 0; i < s_Objects.Length; i++)
            DrawObjectButton(i, t);

        DrawCenterDot(t);

        if (s_HoveredIdx >= 0 && t > 0.4f)
            DrawTooltip(s_HoveredIdx, t);

        DrawModeHint(t);

        Handles.EndGUI();
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Vẽ từng nút
    // ────────────────────────────────────────────────────────────────────────
    private static void DrawObjectButton(int index, float t)
    {
        ObjectEntry obj    = s_Objects[index];
        float       rad    = obj.AngleDeg * Mathf.Deg2Rad;
        Vector2     pos    = s_Center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * MenuRadius * t;

        bool  hovered = s_HoveredIdx == index;
        float scale   = hovered ? HoverScale : 1f;
        float r       = ButtonSize * 0.5f * t * scale;

        Color bg = hovered
            ? new Color(obj.IconColor.r, obj.IconColor.g, obj.IconColor.b, 0.26f * t)
            : new Color(0.16f, 0.16f, 0.16f, 0.93f * t);

        Color border = hovered
            ? new Color(obj.IconColor.r, obj.IconColor.g, obj.IconColor.b, 1.00f * t)
            : new Color(0.44f, 0.44f, 0.44f, 0.50f * t);

        // "Empty" dùng hình vuông (để phân biệt với các object khác)
        // "Text" dùng hình chữ nhật bo góc → vẽ bằng hình tròn nhưng màu đặc biệt
        // Còn lại đều vẽ tròn
        if (index == 7) // Empty → diamond
            DrawDiamond(pos, r, bg, border, hovered ? 2.0f : 0.75f);
        else
            DrawCircle(pos, r, bg, border, hovered ? 2.0f : 0.75f);

        if (hovered)
        {
            if (index == 7)
                DrawDiamond(pos, r + 5f * t,
                    new Color(0,0,0,0),
                    new Color(obj.IconColor.r, obj.IconColor.g, obj.IconColor.b, 0.28f * t), 4f);
            else
                DrawCircle(pos, r + 5f * t,
                    new Color(0,0,0,0),
                    new Color(obj.IconColor.r, obj.IconColor.g, obj.IconColor.b, 0.28f * t), 4f);
        }

        // Label chính
        int labelFontSize = Mathf.RoundToInt(BtnLabelFont * t * scale);
        GUIStyle lbl = new GUIStyle(GUI.skin.label)
        {
            fontSize  = labelFontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(obj.IconColor.r, obj.IconColor.g, obj.IconColor.b, t) }
        };
        GUI.Label(new Rect(pos.x - r, pos.y - r * 0.50f, r * 2f, r * 0.95f), obj.Label, lbl);

        // Sub-label
        GUIStyle sub = new GUIStyle(GUI.skin.label)
        {
            fontSize  = Mathf.RoundToInt(BtnSubFont * t * scale),
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.72f, 0.72f, 0.72f, t * 0.85f) }
        };
        GUI.Label(new Rect(pos.x - r, pos.y + r * 0.08f, r * 2f, r * 0.55f), obj.SubLabel, sub);
    }

    private static void DrawCenterDot(float t)
    {
        float cr = CenterR * t;
        DrawCircle(s_Center, cr,
            new Color(0.13f, 0.16f, 0.20f, 0.95f * t),
            new Color(0.55f, 0.55f, 0.55f, 0.40f * t), 0.75f);

        // Hiển thị tên parent hoặc "ROOT" ở tâm
        string centerLabel = Selection.activeGameObject != null
            ? "CHILD"
            : "ROOT";
        Color centerColor = Selection.activeGameObject != null
            ? new Color(0.55f, 0.85f, 0.95f, t)
            : new Color(0.75f, 0.75f, 0.75f, t);

        GUIStyle cs = new GUIStyle(GUI.skin.label)
        {
            fontSize  = Mathf.RoundToInt(CenterFont * t),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = centerColor }
        };
        float labelW = cr * 2.2f;
        GUI.Label(new Rect(s_Center.x - labelW * 0.5f, s_Center.y - cr * 0.5f, labelW, cr),
            centerLabel, cs);
    }

    private static void DrawTooltip(int index, float t)
    {
        // Tooltip đầy đủ + gợi ý phím số
        string[] fullNames =
        {
            "Cube  [1]",
            "Sphere  [2]",
            "Capsule  [3]",
            "Cylinder  [4]",
            "Plane  [5]",
            "Quad  [6]",
            "Text – TextMeshPro 3D  [7]",
            "Empty Object  [8]",
        };

        // Dòng phụ: thông báo parent
        string parentInfo = Selection.activeGameObject != null
            ? $"→ Child of: {Selection.activeGameObject.name}"
            : "→ Created at Root (no parent)";

        ObjectEntry obj = s_Objects[index];
        string      txt = fullNames[index];

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize  = TooltipFont,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(obj.IconColor.r, obj.IconColor.g, obj.IconColor.b, t) }
        };

        GUIStyle parentStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = Mathf.Max(8, TooltipFont - 2),
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.65f, 0.65f, 0.65f, t * 0.85f) }
        };

        Vector2 sz1 = style.CalcSize(new GUIContent(txt));
        Vector2 sz2 = parentStyle.CalcSize(new GUIContent(parentInfo));
        float   pw  = Mathf.Max(sz1.x, sz2.x) + 24f;
        float   ph  = sz1.y + sz2.y + 14f;

        Rect bg = new Rect(s_Center.x - pw * 0.5f,
                           s_Center.y + BackdropR * 0.58f * t, pw, ph);

        EditorGUI.DrawRect(bg, new Color(0.09f, 0.09f, 0.09f, 0.90f * t));
        DrawRectBorder(bg, new Color(obj.IconColor.r, obj.IconColor.g, obj.IconColor.b, 0.50f * t), 0.75f);

        Rect r1 = new Rect(bg.x, bg.y + 5f, bg.width, sz1.y + 2f);
        Rect r2 = new Rect(bg.x, r1.yMax,   bg.width, sz2.y + 4f);
        GUI.Label(r1, txt,        style);
        GUI.Label(r2, parentInfo, parentStyle);
    }

    private static void DrawModeHint(float t)
    {
        if (t < 0.5f) return;
        float  alpha  = (t - 0.5f) * 2f;
        //string hotkey = HotKeyDisplayString();
        // string hint   = s_Mode == MenuMode.Hold
        //     ? $"[{hotkey}] Hold & drag to select  •  Release to confirm"
        //     : $"[{hotkey}] Click to select  •  1–8 shortcuts  •  Esc / RMB to close";

        GUIStyle hs = new GUIStyle(GUI.skin.label)
        {
            fontSize  = HintFont,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.58f, 0.58f, 0.58f, alpha) }
        };
        float hintW = 480f;
        GUI.Label(
            new Rect(s_Center.x - hintW * 0.5f,
                     s_Center.y - BackdropR * 0.72f * t, hintW, HintFont + 6f),
            "", hs);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Direction beam
    // ════════════════════════════════════════════════════════════════════════
    private static void DrawDirectionBeam(float t)
    {
        int     hi      = s_HoveredIdx;
        Color   col     = s_Objects[hi].IconColor;
        float   len     = Mathf.Min(s_DeltaMag, BackdropR * 0.86f);
        Vector2 dir     = s_MouseDelta.normalized;
        Vector2 beamEnd = s_Center + dir * len * t;

        Handles.color = new Color(col.r, col.g, col.b, 0.18f * t);
        Handles.DrawAAPolyLine(len * 0.20f, s_Center, beamEnd);

        Handles.color = new Color(col.r, col.g, col.b, 0.72f * t);
        Handles.DrawAAPolyLine(1.5f, s_Center, beamEnd);

        float   arrowSize = BeamArrow * t;
        Vector2 perp      = new Vector2(-dir.y, dir.x);
        Vector3 tip       = new Vector3(beamEnd.x, beamEnd.y);
        Vector3 lw        = new Vector3(
            beamEnd.x - dir.x * arrowSize + perp.x * arrowSize * 0.45f,
            beamEnd.y - dir.y * arrowSize + perp.y * arrowSize * 0.45f);
        Vector3 rw        = new Vector3(
            beamEnd.x - dir.x * arrowSize - perp.x * arrowSize * 0.45f,
            beamEnd.y - dir.y * arrowSize - perp.y * arrowSize * 0.45f);

        Handles.color = new Color(col.r, col.g, col.b, 0.90f * t);
        Handles.DrawAAPolyLine(1.8f, lw, tip, rw);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Helpers vẽ hình học
    // ════════════════════════════════════════════════════════════════════════
    private static void DrawCircle(Vector2 center, float radius,
                                   Color fill, Color border, float borderW)
    {
        const int SEG = 48;
        if (radius < 0.5f) return;

        if (fill.a > 0.005f)
        {
            Handles.color = fill;
            var pts = new Vector3[SEG + 1];
            for (int i = 0; i <= SEG; i++)
            {
                float a = i * 2f * Mathf.PI / SEG;
                pts[i]  = new Vector3(center.x + Mathf.Cos(a) * radius,
                                      center.y + Mathf.Sin(a) * radius);
            }
            Handles.DrawAAConvexPolygon(pts);
        }
        if (border.a > 0.005f && borderW > 0f)
        {
            Handles.color = border;
            const int BSEG = 64;
            var bpts = new Vector3[BSEG + 1];
            for (int i = 0; i <= BSEG; i++)
            {
                float a = i * 2f * Mathf.PI / BSEG;
                bpts[i] = new Vector3(center.x + Mathf.Cos(a) * radius,
                                      center.y + Mathf.Sin(a) * radius);
            }
            Handles.DrawAAPolyLine(borderW, bpts);
        }
    }

    // Diamond cho Empty object (giống Local View trong ViewAxisPieMenu)
    private static void DrawDiamond(Vector2 center, float radius,
                                    Color fill, Color border, float borderW)
    {
        if (radius < 0.5f) return;
        float r     = radius * 1.05f;
        Vector3 top   = new Vector3(center.x,     center.y - r);
        Vector3 right = new Vector3(center.x + r, center.y    );
        Vector3 bot   = new Vector3(center.x,     center.y + r);
        Vector3 left  = new Vector3(center.x - r, center.y    );

        if (fill.a > 0.005f)
        { Handles.color = fill; Handles.DrawAAConvexPolygon(top, right, bot, left); }
        if (border.a > 0.005f && borderW > 0f)
        { Handles.color = border; Handles.DrawAAPolyLine(borderW, top, right, bot, left, top); }
    }

    private static void DrawRectBorder(Rect r, Color color, float width)
    {
        Handles.color = color;
        Handles.DrawAAPolyLine(width,
            new Vector3(r.xMin, r.yMin), new Vector3(r.xMax, r.yMin),
            new Vector3(r.xMax, r.yMax), new Vector3(r.xMin, r.yMax),
            new Vector3(r.xMin, r.yMin));
    }
}
#endif
