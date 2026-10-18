// ============================================================
//  TrueInputSystem — TISCode_Tutorials.cs
//  Code Sub-Tab Module: 3 scene hướng dẫn tương tác (Tutorials/)
//  Mở scene theo đường dẫn — không phụ thuộc assembly Tutorials (có thể xoá thư mục đó).
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace GaMo.TrueInputSystem.Editor
{
    public class TISCode_Tutorials : ICodeSubTabModule
    {
        public string Label => "Scene hướng dẫn";

        const string Dir = "Assets/TrueInputSystem/Tutorials/Scenes/";

        static readonly (string file, string title, string desc)[] Scenes =
        {
            ("TIS_Tut_01_ChuotBanPhim", "Chuột + Bàn phím · 15 bài",
             "Di chuyển, camera, zoom, đọc nút & Read Mode, input buffer, tổ hợp phím, Handle, con trỏ & chọn vật, Action Map, override, SwapProfile, đổi phím."),
            ("TIS_Tut_02_TayCam", "Tay cầm · 12 bài",
             "Nhận diện thiết bị, cần analog & deadzone, cò như trục / nút, rung, aim assist, điều hướng menu, con trỏ ảo, gyro, touchpad, đổi nút."),
            ("TIS_Tut_03_Mobile", "Mobile · 11 bài",
             "TISMobileInputSetup, joystick ảo, vuốt camera, nút kỹ năng, hồi chiêu, kỹ năng định hướng MOBA, chạm chọn vật, cử chỉ nhiều ngón, tay cầm Bluetooth."),
        };

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Scene hướng dẫn tương tác",
                "3 scene học bằng cách chơi: mỗi bài có setup, code mẫu, bảng API chạy trực tiếp và nhiệm vụ \"Thử ngay\".");

            foreach (var s in Scenes)
            {
                win.H2(s.title);
                win.Body(s.desc);
                string path = Dir + s.file + ".unity";
                bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
                using (new EditorGUI.DisabledScope(!exists || EditorApplication.isPlaying))
                {
                    if (GUILayout.Button(exists ? "Mở scene  " + s.file : "Không tìm thấy — cài lại gói TIS_Tutorials",
                                         GUILayout.Height(28)))
                    {
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    }
                }
                GUILayout.Space(6);
            }

            win.Sep();
            win.H2("Cách dùng");
            win.Step(1, "Mở scene, nhấn Play", "Danh sách bài bên trái · Bài trước / Bài tiếp theo · PageUp / PageDown · tay cầm: VIEW + LB / RB.");
            win.Step(2, "Làm nhiệm vụ \"Thử ngay\"", "Nhiệm vụ tự tích khi bạn làm được; tiến độ được lưu (PlayerPrefs).");
            win.Step(3, "Chép code mẫu", "Tab Code mẫu ở cột phải có nút Sao chép. Script nhân vật / camera của sân chơi (TISTutPlayer, TISTutOrbitCamera) chỉ đọc qua TISInputBridge — chép thẳng vào game được.");

            win.Sep();
            win.H2("Thêm bài học mới");
            win.CodeBlock(
                "public class MyLesson : TISTutLesson\n" +
                "{\n" +
                "    public override string Title   => \"Bài của tôi\";\n" +
                "    public override string Summary => \"Bài này dạy...\";\n" +
                "    public override string[] Objectives => new[] { \"Nhảy 3 lần\" };\n\n" +
                "    int _jumps;\n\n" +
                "    protected override void OnEnter()\n" +
                "    {\n" +
                "        Player.CanJump = true;\n" +
                "        Watch(\"IsSkillDown(\\\"Jump\\\")\", () => F(TIS.IsSkillDown(\"Jump\")));\n" +
                "    }\n\n" +
                "    protected override void OnTick()\n" +
                "    {\n" +
                "        if (TIS.IsSkillDown(\"Jump\") && ++_jumps >= 3) Complete(0);\n" +
                "    }\n" +
                "}");
            win.Body("Thêm kiểu bài vào LessonsFor(mode) trong Tutorials/Editor/TISTutSceneBuilder.cs rồi dựng lại scene: thêm define TIS_TUTORIAL_DEV để hiện menu Tools › True Input System › Tutorials (xem Tutorials/README.md).");

            win.Note("Mobile trong Editor: TIS chỉ giả lập mobile trong Device Simulator; scene hướng dẫn Mobile có TISTutForceMobile để chạy cả trong Game view. Cử chỉ 2 ngón cần điện thoại thật (Unity Remote 5 / bản build).");
            win.Warn("Builder ghi đè scene và action map của profile hướng dẫn — sửa tay thì lưu bản sao trước khi dựng lại.");
            win.Tip("Toàn bộ thư mục Tutorials/ có thể xoá khi phát hành game — TIS không phụ thuộc vào nó.");
        }
    }
}
#endif
