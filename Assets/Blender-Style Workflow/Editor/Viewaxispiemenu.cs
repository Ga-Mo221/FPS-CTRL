// ViewAxisPieMenu.cs
// Đặt file này vào: Assets/Editor/ViewAxisPieMenu.cs
//
// ── Hai chế độ sử dụng ───────────────────────────────────────────────────────
//
//  [HOLD MODE]   Nhấn GIỮ phím tắt  →  menu hiện
//                Lia chuột về phía trục muốn chọn  →  trục đó được highlight
//                THẢ phím           →  áp dụng trục đang highlight & đóng menu
//                Thả mà không lia chuột (dead-zone) = không làm gì, chỉ đóng
//
//  [TOGGLE MODE] Nhấn rồi THẢ NHANH (< HoldThreshold giây)
//                →  menu mở cố định, dùng chuột click để chọn như bình thường
//                Nhấn lại phím tắt, hoặc Escape, hoặc chuột phải → đóng
//
//  Phím 1–6 chọn nhanh khi menu đang mở (bất kể chế độ nào).
//
//  Cấu hình phím tắt & tham số tại: Edit → Preferences → View Axis Pie Menu
// ─────────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
using GaMo.BlenderEditorTools;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ViewAxisPieMenu
{
    // ════════════════════════════════════════════════════════════════════════
    //  Hằng số hiển thị (không cấu hình được, chỉ ảnh hưởng visual)
    // ════════════════════════════════════════════════════════════════════════
    private const float  ANIM_SPEED = 10f;   // tốc độ lerp mở/đóng

    // ── Shorthand đọc Settings ───────────────────────────────────────────────
    private static float  MenuRadius      => BlenderEditorPreferences.PieMenuRadius;
    private static float  ButtonSize      => BlenderEditorPreferences.PieMenuButtonSize;
    private static float  HoldThresh      => BlenderEditorPreferences.PieMenuHoldThreshold;
    private static float  DeadZone        => BlenderEditorPreferences.PieMenuDeadZone;
    private static double SnapDuration    => BlenderEditorPreferences.PieMenuSnapDuration;

    // Visual sizes – đọc động từ Preferences
    private static float  BackdropRadius  => BlenderEditorPreferences.PieMenuBackdropRadius;
    private static float  CenterRadius    => BlenderEditorPreferences.PieMenuCenterRadius;
    private static int    CenterFont      => BlenderEditorPreferences.PieMenuCenterFontSize;
    private static int    BtnLabelFont    => BlenderEditorPreferences.PieMenuBtnLabelFontSize;
    private static int    BtnSubFont      => BlenderEditorPreferences.PieMenuBtnSubFontSize;
    private static int    TooltipFont     => BlenderEditorPreferences.PieMenuTooltipFontSize;
    private static int    HintFont        => BlenderEditorPreferences.PieMenuHintFontSize;
    private static float  HoverScale      => BlenderEditorPreferences.PieMenuHoverScale;
    private static float  BeamArrowSize   => BlenderEditorPreferences.PieMenuBeamArrowSize;

    // ════════════════════════════════════════════════════════════════════════
    //  Kiểu & trạng thái nội bộ
    // ════════════════════════════════════════════════════════════════════════
    private enum MenuMode { Closed, Hold, Toggle }

    private static MenuMode s_Mode        = MenuMode.Closed;
    private static bool     s_KeyHeld     = false;
    private static double   s_KeyDownTime = 0.0;

    private static float    s_AnimT       = 0f;
    private static Vector2  s_Center;

    private static int      s_HoveredIndex  = -1;
    private static int      s_SelectedIndex = -1;

    private static Vector2  s_MouseDelta;
    private static float    s_DeltaMag;

    // Snap
    private static bool       s_IsSnapping;
    private static double     s_SnapStartTime;
    private static Quaternion s_SnapFromRot;
    private static Quaternion s_SnapToRot;
    private static Vector3    s_SnapFromPivot;
    private static Vector3    s_SnapToPivot;
    private static float      s_SnapFromSize;
    private static float      s_SnapToSize;
    private static SceneView  s_SnapTarget;

    // ════════════════════════════════════════════════════════════════════════
    //  Định nghĩa 8 slot (7 trục + Local View, slot dưới-trái bỏ trống)
    // ════════════════════════════════════════════════════════════════════════
    private struct AxisEntry
    {
        public string     Label;
        public string     SubLabel;
        public Quaternion Rot;
        public Color      IconColor;
        public float      AngleDeg;
        public bool       IsLocalView;   // true → gọi lệnh Local View thay vì snap camera
    }

    private static readonly AxisEntry[] s_Axes =
    {
        // Phải (0°) → +X / FRONT
        new AxisEntry {
            Label="+X", SubLabel="FRONT",
            Rot=Quaternion.LookRotation(Vector3.left,    Vector3.up),
            IconColor=new Color(0.91f,0.33f,0.26f), AngleDeg=0f    },
        // Dưới phải (45°) → Local View
        new AxisEntry {
            Label="LOCAL", SubLabel="VIEW",
            Rot=Quaternion.identity,
            IconColor=new Color(0.85f,0.65f,0.13f), AngleDeg=45f,
            IsLocalView=true },
        // Dưới (90°) → -Y / BOTTOM
        new AxisEntry {
            Label="-Y", SubLabel="BTM",
            Rot=Quaternion.LookRotation(Vector3.up,      Vector3.back),
            IconColor=new Color(0.21f,0.52f,0.24f), AngleDeg=90f   },
        // Trái (180°) → -Z / LEFT
        new AxisEntry {
            Label="-Z", SubLabel="LEFT",
            Rot=Quaternion.LookRotation(Vector3.forward, Vector3.up),
            IconColor=new Color(0.08f,0.42f,0.71f), AngleDeg=180f  },
        // Trái trên (-135° = 225°) → +Z / RIGHT
        new AxisEntry {
            Label="+Z", SubLabel="RIGHT",
            Rot=Quaternion.LookRotation(Vector3.back,    Vector3.up),
            IconColor=new Color(0.10f,0.53f,0.91f), AngleDeg=-135f },
        // Trên (-90°) → +Y / TOP
        new AxisEntry {
            Label="+Y", SubLabel="TOP",
            Rot=Quaternion.LookRotation(Vector3.down,    Vector3.forward),
            IconColor=new Color(0.30f,0.69f,0.31f), AngleDeg=-90f  },
        // Trên phải (-45°) → -X / BACK
        new AxisEntry {
            Label="-X", SubLabel="BACK",
            Rot=Quaternion.LookRotation(Vector3.right,   Vector3.up),
            IconColor=new Color(0.75f,0.26f,0.18f), AngleDeg=-45f  },
    };

    // ════════════════════════════════════════════════════════════════════════
    //  Bootstrap
    // ════════════════════════════════════════════════════════════════════════
    static ViewAxisPieMenu()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Entry point
    // ════════════════════════════════════════════════════════════════════════
    private static void OnSceneGUI(SceneView sv)
    {
        // Plugin bị tắt từ Preferences
        if (!BlenderEditorPreferences.PieMenuEnabled) return;

        Event e = Event.current;
        HandleKeyboard(e, sv);

        bool menuVisible = s_Mode != MenuMode.Closed;

        if (!menuVisible && s_AnimT < 0.001f)
        {
            HandleSnapAnimation(sv);
            return;
        }

        float animTarget = menuVisible ? 1f : 0f;
        s_AnimT = Mathf.Lerp(s_AnimT, animTarget,
                  1f - Mathf.Exp(-ANIM_SPEED * 0.016f));
        if (!menuVisible && s_AnimT < 0.002f) s_AnimT = 0f;

        if (e != null) UpdateDirection(e);

        DrawMenu(sv);

        if (s_Mode == MenuMode.Toggle && e != null)
            HandleToggleClick(e, sv);

        HandleSnapAnimation(sv);

        if (s_AnimT > 0.001f || menuVisible)
            sv.Repaint();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Keyboard
    // ════════════════════════════════════════════════════════════════════════
    private static void HandleKeyboard(Event e, SceneView sv)
    {
        if (e == null) return;

        bool isHotKey = BlenderEditorPreferences.PieMenuMatchesHotKey(e);

        // ── KeyDown ──────────────────────────────────────────────────────
        if (e.type == EventType.KeyDown && isHotKey && !s_KeyHeld)
        {
            e.Use();
            s_KeyHeld     = true;
            s_KeyDownTime = EditorApplication.timeSinceStartup;

            if (s_Mode == MenuMode.Toggle)
            {
                ForceClose();
                return;
            }

            OpenMenu(e, sv);
            s_Mode = MenuMode.Hold;
            return;
        }

        // ── KeyUp ─────────────────────────────────────────────────────────
        // Bắt KeyUp của phím chính (dù modifier đã thả)
        if (e.type == EventType.KeyUp &&
            e.keyCode == BlenderEditorPreferences.PieMenuHotKey && s_KeyHeld)
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
                    if (s_HoveredIndex >= 0)
                        SelectAxis(s_HoveredIndex, sv);
                    else
                        ForceClose();
                }
            }
            return;
        }

        // ── Escape ────────────────────────────────────────────────────────
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape
            && s_Mode != MenuMode.Closed)
        {
            e.Use();
            ForceClose();
            return;
        }

        // ── Phím số 1–7 ──────────────────────────────────────────────────
        if (s_Mode != MenuMode.Closed && e.type == EventType.KeyDown)
        {
            int idx = e.keyCode switch
            {
                KeyCode.Alpha1 => 0, KeyCode.Alpha2 => 1,
                KeyCode.Alpha3 => 2, KeyCode.Alpha4 => 3,
                KeyCode.Alpha5 => 4, KeyCode.Alpha6 => 5,
                KeyCode.Alpha7 => 6,
                _ => -1
            };
            if (idx >= 0) { e.Use(); SelectAxis(idx, sv); }
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
        s_HoveredIndex = -1;
        s_MouseDelta   = Vector2.zero;
        s_DeltaMag     = 0f;
        sv.Repaint();
    }

    private static void ForceClose()
    {
        s_Mode         = MenuMode.Closed;
        s_KeyHeld      = false;
        s_HoveredIndex = -1;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Direction
    // ════════════════════════════════════════════════════════════════════════
    private static void UpdateDirection(Event e)
    {
        if (s_Mode == MenuMode.Closed) return;
        s_MouseDelta   = e.mousePosition - s_Center;
        s_DeltaMag     = s_MouseDelta.magnitude;
        s_HoveredIndex = GetIndexByAngle(s_MouseDelta, s_DeltaMag);
    }

    private static int GetIndexByAngle(Vector2 delta, float mag)
    {
        if (mag < DeadZone) return -1;

        float angleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        int   best     = -1;
        float bestDiff = float.MaxValue;

        for (int i = 0; i < s_Axes.Length; i++)
        {
            float diff = Mathf.Abs(NormalizeAngle(s_Axes[i].AngleDeg - angleDeg));
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }
        return best;
    }

    private static float NormalizeAngle(float a)
        => ((a + 540f) % 360f) - 180f;

    // ════════════════════════════════════════════════════════════════════════
    //  Toggle click
    // ════════════════════════════════════════════════════════════════════════
    private static void HandleToggleClick(Event e, SceneView sv)
    {
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (s_HoveredIndex >= 0)
            { e.Use(); SelectAxis(s_HoveredIndex, sv); }
            else if (s_DeltaMag > BackdropRadius)
            { e.Use(); ForceClose(); }
        }
        if (e.type == EventType.MouseDown && e.button == 1)
        { e.Use(); ForceClose(); }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Chọn trục
    // ════════════════════════════════════════════════════════════════════════
    private static void SelectAxis(int index, SceneView sv)
    {
        s_SelectedIndex = index;
        ForceClose();

        if (s_Axes[index].IsLocalView)
        {
            ToggleLocalView(sv);
        }
        else
        {
            BeginSnap(sv, s_Axes[index].Rot);
        }
    }

    // Toggle Local View (Shift+H) -- khong dung ExecuteMenuItem vi path thay doi theo Unity version
    private static void ToggleLocalView(SceneView sv)
    {
        var svType = typeof(SceneView);

        // Thu goi internal method "ToggleLocalView" (ton tai trong Unity 2022+)
        var method = svType.GetMethod("ToggleLocalView",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (method != null)
        {
            method.Invoke(sv, null);
            return;
        }

        // Fallback: gui Shift+H event gia vao SceneView
        sv.SendEvent(new Event
        {
            type      = EventType.KeyDown,
            keyCode   = KeyCode.H,
            modifiers = EventModifiers.Shift
        });
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
        DrawCircle(s_Center, BackdropRadius * t,
            new Color(0.10f, 0.10f, 0.10f, 0.60f * t),
            new Color(0.55f, 0.55f, 0.55f, 0.18f * t), 1f);

        // Direction beam (Hold Mode)
        if (s_Mode == MenuMode.Hold && s_DeltaMag > DeadZone && s_HoveredIndex >= 0)
            DrawDirectionBeam(t);

        // Spoke lines
        for (int i = 0; i < s_Axes.Length; i++)
        {
            float   rad = s_Axes[i].AngleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 end = s_Center + dir * (MenuRadius - ButtonSize * 0.5f - 4f) * t;
            Color   lc  = s_Axes[i].IconColor;
            lc.a        = (i == s_HoveredIndex ? 0.65f : 0.22f) * t;
            Handles.color = lc;
            Handles.DrawLine(s_Center, end);
        }

        // Nút trục
        for (int i = 0; i < s_Axes.Length; i++)
            DrawAxisButton(i, t);

        DrawCenterDot(t);

        if (s_HoveredIndex >= 0 && t > 0.4f)
            DrawTooltip(s_HoveredIndex, t);

        DrawModeHint(t);

        Handles.EndGUI();
    }

    private static void DrawDirectionBeam(float t)
    {
        int     hi      = s_HoveredIndex;
        Color   col     = s_Axes[hi].IconColor;
        float   len     = Mathf.Min(s_DeltaMag, BackdropRadius * 0.86f);
        Vector2 dir     = s_MouseDelta.normalized;
        Vector2 beamEnd = s_Center + dir * len * t;

        Handles.color = new Color(col.r, col.g, col.b, 0.18f * t);
        Handles.DrawAAPolyLine(len * 0.20f, s_Center, beamEnd);

        Handles.color = new Color(col.r, col.g, col.b, 0.72f * t);
        Handles.DrawAAPolyLine(1.5f, s_Center, beamEnd);

        float   arrowSize = BeamArrowSize * t;
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

    private static void DrawAxisButton(int index, float t)
    {
        AxisEntry ax    = s_Axes[index];
        float     rad   = ax.AngleDeg * Mathf.Deg2Rad;
        Vector2   pos   = s_Center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad))
                          * MenuRadius * t;

        bool  hovered  = s_HoveredIndex  == index;
        bool  selected = s_SelectedIndex == index;
        float scale    = hovered ? HoverScale : 1f;
        float r        = ButtonSize * 0.5f * t * scale;

        Color bg = hovered
            ? new Color(ax.IconColor.r, ax.IconColor.g, ax.IconColor.b, 0.26f * t)
            : selected
                ? new Color(ax.IconColor.r, ax.IconColor.g, ax.IconColor.b, 0.16f * t)
                : new Color(0.16f, 0.16f, 0.16f, 0.93f * t);

        Color border = hovered
            ? new Color(ax.IconColor.r, ax.IconColor.g, ax.IconColor.b, 1.00f * t)
            : selected
                ? new Color(ax.IconColor.r, ax.IconColor.g, ax.IconColor.b, 0.55f * t)
                : new Color(0.44f, 0.44f, 0.44f, 0.50f * t);

        // Local View: viền nét đứt (diamond shape thay vì tròn)
        if (ax.IsLocalView)
            DrawDiamond(pos, r, bg, border, hovered ? 2.0f : 0.75f);
        else
            DrawCircle(pos, r, bg, border, hovered ? 2.0f : 0.75f);

        if (hovered)
        {
            if (ax.IsLocalView)
                DrawDiamond(pos, r + 5f * t,
                    new Color(0, 0, 0, 0),
                    new Color(ax.IconColor.r, ax.IconColor.g, ax.IconColor.b, 0.28f * t),
                    4f);
            else
                DrawCircle(pos, r + 5f * t,
                    new Color(0, 0, 0, 0),
                    new Color(ax.IconColor.r, ax.IconColor.g, ax.IconColor.b, 0.28f * t),
                    4f);
        }

        // Label chính
        int labelFontSize = ax.IsLocalView
            ? Mathf.RoundToInt(BtnSubFont * t * scale * 1.1f)   // "LOCAL" nhỏ hơn
            : Mathf.RoundToInt(BtnLabelFont * t * scale);

        GUIStyle lbl = new GUIStyle(GUI.skin.label)
        {
            fontSize  = labelFontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(ax.IconColor.r, ax.IconColor.g, ax.IconColor.b, t) }
        };
        GUI.Label(new Rect(pos.x - r, pos.y - r * 0.50f, r * 2f, r * 0.95f), ax.Label, lbl);

        GUIStyle sub = new GUIStyle(GUI.skin.label)
        {
            fontSize  = Mathf.RoundToInt(BtnSubFont * t * scale),
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.72f, 0.72f, 0.72f, t * 0.85f) }
        };
        GUI.Label(new Rect(pos.x - r, pos.y + r * 0.08f, r * 2f, r * 0.55f), ax.SubLabel, sub);
    }

    private static void DrawCenterDot(float t)
    {
        float cr = CenterRadius * t;
        DrawCircle(s_Center, cr,
            new Color(0.17f, 0.17f, 0.17f, 0.95f * t),
            new Color(0.55f, 0.55f, 0.55f, 0.40f * t), 0.75f);

        GUIStyle cs = new GUIStyle(GUI.skin.label)
        {
            fontSize  = Mathf.RoundToInt(CenterFont * t),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.75f, 0.75f, 0.75f, t) }
        };
        float labelW = cr * 2.2f;
        GUI.Label(new Rect(s_Center.x - labelW * 0.5f, s_Center.y - cr * 0.5f, labelW, cr), "VIEW", cs);
    }

    private static void DrawTooltip(int index, float t)
    {
        string[] names = {
            "Front (+X)", "Local View", "Bottom (−Y)",
            "Left (−Z)",  "Right (+Z)", "Top (+Y)", "Back (−X)"
        };
        AxisEntry ax  = s_Axes[index];
        string    txt = names[index];

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize  = TooltipFont,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(ax.IconColor.r, ax.IconColor.g,
                                                ax.IconColor.b, t) }
        };
        Vector2 sz = style.CalcSize(new GUIContent(txt));
        float   pw = sz.x + 24f, ph = sz.y + 10f;
        Rect    bg = new Rect(s_Center.x - pw * 0.5f,
                              s_Center.y + BackdropRadius * 0.58f * t, pw, ph);

        EditorGUI.DrawRect(bg, new Color(0.09f, 0.09f, 0.09f, 0.90f * t));
        DrawRectBorder(bg,
            new Color(ax.IconColor.r, ax.IconColor.g, ax.IconColor.b, 0.50f * t),
            0.75f);
        GUI.Label(bg, txt, style);
    }

    private static void DrawModeHint(float t)
    {
        if (t < 0.5f) return;
        float  alpha  = (t - 0.5f) * 2f;
        string hotkey = BlenderEditorPreferences.PieMenuHotKeyDisplayString();
        string hint   = s_Mode == MenuMode.Hold
            ? $""
            : $"";

        GUIStyle hs = new GUIStyle(GUI.skin.label)
        {
            fontSize  = HintFont,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.58f, 0.58f, 0.58f, alpha) }
        };
        float hintW = 420f;
        GUI.Label(
            new Rect(s_Center.x - hintW * 0.5f,
                     s_Center.y - BackdropRadius * 0.72f * t, hintW, HintFont + 6f),
            hint, hs);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Snap animation
    // ════════════════════════════════════════════════════════════════════════
    private static void BeginSnap(SceneView sv, Quaternion targetRot)
    {
        s_SnapTarget    = sv;
        s_SnapFromRot   = sv.rotation;
        s_SnapToRot     = targetRot;
        s_SnapFromPivot = sv.pivot;
        s_SnapToPivot   = sv.pivot;
        s_SnapFromSize  = sv.size;
        s_SnapToSize    = sv.size;
        s_SnapStartTime = EditorApplication.timeSinceStartup;
        s_IsSnapping    = true;

        if (BlenderEditorPreferences.PieMenuOrthoOnSnap)
            sv.orthographic = true;
    }

    private static void HandleSnapAnimation(SceneView sv)
    {
        if (!s_IsSnapping || s_SnapTarget != sv) return;

        float t    = Mathf.Clamp01(
                     (float)((EditorApplication.timeSinceStartup - s_SnapStartTime)
                             / SnapDuration));
        float ease = 1f - Mathf.Pow(1f - t, 3f);

        sv.rotation = Quaternion.Slerp(s_SnapFromRot, s_SnapToRot, ease);
        sv.pivot    = Vector3.Lerp(s_SnapFromPivot, s_SnapToPivot, ease);
        sv.size     = Mathf.Lerp(s_SnapFromSize, s_SnapToSize, ease);
        sv.Repaint();

        if (t >= 1f) s_IsSnapping = false;
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

    // Diamond shape cho nút Local View
    private static void DrawDiamond(Vector2 center, float radius,
                                    Color fill, Color border, float borderW)
    {
        if (radius < 0.5f) return;
        float r = radius * 1.05f;
        Vector3 top   = new Vector3(center.x,     center.y - r);
        Vector3 right = new Vector3(center.x + r, center.y    );
        Vector3 bot   = new Vector3(center.x,     center.y + r);
        Vector3 left  = new Vector3(center.x - r, center.y    );

        if (fill.a > 0.005f)
        {
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(top, right, bot, left);
        }
        if (border.a > 0.005f && borderW > 0f)
        {
            Handles.color = border;
            Handles.DrawAAPolyLine(borderW, top, right, bot, left, top);
        }
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