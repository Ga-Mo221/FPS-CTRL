// =============================================================
//  AnimatorControllerCreator.cs
//  Unity Editor Tool — tạo nhanh Animator Controller.
//
//  Tabs:
//    0 — Tạo Controller  (Weapon mode hoặc Character mode)
//    1 — Weapon Clips    (chỉ hiện / có hiệu lực ở Character mode)
//    2 — Đổi Tên Clips
//    3 — Cắt Clip
//
//  Character mode vs Weapon mode:
//    • Weapon  : Spine layer dùng SpineLayerBuilder (state đơn RELOAD / SWITCH)
//    • Character: Spine layer dùng CharacterSpineLayerBuilder
//                 (Sub State Machine RELOAD + SWITCH, phân nhánh theo WeaponIndex)
//                 Thêm parameter "WeaponIndex" (Int)
// =============================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class AnimatorControllerCreator : EditorWindow
{
    // ── Tabs ──────────────────────────────────────────────────
    private int selectedTab = 0;
    private static readonly string[] TabNames =
        { "Tạo Controller", "Weapon Clips", "Đổi Tên Clips", "Cắt Clip" };

    // ── Controller Mode ───────────────────────────────────────
    private enum ControllerMode { Weapon, Character }
    private ControllerMode controllerMode = ControllerMode.Weapon;

    // ── UI State (Tab 0) ──────────────────────────────────────
    private string  controllerName = "NewCharacter";
    private string  savePath       = "Assets/Animations/Controllers";
    private Vector2 scroll;

    // ── Existing Controller Slot (gán clip vào controller có sẵn) ──
    [SerializeField] private AnimatorController existingController;
    private enum ClipApplyMode { FillEmptyOnly, OverwriteAll }
    private ClipApplyMode clipApplyMode = ClipApplyMode.FillEmptyOnly;

    // ── Rename Clips State (Tab 2) ────────────────────────────
    [SerializeField] private List<AnimationClip> renameClips = new List<AnimationClip>();
    private string  removePrefix = "";
    private string  removeSuffix = "";
    private string  findText     = "";
    private string  replaceText  = "";
    private Vector2 renameScroll;

    // ── Trim Clips State (Tab 3) ──────────────────────────────
    [SerializeField] private ClipTrimmerTab trimmerTab = new ClipTrimmerTab();

    // ── Weapon Clips (Tab 1, Character mode) ─────────────────
    [SerializeField] private WeaponClipsTab weaponClipsTab = new WeaponClipsTab();
    private Vector2 weaponClipsScroll;

    // ── Shared Clip Sources ───────────────────────────────────
    [SerializeField] public List<AnimationClip> singleClips = new List<AnimationClip>();

    // ── Layer Builders ────────────────────────────────────────
    private readonly FullBodyLayerBuilder         fullBodyBuilder   = new FullBodyLayerBuilder();
    private readonly ShootLayerBuilder            shootBuilder      = new ShootLayerBuilder();
    private readonly SpineLayerBuilder            spineBuilder      = new SpineLayerBuilder();
    private readonly CharacterSpineLayerBuilder   charSpineBuilder  = new CharacterSpineLayerBuilder();
    private readonly DeadLayerBuilder             deadBuilder       = new DeadLayerBuilder();

    // ── Parameter Definitions ─────────────────────────────────
    private struct ParamDef
    {
        public string name;
        public AnimatorControllerParameterType type;
        public bool  defaultBool;
        public float defaultFloat;
    }

    private static readonly ParamDef[] DefaultParams =
    {
        new ParamDef { name = "IsDead",       type = AnimatorControllerParameterType.Bool,    defaultBool  = false },
        new ParamDef { name = "IsGrouded",    type = AnimatorControllerParameterType.Bool,    defaultBool  = true  },
        new ParamDef { name = "MoveX",        type = AnimatorControllerParameterType.Float,   defaultFloat = 0f    },
        new ParamDef { name = "MoveY",        type = AnimatorControllerParameterType.Float,   defaultFloat = 0f    },
        new ParamDef { name = "IsCrouching",  type = AnimatorControllerParameterType.Bool,    defaultBool  = false },
        new ParamDef { name = "IsSliding",    type = AnimatorControllerParameterType.Bool,    defaultBool  = false },
        new ParamDef { name = "IsJumping",    type = AnimatorControllerParameterType.Trigger                       },
        new ParamDef { name = "ADS",          type = AnimatorControllerParameterType.Bool,    defaultBool  = false },
        new ParamDef { name = "IsReloading",  type = AnimatorControllerParameterType.Bool,    defaultBool  = false },
        new ParamDef { name = "IsThrow",      type = AnimatorControllerParameterType.Bool,    defaultBool  = false },
        new ParamDef { name = "GrenadeID",    type = AnimatorControllerParameterType.Int                           },
        new ParamDef { name = "IsSwitching",  type = AnimatorControllerParameterType.Bool,    defaultBool  = false },
        new ParamDef { name = "IsShooting",   type = AnimatorControllerParameterType.Trigger                       },
        new ParamDef { name = "ShootIndex",   type = AnimatorControllerParameterType.Int                           },
        new ParamDef { name = "CurrentBullet",type = AnimatorControllerParameterType.Int                           },
    };

    // ─────────────────────────────────────────────────────────
    [MenuItem("Tools/Animator Controller Creator")]
    public static void ShowWindow()
    {
        var win = GetWindow<AnimatorControllerCreator>("AC Creator");
        win.minSize = new Vector2(480, 700);
    }

    // =========================================================
    // ON GUI
    // =========================================================
    private void OnGUI()
    {
        EditorGUILayout.Space(3);
        selectedTab = GUILayout.Toolbar(selectedTab, TabNames,
            EditorStyles.toolbarButton, GUILayout.Height(20));
        EditorGUILayout.Space(10);

        switch (selectedTab)
        {
            case 1: DrawWeaponClipsTab(); return;
            case 2: DrawRenameTab();      return;
            case 3: DrawTrimTab();        return;
            default: DrawCreateTab();     break;
        }
    }

    // =========================================================
    // TAB 0 — TẠO CONTROLLER
    // =========================================================
    private void DrawCreateTab()
    {
        GetSplitHeights(out float scrollHeight, out float footerHeight);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(scrollHeight));

        // ── Mode chọn ─────────────────────────────────────────
        EditorGUILayout.LabelField("Chế độ Controller", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            EditorGUI.BeginChangeCheck();
            controllerMode = (ControllerMode)GUILayout.Toolbar(
                (int)controllerMode,
                new[] { "Weapon", "Character" },
                GUILayout.Height(24));
            if (EditorGUI.EndChangeCheck()) Repaint();

            if (controllerMode == ControllerMode.Character)
            {
                EditorGUILayout.HelpBox(
                    "Character mode: Spine layer dùng Sub State Machine RELOAD và SWITCH.\n" +
                    "Cấu hình weapon entries ở tab \"Weapon Clips\".\n" +
                    "Parameter WeaponIndex (Int) sẽ được thêm tự động.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Weapon mode: Spine layer dùng state đơn RELOAD / SWITCH OUT / SWITCH IN.",
                    MessageType.None);
            }
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6);

        // ── Animator Controller có sẵn (tùy chọn) ──────────────
        EditorGUILayout.LabelField("Animator Controller có sẵn (tùy chọn)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        existingController = (AnimatorController)EditorGUILayout.ObjectField(
            "Controller có sẵn", existingController, typeof(AnimatorController), false);

        if (existingController != null)
        {
            EditorGUILayout.Space(4);
            clipApplyMode = (ClipApplyMode)GUILayout.Toolbar(
                (int)clipApplyMode,
                new[] { "Chỉ điền state trống", "Ghi đè tất cả" },
                GUILayout.Height(22));

            EditorGUILayout.HelpBox(
                clipApplyMode == ClipApplyMode.FillEmptyOnly
                    ? "Sẽ tìm mọi state (mọi layer, mọi Sub State Machine, kể cả các ô trong BlendTree " +
                      "Locomotion Stand/Crouch) đang KHÔNG có clip, và gán clip phù hợp từ danh sách " +
                      "Clips + slot Stand/Crouch bên dưới. Chỗ nào đã có clip sẽ được giữ nguyên."
                    : "Sẽ thay clip của TẤT CẢ state (mọi layer, mọi Sub State Machine) VÀ tất cả ô trong " +
                      "BlendTree Stand/Crouch bằng clip phù hợp tìm được, kể cả chỗ đã có clip từ trước.\n" +
                      "Không tạo state/BlendTree mới, không tạo/sửa transition — chỉ thay motion.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Để trống ô này để tạo Controller mới như bình thường.\n" +
                "Nếu kéo một Controller có sẵn vào đây, nút bên dưới sẽ chuyển thành " +
                "\"Gán Clip vào Controller\" — chỉ tìm state và gán clip, không tạo layer/transition mới.",
                MessageType.None);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6);

        // ── Output ────────────────────────────────────────────
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        controllerName = EditorGUILayout.TextField("Controller Name", controllerName);
        EditorGUILayout.BeginHorizontal();
        savePath = EditorGUILayout.TextField("Save Path", savePath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
            savePath = BrowseFolder(savePath);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6);

        // ── Drag & Drop Clips ─────────────────────────────────
        EditorGUILayout.LabelField("Kéo Thả Animation Clips (State đơn)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        var so = new SerializedObject(this);
        EditorGUILayout.PropertyField(so.FindProperty("singleClips"),
            new GUIContent("Danh sách Clips"), true);
        so.ApplyModifiedProperties();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6);

        // ── Stand Blend Tree Slots ────────────────────────────
        EditorGUILayout.LabelField(
            $"Stand Blend Tree Clips ({fullBodyBuilder.standClips.Length} slots)",
            EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        for (int i = 0; i < fullBodyBuilder.standClips.Length; i++)
        {
            fullBodyBuilder.standClips[i] = (AnimationClip)EditorGUILayout.ObjectField(
                $"Slot [{i}] {FullBodyLayerBuilder.StandLabels[i]}",
                fullBodyBuilder.standClips[i], typeof(AnimationClip), false);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6);

        // ── Crouch Blend Tree Slots ───────────────────────────
        EditorGUILayout.LabelField(
            $"Crouch Blend Tree Clips ({fullBodyBuilder.crouchClips.Length} slots  —  1 idle + 8 slow + 8 fast)",
            EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        for (int i = 0; i < fullBodyBuilder.crouchClips.Length; i++)
        {
            fullBodyBuilder.crouchClips[i] = (AnimationClip)EditorGUILayout.ObjectField(
                $"Slot [{i}] {FullBodyLayerBuilder.CrouchLabels[i]}",
                fullBodyBuilder.crouchClips[i], typeof(AnimationClip), false);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6);

        // ── Dead Clips ────────────────────────────────────────
        EditorGUILayout.LabelField("Dead Clips (4 slots)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        deadBuilder.clipDeadStand  = (AnimationClip)EditorGUILayout.ObjectField(
            "DEAD STAND",  deadBuilder.clipDeadStand,  typeof(AnimationClip), false);
        deadBuilder.clipDeadCrouch = (AnimationClip)EditorGUILayout.ObjectField(
            "DEAD CROUCH", deadBuilder.clipDeadCrouch, typeof(AnimationClip), false);
        deadBuilder.clipDeadAir   = (AnimationClip)EditorGUILayout.ObjectField(
            "DEAD AIR",   deadBuilder.clipDeadAir,   typeof(AnimationClip), false);
        deadBuilder.clipDeadSlide  = (AnimationClip)EditorGUILayout.ObjectField(
            "DEAD SLIDE",  deadBuilder.clipDeadSlide,  typeof(AnimationClip), false);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6);

        // ── Character mode: weapon entries summary ─────────────
        if (controllerMode == ControllerMode.Character)
        {
            int count = weaponClipsTab.entries?.Count ?? 0;
            EditorGUILayout.LabelField("Weapon Entries (Character mode)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Chưa có weapon entry. Sang tab \"Weapon Clips\" để thêm.",
                    MessageType.Warning);
            }
            else
            {
                foreach (var e in weaponClipsTab.entries)
                {
                    string r = e.clipReload != null ? e.clipReload.name : "–";
                    string s = e.clipSwitch != null ? e.clipSwitch.name : "–";
                    EditorGUILayout.LabelField($"  W{e.weaponIndex}", $"Reload: {r}  |  Switch: {s}");
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        // ── Parameters Preview ─────────────────────────────────
        int paramCount = DefaultParams.Length + (controllerMode == ControllerMode.Character ? 1 : 0);
        EditorGUILayout.LabelField($"Parameters  ({paramCount})", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        foreach (var p in DefaultParams)
        {
            string extra = p.type == AnimatorControllerParameterType.Bool
                ? $"  default = {p.defaultBool}"
                : p.type == AnimatorControllerParameterType.Float
                    ? $"  default = {p.defaultFloat}" : "";
            EditorGUILayout.LabelField($"• {p.name}", $"[{p.type}]{extra}");
        }
        if (controllerMode == ControllerMode.Character)
            EditorGUILayout.LabelField("• WeaponIndex", "[Int]  default = 0  ← Character mode");
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6);

        // ── Layers Preview ─────────────────────────────────────
        EditorGUILayout.LabelField("Layers  (4)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("• Full Body", "weight 0  |  Avatar Mask: Full Body");
        EditorGUILayout.LabelField("  └ States:", "IsDead, NULL, JUMP UP/AIR/PLAN, SLIDE IN/SLIDING/OUT");
        EditorGUILayout.LabelField("  └ Sub: Locomotion", "Stand (BlendTree 2D, 21 clips) + Crouch (BlendTree 2D, 17 clips)");
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("• Spine", "weight 1  |  No Mask");
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("• Shoot", "weight 1  |  No Mask");
        EditorGUILayout.LabelField("  └ States:", "IsDead, NULL, SHOOT");
        if (controllerMode == ControllerMode.Character)
        {
            int wCount = weaponClipsTab.entries?.Count ?? 0;
            EditorGUILayout.LabelField("  └ States:", "IsDead, NULL");
            EditorGUILayout.LabelField("  └ Sub: RELOAD",      $"{wCount} state(s) — RELOAD W0, RELOAD W1 …");
            EditorGUILayout.LabelField("  └ Sub: SWITCH OUT",  $"{wCount} state(s) — SWITCH OUT W0, SWITCH OUT W1 …");
            EditorGUILayout.LabelField("  └ Sub: SWITCH IN",   $"{wCount} state(s) — SWITCH IN W0, SWITCH IN W1 …");
            EditorGUILayout.LabelField("  └ Sub: ADS",     "ADS + CROUCH ADS");
            EditorGUILayout.LabelField("  └ Sub: GRENADE", "FRAG GET/IDLE/THROW + FLASH GET/IDLE/THROW");
        }
        else
        {
            EditorGUILayout.LabelField("  └ States:", "IsDead, NULL, RELOAD, SWITCH OUT, SWITCH IN");
            EditorGUILayout.LabelField("  └ Sub: ADS",     "ADS + CROUCH ADS");
            EditorGUILayout.LabelField("  └ Sub: GRENADE", "FRAG GET/IDLE/THROW + FLASH GET/IDLE/THROW");
        }
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("• Dead", "weight 1  |  No Mask");
        EditorGUILayout.LabelField("  └ States:", "NULL, DEAD STAND, DEAD CROUCH, DEAD AIR, DEAD SLIDE");
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6);

        EditorGUILayout.EndScrollView();

        // ── Footer: Nút Tạo Controller ────────────────────────
        EditorGUILayout.BeginVertical(GUILayout.Height(footerHeight));
        GUILayout.FlexibleSpace();

        bool usingExisting = existingController != null;

        bool nameOk = usingExisting || !string.IsNullOrWhiteSpace(controllerName);
        bool pathOk = usingExisting || !string.IsNullOrWhiteSpace(savePath);
        bool clipsOk = singleClips != null && singleClips.Count > 0;

        if (!usingExisting && !nameOk) EditorGUILayout.HelpBox("Hãy nhập tên controller.", MessageType.Warning);
        if (!usingExisting && !pathOk) EditorGUILayout.HelpBox("Hãy chọn đường dẫn lưu.", MessageType.Warning);
        if (usingExisting && !clipsOk) EditorGUILayout.HelpBox("Hãy kéo Animation Clips vào danh sách để có clip mà gán.", MessageType.Warning);

        GUI.enabled = usingExisting ? clipsOk : (nameOk && pathOk);
        var createBtn = new GUIStyle(GUI.skin.button)
            { fontSize = 14, fontStyle = FontStyle.Bold,
              fixedHeight = Mathf.Clamp(footerHeight - 16f, 28f, 80f) };

        string btnLabel;
        if (usingExisting)
        {
            btnLabel = clipApplyMode == ClipApplyMode.FillEmptyOnly
                ? "Gán Clip vào Controller (chỉ state trống)"
                : "Gán Clip vào Controller (ghi đè tất cả)";
        }
        else
        {
            string modeLabel = controllerMode == ControllerMode.Character ? " (Character)" : " (Weapon)";
            btnLabel = $"Tạo Controller{modeLabel}";
        }

        if (GUILayout.Button(btnLabel, createBtn))
        {
            if (usingExisting) ApplyClipsToExistingController();
            else                CreateController();
        }

        GUI.enabled = true;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // TAB 1 — WEAPON CLIPS
    // =========================================================
    private void DrawWeaponClipsTab()
    {
        if (controllerMode != ControllerMode.Character)
        {
            EditorGUILayout.HelpBox(
                "Tab này chỉ có hiệu lực khi chọn chế độ \"Character\" ở tab \"Tạo Controller\".\n" +
                "Hiện tại đang ở chế độ \"Weapon\" — dữ liệu vẫn được lưu nhưng không được dùng.",
                MessageType.Warning);
            EditorGUILayout.Space(8);
        }

        weaponClipsScroll = EditorGUILayout.BeginScrollView(weaponClipsScroll);
        weaponClipsTab.Draw();
        EditorGUILayout.EndScrollView();
    }

    // =========================================================
    // GÁN CLIP VÀO CONTROLLER CÓ SẴN
    // (không tạo layer/state/transition mới — chỉ tìm state theo
    //  tên và gán AnimationClip phù hợp từ singleClips)
    // =========================================================
    private void ApplyClipsToExistingController()
    {
        if (existingController == null) return;

        var mode = clipApplyMode == ClipApplyMode.FillEmptyOnly
            ? ControllerClipInjector.FillMode.FillEmptyOnly
            : ControllerClipInjector.FillMode.OverwriteAll;

        // ── State thường (không phải BlendTree) ────────────────
        var stateResult = ControllerClipInjector.ApplyClips(existingController, singleClips, mode);

        // ── BlendTree Stand / Crouch ────────────────────────────
        // Dùng đúng các slot [21]/[17] hiện có trên UI (đã auto-fill
        // theo tên clip), rồi bơm vào BlendTree tìm thấy trong controller.
        fullBodyBuilder.Init(singleClips);
        fullBodyBuilder.AutoFillStandClips();
        fullBodyBuilder.AutoFillCrouchClips();
        fullBodyBuilder.CopyCrouchFastGroupFromSlow();

        var btResult = ControllerClipInjector.ApplyBlendTreeClips(
            existingController, fullBodyBuilder.standClips, fullBodyBuilder.crouchClips, mode);

        EditorUtility.SetDirty(existingController);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = existingController;

        string modeStr = clipApplyMode == ClipApplyMode.FillEmptyOnly
            ? "Chỉ điền state trống"
            : "Ghi đè tất cả state";

        int totalMatched = stateResult.matched + btResult.matched;
        int totalSkipped = stateResult.skipped + btResult.skipped;
        int totalNoMatch = stateResult.noMatch + btResult.noMatch;

        EditorUtility.DisplayDialog(
            "Hoàn thành!",
            $"Controller: {existingController.name}\n" +
            $"Chế độ: {modeStr}\n\n" +
            $"Đã gán clip: {totalMatched}  (state thường: {stateResult.matched}, BlendTree slot: {btResult.matched})\n" +
            $"Bỏ qua: {totalSkipped}\n" +
            $"Không tìm thấy clip phù hợp: {totalNoMatch}",
            "OK");
    }

    // =========================================================
    // CREATE CONTROLLER
    // =========================================================
    private void CreateController()
    {
        EnsureFolder(savePath);

        string assetPath = $"{savePath}/{controllerName}.controller";
        var    ac        = AnimatorController.CreateAnimatorControllerAtPath(assetPath);

        AddParameters(ac, controllerMode == ControllerMode.Character);

        // Layer 0: Full Body
        fullBodyBuilder.Init(singleClips);
        fullBodyBuilder.Build(ac, 0);

        // Layer 1: Spine
        if (controllerMode == ControllerMode.Character)
        {
            charSpineBuilder.Init(singleClips);
            charSpineBuilder.Build(ac, 1);
        }
        else
        {
            spineBuilder.Init(singleClips);
            spineBuilder.Build(ac, 1);
        }

        // Layer 2: Shoot
        shootBuilder.Init(singleClips);
        shootBuilder.Build(ac, 2);

        // Layer 3: Dead
        deadBuilder.Init(singleClips);
        deadBuilder.Build(ac, 3);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);

        string modeStr = controllerMode == ControllerMode.Character
            ? $"Character mode — {weaponClipsTab.entries?.Count ?? 0} weapon entry(s)"
            : "Weapon mode";

        EditorUtility.DisplayDialog(
            "Hoàn thành!",
            $"Đã tạo: {assetPath}\nChế độ: {modeStr}\n\nCác Animation Clips đã được gán tự động!",
            "OK");
    }

    // ─────────────────────────────────────────────────────────
    // ADD PARAMETERS
    // ─────────────────────────────────────────────────────────
    private static void AddParameters(AnimatorController ac, bool isCharacter)
    {
        while (ac.parameters.Length > 0)
            ac.RemoveParameter(0);

        foreach (var p in DefaultParams)
        {
            switch (p.type)
            {
                case AnimatorControllerParameterType.Bool:
                    ac.AddParameter(new AnimatorControllerParameter
                        { name = p.name, type = p.type, defaultBool = p.defaultBool });
                    break;
                case AnimatorControllerParameterType.Float:
                    ac.AddParameter(new AnimatorControllerParameter
                        { name = p.name, type = p.type, defaultFloat = p.defaultFloat });
                    break;
                default:
                    ac.AddParameter(new AnimatorControllerParameter
                        { name = p.name, type = p.type });
                    break;
            }
        }

        // Character mode: thêm WeaponIndex
        if (isCharacter)
        {
            ac.AddParameter(new AnimatorControllerParameter
            {
                name       = "WeaponIndex",
                type       = AnimatorControllerParameterType.Int,
                defaultInt = 0,
            });
        }
    }

    // =========================================================
    // TAB 2 — ĐỔI TÊN CLIPS HÀNG LOẠT
    // =========================================================
    private void DrawRenameTab()
    {
        GetSplitHeights(out float scrollHeight, out float footerHeight);
        renameScroll = EditorGUILayout.BeginScrollView(renameScroll, GUILayout.Height(scrollHeight));
        EditorGUILayout.Space(4);

        // ── Clip list ─────────────────────────────────────────
        EditorGUILayout.LabelField("Danh sách Clips", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        var so   = new SerializedObject(this);
        var prop = so.FindProperty("renameClips");
        EditorGUILayout.PropertyField(prop, new GUIContent("Clips cần đổi tên"), true);
        so.ApplyModifiedProperties();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6);

        // ── Xóa Prefix / Suffix ───────────────────────────────
        EditorGUILayout.LabelField("Xóa khỏi tên", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Prefix cần xóa", GUILayout.Width(120));
        removePrefix = EditorGUILayout.TextField(removePrefix);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Suffix cần xóa", GUILayout.Width(120));
        removeSuffix = EditorGUILayout.TextField(removeSuffix);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6);

        // ── Find / Replace ────────────────────────────────────
        EditorGUILayout.LabelField("Find / Replace", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Find", GUILayout.Width(120));
        findText = EditorGUILayout.TextField(findText);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Replace với", GUILayout.Width(120));
        replaceText = EditorGUILayout.TextField(replaceText);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(
            "Tìm và thay thế chuỗi bất kỳ trong tên clip (phân biệt hoa/thường)." +
            "Để trống 'Replace với' để xóa chuỗi tìm được.",
            MessageType.None);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6);

        // ── Preview ───────────────────────────────────────────
        bool hasClips  = renameClips != null && renameClips.Count > 0;
        bool hasTarget = !string.IsNullOrEmpty(removePrefix) || !string.IsNullOrEmpty(removeSuffix)
                      || !string.IsNullOrEmpty(findText);

        if (hasClips && hasTarget)
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var header   = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
            var oldStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };
            var newStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.4f,  0.9f,  0.4f)  } };
            var noChg    = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.5f,  0.5f,  0.5f)  } };

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tên hiện tại", header);
            EditorGUILayout.LabelField("→  Tên mới",   header);
            EditorGUILayout.EndHorizontal();

            foreach (var clip in renameClips)
            {
                if (clip == null) continue;
                string newName = ApplyRenameRules(clip.name, removePrefix, removeSuffix, findText, replaceText);
                bool changed   = newName != clip.name;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(clip.name, changed ? oldStyle : noChg);
                EditorGUILayout.LabelField(changed ? ("→  " + newName) : "(không đổi)", changed ? newStyle : noChg);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        EditorGUILayout.EndScrollView();

        // ── Footer ────────────────────────────────────────────
        EditorGUILayout.BeginVertical(GUILayout.Height(footerHeight));
        GUILayout.FlexibleSpace();

        int changeCount = CountRenameChanges();
        GUI.enabled = changeCount > 0;

        var btn = new GUIStyle(GUI.skin.button)
            { fontSize = 14, fontStyle = FontStyle.Bold,
              fixedHeight = Mathf.Clamp(footerHeight - 16f, 28f, 80f) };

        string btnLabel = changeCount > 0
            ? $"Đổi tên  ({changeCount} clip sẽ thay đổi)"
            : "Đổi tên";

        if (GUILayout.Button(btnLabel, btn))
            ExecuteRename();

        GUI.enabled = true;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();
    }

    private int CountRenameChanges()
    {
        if (renameClips == null) return 0;
        int count = 0;
        foreach (var clip in renameClips)
            if (clip != null && ApplyRenameRules(clip.name, removePrefix, removeSuffix, findText, replaceText) != clip.name)
                count++;
        return count;
    }

    /// <summary>
    /// Áp dụng theo thứ tự: xóa prefix → xóa suffix → find/replace.
    /// </summary>
    private static string ApplyRenameRules(string original, string prefix, string suffix,
                                           string find = "", string replace = "")
    {
        string result = original;

        // 1. Xóa prefix
        if (!string.IsNullOrEmpty(prefix) && result.StartsWith(prefix))
            result = result.Substring(prefix.Length);

        // 2. Xóa suffix
        if (!string.IsNullOrEmpty(suffix) && result.EndsWith(suffix))
            result = result.Substring(0, result.Length - suffix.Length);

        // 3. Find / Replace (phân biệt hoa/thường)
        if (!string.IsNullOrEmpty(find) && result.Contains(find))
            result = result.Replace(find, replace ?? "");

        return string.IsNullOrWhiteSpace(result) ? original : result;
    }

    private void ExecuteRename()
    {
        if (renameClips == null || renameClips.Count == 0) return;

        int success = 0, skip = 0, fail = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var clip in renameClips)
            {
                if (clip == null) { skip++; continue; }
                string newName = ApplyRenameRules(clip.name, removePrefix, removeSuffix, findText, replaceText);
                if (newName == clip.name) { skip++; continue; }

                string err = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(clip), newName);
                if (string.IsNullOrEmpty(err)) success++;
                else { Debug.LogWarning($"[Rename] '{clip.name}' → '{newName}': {err}"); fail++; }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog("Đổi tên hoàn tất",
            $"Thành công : {success} clip\n" +
            $"Bỏ qua     : {skip} clip\n" +
            $"Lỗi        : {fail} clip (xem Console)", "OK");
    }

    // =========================================================
    // TAB 3 — CẮT CLIP
    // =========================================================
    private void DrawTrimTab()
    {
        var so   = new SerializedObject(this);
        var prop = so.FindProperty("trimmerTab").FindPropertyRelative("trimClips");
        trimmerTab.Draw(so, prop);
    }

    // =========================================================
    // UTILITIES
    // =========================================================
    private void GetSplitHeights(out float scrollHeight, out float footerHeight)
    {
        const float toolbarReserved = 45f;
        float available = Mathf.Max(position.height - toolbarReserved, 200f);
        scrollHeight = available * 0.94f;
        footerHeight = available * 0.06f;
    }

    private static string BrowseFolder(string currentPath)
    {
        string chosen = EditorUtility.OpenFolderPanel("Chọn thư mục lưu", currentPath, "");
        if (!string.IsNullOrEmpty(chosen) && chosen.StartsWith(Application.dataPath))
            return "Assets" + chosen.Substring(Application.dataPath.Length);
        return currentPath;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts   = path.Split('/');
        string   current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif