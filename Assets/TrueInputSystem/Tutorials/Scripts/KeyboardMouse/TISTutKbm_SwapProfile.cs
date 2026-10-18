// ============================================================
//  TrueInputSystem Tutorials — Chuột + Bàn phím · SwapProfile (đi bộ ↔ lái xe)
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/KBM · Đổi profile")]
    public class TISTutKbm_SwapProfile : TISTutLesson
    {
        [SerializeField, Tooltip("Profile lái xe (TIS_Tut_KeyboardMouse_Car).")]
        TISInputProfileConfig _carProfile;

        public override string Title => "Đổi profile: đi bộ ↔ lái xe";

        public override string Summary =>
            "SwapProfile thay cả bộ điều khiển chỉ bằng một dòng — action map, binding, độ nhạy đều đổi theo. Hợp cho lên xe, mở bản đồ chiến thuật, đổi nhân vật. Sự kiện OnProfileSwapped báo khi đổi xong để cập nhật UI gợi ý phím.";

        public override string[] SetupSteps => new[]
        {
            "Tạo 2 profile: <b>TIS_Tut_KeyboardMouse</b> (đi bộ: Jump, Run, Interact...) và <b>TIS_Tut_KeyboardMouse_Car</b> (lái xe: Boost, Horn, ExitCar).",
            "Profile gốc gán trên bridge luôn đọc lại được qua <b>OriginalProfile</b>; profile đang chạy là <b>ActiveProfile</b>.",
            "Phím người chơi đã đổi (Rebinding) được lưu riêng cho từng profile và tự áp lại khi swap.",
        };

        public override string Code =>
@"[SerializeField] TISInputProfileConfig car;

void EnterCar() => tis.SwapProfile(car);
void ExitCar()  =>
    tis.SwapProfile(tis.OriginalProfile);

void OnEnable() =>
    tis.OnProfileSwapped += OnSwapped;

void OnSwapped(TISInputProfileConfig p)
    => hint.text = p.profileName;

// Trong xe: action của profile lái xe
if (tis.IsSkillDown(""Horn"")) Honk();";

        public override string[] Objectives => new[]
        {
            "Đi tới chiếc xe và nhấn F để lên xe",
            "Bấm còi (H)",
            "Giữ Shift để tăng tốc",
            "Nhấn F để xuống xe",
        };

        public override string FooterHint => "F lên / xuống xe · trong xe: WASD lái, Shift tăng tốc, H bấm còi";

        bool _inCar;
        GameObject _parked;
        GameObject _riding;
        float _hop;
        string _lastSwap = "—";

        protected override void OnEnter()
        {
            _inCar = false; _hop = 0f; _lastSwap = "—";
            PG.Toys.gameObject.SetActive(false);
            Player.CanJump = true;
            Cam.LookEnabled = true;

            _parked = BuildCar(Stage, new Vector3(4.5f, 0f, 4f));
            TIS.OnProfileSwapped += OnSwapped;

            WasdCluster(new Vector2(0f, 0f), new Vector2(20f, 20f));
            KeyRow(new Vector2(0f, 0f), new Vector2(230f, 20f),
                   ("F", "<Keyboard>/f", 64f), ("H", "<Keyboard>/h", 64f), ("SHIFT", "<Keyboard>/leftShift", 110f));

            Watch("ActiveProfile", () => TIS.ActiveProfile != null ? TIS.ActiveProfile.profileName : "—");
            Watch("OriginalProfile", () => TIS.OriginalProfile != null ? TIS.OriginalProfile.profileName : "—");
            Watch("OnProfileSwapped", () => _lastSwap);
            Watch("IsSkillHeld(\"Boost\")", () => _inCar ? F(TIS.IsSkillHeld("Boost")) : "— (chỉ có trong xe)");
        }

        void OnSwapped(TISInputProfileConfig p) => _lastSwap = p.profileName;

        GameObject BuildCar(Transform parent, Vector3 at)
        {
            var car = new GameObject("Car");
            car.transform.SetParent(parent, false);
            car.transform.position = at;
            PG.Spawn(PrimitiveType.Cube, "Body", PG.coral, at + new Vector3(0f, 0.75f, 0f), new Vector3(1.8f, 0.7f, 3f), car.transform, collider: false);
            PG.Spawn(PrimitiveType.Cube, "Cabin", PG.paper, at + new Vector3(0f, 1.35f, -0.2f), new Vector3(1.5f, 0.55f, 1.5f), car.transform, collider: false);
            foreach (var o in new[] { new Vector3(-0.9f, 0.4f, 1f), new Vector3(0.9f, 0.4f, 1f), new Vector3(-0.9f, 0.4f, -1f), new Vector3(0.9f, 0.4f, -1f) })
            {
                var w = PG.Spawn(PrimitiveType.Cylinder, "Wheel", PG.ink, at + o, new Vector3(0.75f, 0.12f, 0.75f), car.transform, collider: false);
                w.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            }
            return car;
        }

        protected override void OnTick()
        {
            var tis = TIS;
            if (!_inCar)
            {
                bool near = Vector3.Distance(Player.transform.position, _parked.transform.position) < 3.2f;
                if (near && tis.IsSkillDown("Interact")) EnterCar();
                return;
            }

            bool boost = tis.IsSkillHeld("Boost");
            Player.SpeedMultiplier = boost ? 2.6f : 1.6f;
            if (boost && Player.Velocity.sqrMagnitude > 1f) Complete(2);

            if (tis.IsSkillDown("Horn"))
            {
                _hop = 1f;
                Toast("Bíp bíp!");
                Complete(1);
            }
            if (_riding != null)
            {
                _hop = Mathf.MoveTowards(_hop, 0f, Time.deltaTime * 4f);
                _riding.transform.localPosition = new Vector3(0f, -1f + Mathf.Sin(_hop * Mathf.PI) * 0.25f, 0f);
            }

            if (tis.IsSkillDown("ExitCar")) ExitCar();
        }

        void EnterCar()
        {
            if (_carProfile == null) { Toast("Chưa gán profile lái xe cho bài này"); return; }
            _inCar = true;
            _parked.SetActive(false);
            _riding = BuildCar(Player.transform, Player.transform.position + Vector3.down * 1f);
            _riding.transform.localPosition = new Vector3(0f, -1f, 0f);
            _riding.transform.localRotation = Quaternion.identity;
            if (Player.Visual != null) Player.Visual.gameObject.SetActive(false);
            Player.CanJump = false;

            TIS.SwapProfile(_carProfile);
            Toast("Đã lên xe — profile: " + _carProfile.profileName);
            Complete(0);
        }

        void ExitCar()
        {
            _inCar = false;
            TIS.SwapProfile(TIS.OriginalProfile);
            if (_riding != null) Destroy(_riding);
            _riding = null;
            if (Player.Visual != null) Player.Visual.gameObject.SetActive(true);
            _parked.transform.position = Player.transform.position + Player.transform.right * 2.2f + Vector3.down * 1.1f;
            _parked.transform.rotation = Player.transform.rotation;
            _parked.SetActive(true);
            Player.SpeedMultiplier = 1f;
            Player.CanJump = true;
            Toast("Đã xuống xe");
            if (IsObjectiveDone(0)) Complete(3);
        }

        protected override void OnExit()
        {
            TIS.OnProfileSwapped -= OnSwapped;
            if (_inCar) ExitCar();
        }
    }
}
