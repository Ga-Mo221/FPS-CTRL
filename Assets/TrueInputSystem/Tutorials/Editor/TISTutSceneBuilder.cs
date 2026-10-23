// ============================================================
//  TrueInputSystem Tutorials — TISTutSceneBuilder.cs (Editor)
//
//  Dựng scene hướng dẫn bằng code (tái tạo được, không chỉnh tay):
//    Camera + ánh sáng + nền sân chơi · TISInputBridge + profile
//    EventSystem (InputSystemUIInputModule) · Canvas khung giao diện
//    TISTutController + các bài học (theo mode)
//
//  Scene được tạo ADDITIVE rồi lưu & đóng → scene bạn đang mở không bị đụng.
//  Chạy lại builder sẽ GHI ĐÈ scene hướng dẫn cùng tên.
// ============================================================

using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GaMo.TrueInputSystem.Tutorials.Editor
{
    public static class TISTutSceneBuilder
    {
        public const string ScenesDir = TISTutAssets.Root + "Scenes/";

        // Kích thước tham chiếu (CanvasScaler 1920×1080)
        const float M   = 28f;   // lề ngoài
        const float G   = 20f;   // khoảng cách cột
        const float TopH = 64f, FootH = 64f;
        const float LeftW = 330f, RightW = 440f;
        const float HeaderH = 150f;
        const float LiveH = 390f;

        static TISTutTheme _th;

        // ══════════════════════════════════════════════════════
        //  MENU
        // ══════════════════════════════════════════════════════
#if TIS_TUTORIAL_DEV   // menu chỉ dành cho người bảo trì bộ hướng dẫn

        [MenuItem("Tools/True Input System/Tutorials/Dựng scene — Chuột + Bàn phím", priority = 61)]
#endif
        static void MenuKbm()     => Build(TISTutMode.KeyboardMouse);
#if TIS_TUTORIAL_DEV   // menu chỉ dành cho người bảo trì bộ hướng dẫn

        [MenuItem("Tools/True Input System/Tutorials/Dựng scene — Tay cầm", priority = 62)]
#endif
        static void MenuGamepad() => Build(TISTutMode.Gamepad);
#if TIS_TUTORIAL_DEV   // menu chỉ dành cho người bảo trì bộ hướng dẫn

        [MenuItem("Tools/True Input System/Tutorials/Dựng scene — Mobile", priority = 63)]
#endif
        static void MenuMobile()  => Build(TISTutMode.Mobile);
#if TIS_TUTORIAL_DEV   // menu chỉ dành cho người bảo trì bộ hướng dẫn

        [MenuItem("Tools/True Input System/Tutorials/Dựng cả 3 scene", priority = 64)]
#endif
        public static void BuildAll()
        {
            Build(TISTutMode.KeyboardMouse);
            Build(TISTutMode.Gamepad);
            Build(TISTutMode.Mobile);
        }

        // ══════════════════════════════════════════════════════
        //  NỘI DUNG THEO MODE
        // ══════════════════════════════════════════════════════

        static Type[] LessonsFor(TISTutMode mode) => mode switch
        {
            TISTutMode.KeyboardMouse => new[]
            {
                typeof(TISTutLesson_Welcome),
                typeof(TISTutKbm_Move),
                typeof(TISTutKbm_Look),
                typeof(TISTutKbm_Zoom),
                typeof(TISTutKbm_Buttons),
                typeof(TISTutKbm_ReadMode),
                typeof(TISTutKbm_Buffer),
                typeof(TISTutKbm_Modifier),
                typeof(TISTutKbm_CustomActions),
                typeof(TISTutKbm_Pointer),
                typeof(TISTutKbm_ActionMaps),
                typeof(TISTutKbm_Override),
                typeof(TISTutKbm_SwapProfile),
                typeof(TISTutKbm_Rebind),
                typeof(TISTutKbm_MiniGame),
            },
            TISTutMode.Gamepad => new[]
            {
                typeof(TISTutLesson_Welcome),
                typeof(TISTutGp_Detect),
                typeof(TISTutGp_Sticks),
                typeof(TISTutGp_Triggers),
                typeof(TISTutGp_Rumble),
                typeof(TISTutGp_AimAssist),
                typeof(TISTutGp_UINav),
                typeof(TISTutGp_VirtualCursor),
                typeof(TISTutGp_Gyro),
                typeof(TISTutGp_Touchpad),
                typeof(TISTutGp_Rebind),
                typeof(TISTutGp_MiniGame),
            },
            TISTutMode.Mobile => new[]
            {
                typeof(TISTutLesson_Welcome),
                typeof(TISTutMb_Setup),
                typeof(TISTutMb_Joystick),
                typeof(TISTutMb_Camera),
                typeof(TISTutMb_SkillButtons),
                typeof(TISTutMb_Cooldown),
                typeof(TISTutMb_Directional),
                typeof(TISTutMb_Toucher),
                typeof(TISTutMb_Gestures),
                typeof(TISTutMb_Gamepad),
                typeof(TISTutMb_MiniGame),
            },
            _ => new[] { typeof(TISTutLesson_Welcome) },
        };

        /// <summary>Gán asset cho bài học cần tham chiếu (profile phụ...).</summary>
        static void ConfigureLesson(TISTutLesson lesson)
        {
            if (lesson is TISTutKbm_SwapProfile)
            {
                var so = new SerializedObject(lesson);
                so.FindProperty("_carProfile").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TISInputProfileConfig>(TISTutProfiles.KbmCarPath);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static TISInputProfileConfig ProfileFor(TISTutMode mode)
        {
            // Profile riêng của mode (Tutorials/Profiles/) — chưa có thì tạm dùng profile mẫu
            string[] candidates =
            {
                TISTutAssets.Root + "Profiles/TIS_Tut_" + mode + ".asset",
                "Assets/TrueInputSystem/Profiles/Character CTRL.asset",
            };
            foreach (var p in candidates)
            {
                var prof = AssetDatabase.LoadAssetAtPath<TISInputProfileConfig>(p);
                if (prof != null) return prof;
            }
            return null;
        }

        // ══════════════════════════════════════════════════════
        //  DỰNG SCENE
        // ══════════════════════════════════════════════════════

        public static string Build(TISTutMode mode)
        {
            _th = TISTutAssets.EnsureAll();
            TISTutProfiles.EnsureAll();
            TISTutTheme.Current = _th;
            TISTutTheme.Mode = mode;

            string path = ScenesDir + TISTutController.SceneNames[(int)mode] + ".unity";
            var prevActive = SceneManager.GetActiveScene();

            // Nếu scene hướng dẫn này đang mở → đóng trước để ghi đè
            bool single = false;
            var open = SceneManager.GetSceneByPath(path);
            if (open.IsValid() && open.isLoaded)
            {
                if (open.isDirty)
                {
                    Debug.LogWarning($"[TISTut] {path} đang mở và có thay đổi chưa lưu — lưu hoặc đóng scene rồi dựng lại.");
                    return null;
                }
                // Scene duy nhất đang mở (không có thay đổi chưa lưu): dựng thẳng vào scene mới thay thế nó
                if (SceneManager.sceneCount == 1) single = true;
                else EditorSceneManager.CloseScene(open, true);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, single ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                BuildWorld(mode, out var cam);
                var bridge = BuildBridge(mode, cam);
                BuildEventSystem();
                var view = BuildShell(mode, out var playground);
                cam.gameObject.AddComponent<TISTutViewportFitter>().Target = playground;
                BuildPlayground(cam);
                if (mode == TISTutMode.Gamepad) BuildGamepadExtras(cam, view);
                if (mode == TISTutMode.Mobile)  BuildMobileExtras(bridge, view);
                BuildController(mode, view);

                EditorSceneManager.SaveScene(scene, path);
                Debug.Log($"[TISTut] Đã dựng {path}");
            }
            finally
            {
                if (!single)
                {
                    if (prevActive.IsValid()) SceneManager.SetActiveScene(prevActive);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            return path;
        }

        // ── Thế giới 3D ──────────────────────────────────────

        static void BuildWorld(TISTutMode mode, out Camera cam)
        {
            RenderSettings.ambientMode         = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = TISTutTheme.Hex("#EAF4F7");
            RenderSettings.ambientEquatorColor = TISTutTheme.Hex("#DDEBD6");
            RenderSettings.ambientGroundColor  = TISTutTheme.Hex("#B9C9A8");
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _th.toySky;
            cam.fieldOfView = 50f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 7f, -10f), Quaternion.Euler(30f, 0f, 0f));
            camGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Sun Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = TISTutTheme.Hex("#FFF4E0");
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            var world = new GameObject("World").transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(world);
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial = TISTutAssets.Mat("Grass");

            // Vài đồ vật tươi màu cho sân chơi trống (bài học có thể ẩn để đặt đồ riêng)
            var toys = new GameObject("Toys").transform;
            toys.SetParent(world);
            Toy(toys, PrimitiveType.Cube,     "Toy Sun",   "Sun",   new Vector3(-4f, 0.75f, 3f),  1.5f);
            Toy(toys, PrimitiveType.Sphere,   "Toy Lilac", "Lilac", new Vector3(4.5f, 0.9f, 2.5f), 1.8f);
            Toy(toys, PrimitiveType.Cylinder, "Toy Mint",  "Mint",  new Vector3(3f, 0.6f, 7f),    1.2f);
        }

        // ── Riêng scene Tay cầm: target scanner + con trỏ ảo ─

        static void BuildGamepadExtras(Camera cam, TISTutShellView view)
        {
            // Target scanner cho bài Aim assist (tắt sẵn — bài học tự bật)
            var scanner = cam.gameObject.AddComponent<TISTargetScanner>();
            var sso = new SerializedObject(scanner);
            var tags = sso.FindProperty("_targetTags");
            tags.arraySize = 1;
            tags.GetArrayElementAtIndex(0).stringValue = "Finish";
            sso.FindProperty("_requireLineOfSight").boolValue = false;
            sso.FindProperty("_scanAngle").floatValue = 12f;
            sso.FindProperty("_maxDistance").floatValue = 40f;
            sso.FindProperty("_aimPointMode").enumValueIndex = 0;
            sso.ApplyModifiedPropertiesWithoutUndo();
            scanner.enabled = false;

            // Con trỏ ảo (bài Con trỏ ảo bật bằng SetActiveGamepadCursor)
            var canvas = view.GetComponent<Canvas>();
            var cursorGo = new GameObject("Gamepad Virtual Cursor", typeof(RectTransform));
            cursorGo.transform.SetParent(canvas.transform, false);
            var dot = TISTutUI.Shape(cursorGo.transform, "Cursor", _th.ink, 14f, 3f, _th.Accent());
            dot.raycastTarget = false;                       // không được chặn click của chính nó
            var drt = dot.rectTransform;
            drt.sizeDelta = new Vector2(28f, 28f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            var vc = cursorGo.AddComponent<TISGamepadVirtualCursor>();
            var vso = new SerializedObject(vc);
            vso.FindProperty("cursorTransform").objectReferenceValue = drt;
            vso.FindProperty("parentCanvas").objectReferenceValue = canvas;
            vso.ApplyModifiedPropertiesWithoutUndo();
            cursorGo.transform.SetAsLastSibling();
        }

        // ── Riêng scene Mobile: TISMobileInputSetup + HUD nút cảm ứng ─

        static void BuildMobileExtras(TISInputBridge bridge, TISTutShellView view)
        {
            var circle = TISTutAssets.Circle;

            // Chạy chế độ mobile cả trong Game view (mặc định TIS chỉ bật trong Device Simulator)
            new GameObject("TIS Tut Force Mobile (Editor)").AddComponent<TISTutForceMobile>();

            // Joystick ảo + vùng xoay camera + nhận chạm toàn màn hình (TIS tự dựng lúc chạy)
            var setupGo = new GameObject("TIS Mobile Input Setup");
            var setup = setupGo.AddComponent<TISMobileInputSetup>();
            var so = new SerializedObject(setup);
            so.FindProperty("_inputBridge").objectReferenceValue = bridge;
            so.FindProperty("_canvasSortOrder").intValue = -1;            // dưới khung hướng dẫn
            so.FindProperty("_joystickBGDiameter").floatValue = 230f;
            so.FindProperty("_knobSizeRatio").floatValue = 0.45f;
            so.FindProperty("_spriteNen").objectReferenceValue = circle;
            so.FindProperty("_spriteKnob").objectReferenceValue = circle;
            so.FindProperty("_mauNen").colorValue = new Color(_th.ink.r, _th.ink.g, _th.ink.b, 0.38f);
            so.FindProperty("_idleOpacityOverride").floatValue = 0.6f;     // lúc thả tay vẫn thấy joystick
            so.FindProperty("_mauKnobHienThi").colorValue = _th.mobileAccentStrong;
            so.ApplyModifiedPropertiesWithoutUndo();

            // HUD nút (con của Lesson Overlay — nằm trong sân chơi)
            var hudRt = TISTutUI.Rect(view.overlayRoot, "Mobile HUD");
            TISTutUI.Stretch(hudRt);
            var hud = hudRt.gameObject.AddComponent<TISTutMobileHud>();

            hud.jump   = SkillButton(hudRt, "Jump",   "NHẢY",     new Vector2(-150f, 160f), 136f, circle);
            hud.attack = SkillButton(hudRt, "Attack", "ĐÁNH",     new Vector2(-305f, 110f), 112f, circle);
            hud.dash   = SkillButton(hudRt, "Dash",   "LƯỚT",     new Vector2(-120f, 320f), 104f, circle);

            // Vùng huỷ kỹ năng (giữa phía trên sân chơi — script tự bật khi đang kéo)
            var cancel = TISTutUI.Rect(hudRt, "Cancel Zone");
            TISTutUI.At(cancel, new Vector2(1f, 1f), new Vector2(-60f, -40f), new Vector2(130f, 130f));
            var cancelImg = cancel.gameObject.AddComponent<UnityEngine.UI.Image>();
            cancelImg.sprite = circle;
            cancelImg.color = new Color(1f, 1f, 1f, 0.6f);
            var cx = Txt(cancel, "X", "✕", _th.monoBold, 46, _th.ink, TextAlignmentOptions.Center);
            Stretch(cx.rectTransform);
            cx.rectTransform.offsetMin = new Vector2(0f, 18f);
            var cl = Txt(cancel, "Label", "HUỶ", _th.monoBold, 15, _th.ink, TextAlignmentOptions.Bottom);
            Stretch(cl.rectTransform);
            cl.rectTransform.offsetMin = new Vector2(0f, 16f);
            cancel.gameObject.SetActive(false);

            var fbRt = TISTutUI.Rect(hudRt, "Fireball");
            TISTutUI.At(fbRt, new Vector2(1f, 0f), new Vector2(-170f, 190f), new Vector2(140f, 140f));
            fbRt.pivot = new Vector2(0.5f, 0.5f);
            var fbImg = fbRt.gameObject.AddComponent<UnityEngine.UI.Image>();
            fbImg.sprite = circle;
            var fbLabel = Txt(fbRt, "Label", "CẦU\nLỬA", _th.monoBold, 20, _th.ink, TextAlignmentOptions.Center);
            Stretch(fbLabel.rectTransform);
            var fb = fbRt.gameObject.AddComponent<TISDirectionalSkillButton>();
            var fso = new SerializedObject(fb);
            fso.FindProperty("_skillName").stringValue = "Fireball";
            fso.FindProperty("_directionActionName").stringValue = "Fireball_Dir";
            fso.FindProperty("_maxDragRadiusPx").floatValue = 150f;
            fso.FindProperty("_normalColor").colorValue = new Color(1f, 1f, 1f, 0.92f);
            fso.FindProperty("_draggingColor").colorValue = _th.mobileAccent;
            fso.FindProperty("_castColor").colorValue = _th.mobileAccentStrong;
            fso.FindProperty("_cancelZone").objectReferenceValue = cancel;
            fso.FindProperty("_cancelZoneImage").objectReferenceValue = cancelImg;
            fso.FindProperty("_cancelZoneActiveColor").colorValue = _th.toyCoral;
            fso.ApplyModifiedPropertiesWithoutUndo();
            hud.fireball = fb;

            hud.jump.gameObject.SetActive(false);
            hud.attack.gameObject.SetActive(false);
            hud.dash.gameObject.SetActive(false);
            fbRt.gameObject.SetActive(false);
        }

        static TISMobileSkillButton SkillButton(RectTransform parent, string skill, string label, Vector2 pos, float size, Sprite circle)
        {
            var rt = TISTutUI.Rect(parent, "Btn " + skill);
            TISTutUI.At(rt, new Vector2(1f, 0f), pos, new Vector2(size, size));
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.sprite = circle;

            // Vòng hồi chiêu (Image Filled Radial phủ lên nút)
            var cdRt = TISTutUI.Rect(rt, "Cooldown");
            Stretch(cdRt);
            var cd = cdRt.gameObject.AddComponent<UnityEngine.UI.Image>();
            cd.sprite = circle;
            cd.type = UnityEngine.UI.Image.Type.Filled;
            cd.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            cd.fillOrigin = 2;
            cd.fillAmount = 0f;
            cd.color = new Color(_th.ink.r, _th.ink.g, _th.ink.b, 0.55f);
            cd.raycastTarget = false;

            var t = Txt(rt, "Label", label, _th.monoBold, size > 120f ? 24 : 20, _th.ink, TextAlignmentOptions.Center);
            t.characterSpacing = 3f;
            Stretch(t.rectTransform);

            var b = rt.gameObject.AddComponent<TISMobileSkillButton>();
            var so = new SerializedObject(b);
            so.FindProperty("_skillName").stringValue = skill;
            so.FindProperty("_normalColor").colorValue = new Color(1f, 1f, 1f, 0.92f);
            so.FindProperty("_pressedColor").colorValue = _th.mobileAccentStrong;
            so.FindProperty("_disabledColor").colorValue = new Color(0.75f, 0.75f, 0.72f, 0.85f);
            so.FindProperty("_cooldownFill").objectReferenceValue = cd;
            so.ApplyModifiedPropertiesWithoutUndo();
            return b;
        }

        // ── Nhân vật + camera quỹ đạo + TISTutPlayground ─────

        static void BuildPlayground(Camera cam)
        {
            // Chỉ tìm trong scene đang dựng (scene người dùng đang mở cũng có thể có "World")
            Transform world = null;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "World") { world = root.transform; break; }

            var player = new GameObject("Player");
            player.transform.position = TISTutPlayground.PlayerStart;
            var cc = player.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.5f; cc.center = Vector3.zero;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(player.transform, false);
            visual.GetComponent<Renderer>().sharedMaterial = TISTutAssets.Mat("Coral");
            // "Kính" để thấy nhân vật đang quay về hướng nào
            var eye = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eye.name = "Visor";
            UnityEngine.Object.DestroyImmediate(eye.GetComponent<Collider>());
            eye.transform.SetParent(visual.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0.45f, 0.42f);
            eye.transform.localScale = new Vector3(0.7f, 0.22f, 0.25f);
            eye.GetComponent<Renderer>().sharedMaterial = TISTutAssets.Mat("Ink");

            var pl = player.AddComponent<TISTutPlayer>();
            var pso = new SerializedObject(pl);
            pso.FindProperty("_visual").objectReferenceValue = visual.transform;
            pso.ApplyModifiedPropertiesWithoutUndo();

            var orbit = cam.gameObject.AddComponent<TISTutOrbitCamera>();
            orbit.target = player.transform;

            var pgGo = new GameObject("Playground");
            var pg = pgGo.AddComponent<TISTutPlayground>();
            var so = new SerializedObject(pg);
            so.FindProperty("_player").objectReferenceValue = pl;
            so.FindProperty("_camera").objectReferenceValue = orbit;
            so.FindProperty("_toys").objectReferenceValue   = world.Find("Toys");
            so.ApplyModifiedPropertiesWithoutUndo();
            pg.grass = TISTutAssets.Mat("Grass");
            pg.coral = TISTutAssets.Mat("Coral");
            pg.sun   = TISTutAssets.Mat("Sun");
            pg.lilac = TISTutAssets.Mat("Lilac");
            pg.mint  = TISTutAssets.Mat("Mint");
            pg.ink   = TISTutAssets.Mat("Ink");
            pg.paper = TISTutAssets.Mat("Paper");
        }

        static GameObject Toy(Transform parent, PrimitiveType type, string name, string mat, Vector3 pos, float scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * scale;
            go.GetComponent<Renderer>().sharedMaterial = TISTutAssets.Mat(mat);
            return go;
        }

        // ── Input ─────────────────────────────────────────────

        static TISInputBridge BuildBridge(TISTutMode mode, Camera cam)
        {
            var go = new GameObject("TIS Input Bridge");
            var bridge = go.AddComponent<TISInputBridge>();
            var so = new SerializedObject(bridge);
            so.FindProperty("_profile").objectReferenceValue = ProfileFor(mode);
            so.FindProperty("_simulateMobileInEditor").boolValue = mode == TISTutMode.Mobile;
            so.FindProperty("_trackerCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();
            return bridge;
        }

        static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // ── Controller + bài học ──────────────────────────────

        static void BuildController(TISTutMode mode, TISTutShellView view)
        {
            var go = new GameObject("TIS Tutorial");
            var ctrl = go.AddComponent<TISTutController>();
            var lessons = new GameObject("Lessons").transform;
            lessons.SetParent(go.transform);

            foreach (var t in LessonsFor(mode))
            {
                var lg = new GameObject(t.Name.Replace("TISTutLesson_", "Bài · ").Replace("TISTutKbm_", "Bài · ").Replace("TISTutGp_", "Bài · ").Replace("TISTutMb_", "Bài · "));
                lg.transform.SetParent(lessons);
                ConfigureLesson((TISTutLesson)lg.AddComponent(t));
            }

            var so = new SerializedObject(ctrl);
            so.FindProperty("_theme").objectReferenceValue      = _th;
            so.FindProperty("_mode").enumValueIndex             = (int)mode;
            so.FindProperty("_view").objectReferenceValue       = view;
            so.FindProperty("_lessonRoot").objectReferenceValue = lessons;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ══════════════════════════════════════════════════════
        //  KHUNG GIAO DIỆN
        // ══════════════════════════════════════════════════════

        static TISTutShellView BuildShell(TISTutMode mode, out RectTransform playground)
        {
            var canvasGo = new GameObject("Tutorial UI", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var view = canvasGo.AddComponent<TISTutShellView>();
            var root = canvasGo.transform;

            // Vùng chính (giữa thanh trên và chân trang)
            float top = M + TopH + G, bottom = M + FootH + G;

            // Sân chơi = cửa sổ camera
            playground = Rect(root, "Playground");
            Place(playground, M + LeftW + G, top + HeaderH + G, M + RightW + G, bottom);

            // Nền kem có lỗ cho sân chơi — đặt dưới cùng
            var bg = Rect(root, "Background");
            Stretch(bg);
            var hole = bg.gameObject.AddComponent<TISTutHoleFrame>();
            hole.color = _th.paper;
            hole.Hole = playground;
            bg.SetAsFirstSibling();

            BuildTopBar(root, view, mode);
            BuildLessonList(root, view, top, bottom);
            BuildHeader(root, view, top);
            BuildPlayground(playground, view);
            BuildLivePanel(root, view, top);
            BuildInfoPanel(root, view, top, bottom);
            BuildFooter(root, view);

            view.playground = playground;
            return view;
        }

        // ── Thanh trên ───────────────────────────────────────

        static void BuildTopBar(Transform root, TISTutShellView view, TISTutMode mode)
        {
            var bar = Rect(root, "Top Bar");
            Place(bar, M, M, M, -1f, height: TopH);

            // Logo: "TIS" + chip "HỌC NHANH"
            var logo = Txt(bar, "Logo", "TIS", _th.monoBold, 34, _th.ink, TextAlignmentOptions.MidlineLeft);
            Place(logo.rectTransform, 0f, 0f, -1f, 0f, width: 90f);
            var chip = Shape(bar, "Logo Chip", _th.Accent(), 10f);
            Place(chip.rectTransform, 84f, 12f, -1f, 12f, width: 190f);
            var chipT = Txt(chip.transform, "Text", "HỌC NHANH", _th.monoBold, 20, _th.ink, TextAlignmentOptions.Center);
            chipT.characterSpacing = 6f;
            Stretch(chipT.rectTransform);

            var sub = Txt(bar, "Subtitle", "Hướng dẫn True Input System", _th.body, 20, _th.inkSoft, TextAlignmentOptions.MidlineLeft);
            Place(sub.rectTransform, 300f, 0f, -1f, 0f, width: 420f);

            // Chip thiết bị đang dùng (phải cùng)
            var dev = Shape(bar, "Device Chip", _th.Accent(), 32f);
            Place(dev.rectTransform, -1f, 6f, 0f, 6f, width: 340f);
            var devT = Txt(dev.transform, "Text", "● Đang dùng: —", _th.bodyBold, 19, _th.ink, TextAlignmentOptions.Center);
            Stretch(devT.rectTransform);
            view.deviceChip = dev;
            view.deviceChipText = devT;

            // 3 tab mode
            float x = 340f + 18f;
            view.tabMobile        = TabButton(bar, "Tab Mobile",   "MOBILE",       ref x, 170f);
            view.tabGamepad       = TabButton(bar, "Tab Gamepad",  "TAY CẦM",      ref x, 190f);
            view.tabKeyboardMouse = TabButton(bar, "Tab KBM",      "CHUỘT + PHÍM", ref x, 250f);
            view.tabKeyboardMouse.IsOn = mode == TISTutMode.KeyboardMouse;
            view.tabGamepad.IsOn       = mode == TISTutMode.Gamepad;
            view.tabMobile.IsOn        = mode == TISTutMode.Mobile;
        }

        static TISTutButton TabButton(RectTransform bar, string name, string label, ref float rightX, float w)
        {
            var b = MakeButton(bar, name, label, TISTutButton.Style.Ghost, w, 52f, capIcon: null, capLeft: false, fontSize: 19);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-rightX, 0f);
            rightX += w + 10f;
            return b;
        }

        // ── Cột trái: danh sách bài ──────────────────────────

        static void BuildLessonList(Transform root, TISTutShellView view, float top, float bottom)
        {
            var card = Shape(root, "Lesson Card", _th.card, 22f);
            Place(card.rectTransform, M, top, -1f, bottom, width: LeftW);

            var counter = Txt(card.transform, "Counter", "BÀI HỌC · 1/1", _th.monoBold, 17, _th.inkSoft, TextAlignmentOptions.MidlineLeft);
            counter.characterSpacing = 4f;
            Place(counter.rectTransform, 22f, 18f, 22f, -1f, height: 30f);
            view.lessonCounter = counter;

            var list = Rect(card.transform, "List");
            Place(list, 14f, 58f, 14f, 14f);
            var vl = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 5f;
            vl.childControlWidth = true; vl.childControlHeight = false;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            view.lessonListRoot = list;

            // Mẫu dòng bài học
            var btn = MakeButton(list, "Lesson Item Template", "00  Tên bài", TISTutButton.Style.Ghost, LeftW - 28f, 46f,
                                 capIcon: null, capLeft: false, fontSize: 18, slant: 0f, labelLeft: 50f, mono: false);
            // Tên bài dài → thu nhỏ chữ rồi cắt "…" thay vì tràn ra ngoài
            var lt = btn.Label;
            lt.enableAutoSizing = true;
            lt.fontSizeMin = 14f; lt.fontSizeMax = 18f;
            lt.overflowMode = TextOverflowModes.Ellipsis;
            lt.rectTransform.offsetMax = new Vector2(-12f, 0f);
            var icon = Txt(btn.transform, "Icon", "○", _th.monoBold, 20, _th.inkMuted, TextAlignmentOptions.Center);
            Place(icon.rectTransform, 14f, 0f, -1f, 0f, width: 28f);
            var item = btn.gameObject.AddComponent<TISTutLessonItem>();
            SetRef(item, "_button", btn);
            SetRef(item, "_icon", icon);
            view.lessonItemTemplate = item;
        }

        // ── Đầu cột giữa: tiêu đề + mô tả bài ───────────────

        static void BuildHeader(Transform root, TISTutShellView view, float top)
        {
            var card = Shape(root, "Lesson Header", _th.card, 22f);
            Place(card.rectTransform, M + LeftW + G, top, M + RightW + G, -1f, height: HeaderH);

            var title = Txt(card.transform, "Title", "Tên bài học", _th.bodyBold, 36, _th.ink, TextAlignmentOptions.TopLeft);
            Place(title.rectTransform, 30f, 20f, 30f, -1f, height: 50f);
            view.lessonTitle = title;

            var sum = Txt(card.transform, "Summary", "Mô tả bài học", _th.body, 19, _th.inkSoft, TextAlignmentOptions.TopLeft, wrap: true);
            sum.lineSpacing = 6f;
            Place(sum.rectTransform, 30f, 74f, 30f, 14f);
            view.lessonSummary = sum;
        }

        // ── Sân chơi: nhiệm vụ, tiến độ, toast ───────────────

        static void BuildPlayground(RectTransform pg, TISTutShellView view)
        {
            var overlay = Rect(pg, "Lesson Overlay");
            Stretch(overlay);
            view.overlayRoot = overlay;

            // Card nhiệm vụ "Thử ngay"
            var obj = Shape(pg, "Objectives", _th.card, 18f);
            var ort = obj.rectTransform;
            ort.anchorMin = ort.anchorMax = new Vector2(0f, 1f);
            ort.pivot = new Vector2(0f, 1f);
            ort.anchoredPosition = new Vector2(18f, -18f);
            ort.sizeDelta = new Vector2(660f, 100f);
            var vl = obj.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(18, 18, 16, 16);
            vl.spacing = 8f;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            var fit = obj.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var badgeRow = Rect(obj.transform, "Badge Row");
            badgeRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            var badge = Shape(badgeRow, "Badge", _th.Accent(), 8f);
            Place(badge.rectTransform, 0f, 0f, -1f, 0f, width: 140f);
            var badgeT = Txt(badge.transform, "Text", "THỬ NGAY", _th.monoBold, 17, _th.ink, TextAlignmentOptions.Center);
            badgeT.characterSpacing = 5f;
            Stretch(badgeT.rectTransform);
            view.tryBadge = badgeT;

            var row = Rect(obj.transform, "Objective Template");
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            var check = Shape(row, "Check", _th.card, 8f, border: 2f);
            var ckrt = check.rectTransform;                     // pivot giữa → hiệu ứng "bật" nảy đều
            ckrt.anchorMin = ckrt.anchorMax = new Vector2(0f, 0.5f);
            ckrt.pivot = new Vector2(0.5f, 0.5f);
            ckrt.anchoredPosition = new Vector2(14f, 0f);
            ckrt.sizeDelta = new Vector2(28f, 28f);
            var checkIcon = Txt(check.transform, "Icon", "", _th.monoBold, 18, _th.ink, TextAlignmentOptions.Center);
            Stretch(checkIcon.rectTransform);
            var rowText = Txt(row, "Text", "Nhiệm vụ", _th.body, 19, _th.ink, TextAlignmentOptions.MidlineLeft);
            rowText.enableAutoSizing = true;              // nhiệm vụ dài → thu nhỏ chữ, không tràn card
            rowText.fontSizeMin = 14f; rowText.fontSizeMax = 19f;
            rowText.overflowMode = TextOverflowModes.Ellipsis;
            Place(rowText.rectTransform, 42f, 0f, 0f, 0f);
            var orow = row.gameObject.AddComponent<TISTutObjectiveRow>();
            SetRef(orow, "_check", check);
            SetRef(orow, "_checkIcon", checkIcon);
            SetRef(orow, "_text", rowText);
            view.objectiveRoot = (RectTransform)obj.transform;
            view.objectiveTemplate = orow;

            // Chip tiến độ (dưới-phải)
            var prog = Shape(pg, "Progress", _th.ink, 12f);
            var prt = prog.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 0f);
            prt.anchoredPosition = new Vector2(-18f, 18f);
            prt.sizeDelta = new Vector2(200f, 46f);
            var progT = Txt(prog.transform, "Text", "TIẾN ĐỘ 0/0", _th.monoBold, 18, _th.Accent(), TextAlignmentOptions.Center);
            progT.characterSpacing = 3f;
            Stretch(progT.rectTransform);
            view.progressChip = prog;
            view.progressText = progT;

            // Toast (trên-giữa)
            var toast = Shape(pg, "Toast", _th.ink, 26f);
            var trt = toast.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -24f);
            trt.sizeDelta = new Vector2(520f, 54f);
            var tg = toast.gameObject.AddComponent<CanvasGroup>();
            tg.alpha = 0f; tg.blocksRaycasts = false; tg.interactable = false;
            var toastT = Txt(toast.transform, "Text", "", _th.bodyBold, 20, _th.paper, TextAlignmentOptions.Center);
            Stretch(toastT.rectTransform);
            view.toastGroup = tg;
            view.toastText = toastT;
        }

        // ── Cột phải: API đang chạy ──────────────────────────

        static void BuildLivePanel(Transform root, TISTutShellView view, float top)
        {
            var card = Shape(root, "Live API Card", _th.card, 22f);
            Place(card.rectTransform, -1f, top, M, -1f, width: RightW, height: LiveH);

            var head = Txt(card.transform, "Header", "API ĐANG CHẠY", _th.monoBold, 17, _th.inkSoft, TextAlignmentOptions.MidlineLeft);
            head.characterSpacing = 4f;
            Place(head.rectTransform, 24f, 18f, 24f, -1f, height: 30f);

            var dot = Shape(card.transform, "Live Dot", _th.AccentStrong(), 7f);
            Place(dot.rectTransform, -1f, 27f, 24f, -1f, width: 14f, height: 14f);

            var list = Rect(card.transform, "Rows");
            Place(list, 24f, 60f, 24f, 16f);
            var vl = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 6f;
            vl.childControlWidth = true; vl.childControlHeight = false;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            view.liveRoot = list;

            var empty = Txt(card.transform, "Empty", "Bài này chưa theo dõi API nào.", _th.body, 18, _th.inkMuted, TextAlignmentOptions.TopLeft);
            Place(empty.rectTransform, 24f, 64f, 24f, -1f, height: 30f);
            view.liveEmptyText = empty;

            // Mẫu dòng
            var row = Rect(list, "Live Row Template");
            row.sizeDelta = new Vector2(0f, 48f);
            var n = Txt(row, "Name", "API", _th.mono, 17, _th.ink, TextAlignmentOptions.TopLeft);
            Place(n.rectTransform, 0f, 2f, -1f, -1f, width: 230f, height: 26f);
            n.enableAutoSizing = true;  n.fontSizeMin = 11f; n.fontSizeMax = 17f;   // tên API dài → thu nhỏ, không đè giá trị
            n.overflowMode = TextOverflowModes.Ellipsis;
            var v = Txt(row, "Value", "—", _th.monoBold, 17, _th.ink, TextAlignmentOptions.TopRight);
            Place(v.rectTransform, -1f, 2f, 0f, -1f, width: 158f, height: 26f);
            v.enableAutoSizing = true;  v.fontSizeMin = 11f; v.fontSizeMax = 17f;
            var barRoot = Rect(row, "Bar");
            Place(barRoot, 0f, -1f, 0f, 6f, height: 8f);
            var track = Shape(barRoot, "Track", _th.track, 4f);
            Stretch(track.rectTransform);
            var fill = Shape(barRoot, "Fill", _th.AccentStrong(), 4f);
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0.5f, 1f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            var lr = row.gameObject.AddComponent<TISTutLiveRow>();
            SetRef(lr, "_name", n);
            SetRef(lr, "_value", v);
            SetRef(lr, "_barRoot", barRoot);
            SetRef(lr, "_barTrack", track);
            SetRef(lr, "_barFill", fill);
            view.liveRowTemplate = lr;
        }

        // ── Cột phải: Cách setup / Code mẫu ──────────────────

        static void BuildInfoPanel(Transform root, TISTutShellView view, float top, float bottom)
        {
            var card = Shape(root, "Info Card", _th.card, 22f);
            Place(card.rectTransform, -1f, top + LiveH + G, M, bottom, width: RightW);

            var tabs = Rect(card.transform, "Tabs");
            Place(tabs, 18f, 18f, 18f, -1f, height: 46f);
            view.setupTab = MakeButton(tabs, "Tab Setup", "CÁCH SETUP", TISTutButton.Style.Ghost, 190f, 46f, null, false, 17);
            AnchorLeft((RectTransform)view.setupTab.transform, 0f);
            view.codeTab = MakeButton(tabs, "Tab Code", "CODE MẪU", TISTutButton.Style.Ghost, 170f, 46f, null, false, 17);
            AnchorLeft((RectTransform)view.codeTab.transform, 200f);

            // Trang setup
            var setup = Rect(card.transform, "Setup Page");
            Place(setup, 24f, 80f, 24f, 20f);
            var st = Txt(setup, "Text", "Các bước setup", _th.body, 18, _th.ink, TextAlignmentOptions.TopLeft, wrap: true);
            st.lineSpacing = 4f;
            st.enableAutoSizing = true; st.fontSizeMin = 12f; st.fontSizeMax = 18f;   // nhiều bước → thu nhỏ cho vừa thẻ
            Stretch(st.rectTransform);
            view.setupPage = setup.gameObject;
            view.setupText = st;

            // Trang code (nền than chì)
            var code = Shape(card.transform, "Code Page", _th.ink, 16f);
            Place(code.rectTransform, 14f, 76f, 14f, 14f);
            var ct = Txt(code.transform, "Code", "// code", _th.mono, 14, _th.codeText, TextAlignmentOptions.TopLeft, wrap: true);
            ct.lineSpacing = 2f;
            ct.overflowMode = TextOverflowModes.Ellipsis;
            Place(ct.rectTransform, 18f, 56f, 18f, 14f);
            var copy = MakeButton(code.transform, "Copy Button", "SAO CHÉP", TISTutButton.Style.Accent, 150f, 38f, null, false, 15, slant: 6f);
            var crt = (RectTransform)copy.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-12f, -10f);
            var lbl = Txt(code.transform, "Label", "// Code mẫu", _th.mono, 15, _th.Accent(), TextAlignmentOptions.MidlineLeft);
            Place(lbl.rectTransform, 18f, 10f, -1f, -1f, width: 200f, height: 38f);
            view.codePage = code.gameObject;
            view.codeText = ct;
            view.copyButton = copy;
        }

        // ── Chân trang ────────────────────────────────────────

        static void BuildFooter(Transform root, TISTutShellView view)
        {
            var bar = Rect(root, "Footer");
            Place(bar, M, -1f, M, M, height: FootH);

            view.prevButton = MakeButton(bar, "Prev", "BÀI TRƯỚC", TISTutButton.Style.Accent, 290f, FootH, "←", capLeft: true, 20);
            AnchorLeft((RectTransform)view.prevButton.transform, 0f);

            view.nextButton = MakeButton(bar, "Next", "BÀI TIẾP THEO", TISTutButton.Style.Dark, 360f, FootH, "→", capLeft: false, 20);
            var nrt = (RectTransform)view.nextButton.transform;
            nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(1f, 0.5f);
            nrt.anchoredPosition = Vector2.zero;

            var hint = Txt(bar, "Hint", "", _th.body, 18, _th.inkSoft, TextAlignmentOptions.Center);
            Place(hint.rectTransform, 320f, 0f, 390f, 0f);
            view.footerHint = hint;
        }

        // ══════════════════════════════════════════════════════
        //  TIỆN ÍCH UI (chuyển tiếp sang TISTutUI — dùng chung với runtime)
        // ══════════════════════════════════════════════════════

        static TISTutButton MakeButton(Transform parent, string name, string label, TISTutButton.Style style,
                                       float w, float h, string capIcon, bool capLeft, int fontSize,
                                       float slant = 10f, float labelLeft = -1f, bool mono = true)
            => TISTutUI.Button(parent, name, label, style, w, h, capIcon, capLeft, fontSize, slant, labelLeft, mono);

        static RectTransform Rect(Transform parent, string name) => TISTutUI.Rect(parent, name);

        static TISTutShape Shape(Transform parent, string name, Color c, float radius, float border = 0f)
            => TISTutUI.Shape(parent, name, c, radius, border);

        static TextMeshProUGUI Txt(Transform parent, string name, string text, TMP_FontAsset font, float size,
                                   Color color, TextAlignmentOptions align, bool wrap = false)
            => TISTutUI.Text(parent, name, text, font, size, color, align, wrap);

        static void Stretch(RectTransform rt) => TISTutUI.Stretch(rt);
        static void AnchorLeft(RectTransform rt, float x) => TISTutUI.AnchorLeft(rt, x);

        static void Place(RectTransform rt, float left, float top, float right, float bottom,
                          float width = 0f, float height = 0f)
            => TISTutUI.Place(rt, left, top, right, bottom, width, height);

        static void SetRef(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[TISTut] {target.GetType().Name} không có field {field}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
