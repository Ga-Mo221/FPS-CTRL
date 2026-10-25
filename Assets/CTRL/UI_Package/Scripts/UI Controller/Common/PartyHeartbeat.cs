// [Aemy 2026-10-22] FILE MOI. Heartbeat to doi chay XUYEN SCENE (menu <-> tran).
using UnityEngine;
using Backend.Bootstrap;

namespace Game.UI
{
    /// <summary>
    /// Truoc day chi PartyController (nam o scene menu) gui heartbeat -> trong tran khong ai "Touch":
    ///  - nguoi ve menu som thay nguoi con trong tran "mat ket noi" va da nham;
    ///  - truong nhom thoat game han thi khong ai don (thanh vien ket voi truong nhom ma).
    /// Lop nay giu trang thai to doi cua PHIEN CHOI hien tai (ma, san sang, truong nhom) trong 1 runner
    /// DontDestroyOnLoad va Touch deu ~9s ca khi dang trong tran. SRP: chi lo heartbeat + nho trang thai;
    /// quyet dinh vao / roi / da do PartyController lo.
    /// Thoat game: runner mat theo -> khong con Touch -> nguoi khac don sau ~30s (giu dung y "vao lai nhanh van giu to doi").
    /// </summary>
    public static class PartyHeartbeat
    {
        public const float IntervalSeconds = 5f; // [Aemy 2026-10-22] 9s -> 5s: nguoi khac phat hien mat ket noi nhanh hon

        public static string Code { get; private set; }
        public static bool Ready { get; private set; }
        public static bool IsLeader { get; private set; }
        public static bool Active { get { return !string.IsNullOrEmpty(Code); } }

        private static Runner _runner;
        private static bool _quitting, _leftForQuit;

        // [Aemy 2026-10-22] Tat game binh thuong (nut THOAT GAME, dau X, Alt+F4) khi con trong to doi:
        // hoan tat game toi da ~2.5s de ROI NHOM tren server truoc -> ca nhom thay minh roi o lan poll ke tiep (~3s)
        // thay vi cho het han heartbeat. Crash / tat tien trinh thi van dua vao het han heartbeat (~15s).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HookQuit()
        {
            Application.wantsToQuit -= OnWantsToQuit;
            Application.wantsToQuit += OnWantsToQuit;
        }

        private static bool OnWantsToQuit()
        {
            if (_leftForQuit || !Active) return true;
            if (_quitting) return false;
            _quitting = true;
            EnsureRunner();
            _runner.StartCoroutine(LeaveThenQuit());
            return false;
        }

        private static System.Collections.IEnumerator LeaveThenQuit()
        {
            bool done = false;
            string code = Code;
            Debug.Log("[PartyHeartbeat] Tat game -> roi to doi " + code + " truoc.");
            PartyController.LeaveOnServer(code, () => done = true);
            float until = Time.unscaledTime + 2.5f;
            while (!done && Time.unscaledTime < until) yield return null;
            PartyController.ForgetSavedParty();
            _leftForQuit = true;
            End();
            Application.Quit();
        }

        // Editor tat domain reload -> static giu tu lan Play truoc: reset moi lan chay.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Code = null; Ready = false; IsLeader = false; _runner = null; _quitting = false; _leftForQuit = false; }

        /// <summary>Vao / khoi phuc to doi: bat dau (hoac tiep tuc) Touch. Touch ngay 1 lan.</summary>
        public static void Begin(string code, bool ready, bool isLeader)
        {
            if (string.IsNullOrEmpty(code)) { End(); return; }
            bool same = code == Code;
            Code = code; Ready = ready; IsLeader = isLeader;
            EnsureRunner();
            if (!same) TouchNow();
        }

        /// <summary>Doi trang thai san sang / truong nhom (gia tri ghi kem moi lan Touch).</summary>
        public static void Set(bool ready, bool isLeader)
        {
            if (!Active) return;
            Ready = ready; IsLeader = isLeader;
        }

        /// <summary>Roi / bi da / doi tai khoan: ngung Touch.</summary>
        public static void End()
        {
            Code = null; Ready = false; IsLeader = false;
            if (_runner != null) { Object.Destroy(_runner.gameObject); _runner = null; }
        }

        private static void TouchNow()
        {
            var svc = BackendBootstrap.PartyService;
            if (svc != null && Active) svc.Touch(Code, Ready, null, null);
        }

        private static void EnsureRunner()
        {
            if (_runner != null) return;
            var go = new GameObject("[PartyHeartbeat]");
            go.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }

        private class Runner : MonoBehaviour
        {
            private float _next;
            private void OnEnable() { _next = Time.unscaledTime + IntervalSeconds; }
            private void Update()
            {
                if (!Active) return;
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + IntervalSeconds;
                TouchNow();
            }
        }
    }
}
