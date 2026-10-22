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
        public const float IntervalSeconds = 9f;

        public static string Code { get; private set; }
        public static bool Ready { get; private set; }
        public static bool IsLeader { get; private set; }
        public static bool Active { get { return !string.IsNullOrEmpty(Code); } }

        private static Runner _runner;

        // Editor tat domain reload -> static giu tu lan Play truoc: reset moi lan chay.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Code = null; Ready = false; IsLeader = false; _runner = null; }

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
