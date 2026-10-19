using System.Collections;
using Backend.Interfaces;
using Backend.PlayfabImpl;
using Backend.Handoff;
using Backend.Models;
using UnityEngine;

namespace Backend.Bootstrap
{
    /// <summary>
    /// Điểm khởi tạo backend + Service Locator + ICoroutineRunner.
    /// </summary>
    [DefaultExecutionOrder(-1000)] // khoi tao truoc cac binder doc BackendBootstrap.* trong Awake
    public class BackendBootstrap : MonoBehaviour, ICoroutineRunner
    {
        /// <summary>Instance dang song (guard tranh trung khi vao tu Login roi sang MenuShell).</summary>
        public static BackendBootstrap Instance { get; private set; }
        [Header("Matchmaking queues (khớp tên queue tạo trên PlayFab)")]
        [SerializeField] private string _ffaQueueName = "ffa_quick";
        [SerializeField] private string _tdmQueueName = "tdm_quick";
        [SerializeField] private int _giveUpAfterSeconds = 120;
        [SerializeField] private float _pollIntervalSeconds = 6f;
        
        [Header("Google OAuth Config (PC)")]
        [SerializeField] private string _googleWebClientId = "YOUR_WEB_CLIENT_ID.apps.googleusercontent.com";
        [SerializeField] private int _googleLocalPort = 5000;

        // ---- Auth ----
        public static IAuthService AuthService { get; private set; }
        public static IExternalAuthService ExternalAuthService { get; private set; }
        public static IAccountRecoveryService AccountRecoveryService { get; private set; }
        public static IEmailVerificationStatusService EmailVerificationStatusService { get; private set; }
        public static IUsernameAvailabilityService UsernameAvailabilityService { get; private set; }
        
        // ---- Google Auth (PC Service) ----
        public static IPCGoogleAuthService PCGoogleAuthService { get; private set; }
        
        // Getter hỗ trợ lấy cấu hình Google Client ID và Port
        public static string GoogleWebClientId { get; private set; }
        public static int GoogleLocalPort { get; private set; }

        // ---- Matchmaking ----
        public static IMatchmakingService MatchmakingService { get; private set; }
        public static IMatchHandoff MatchHandoff { get; private set; }

        // ---- Player Data (Phase 3) ----
        public static IPlayerProfileService PlayerProfileService { get; private set; }
        public static ILoadoutService LoadoutService { get; private set; }
        public static ISettingsService SettingsService { get; private set; }
        public static IPlayerStatsService PlayerStatsService { get; private set; }
        public static ILeaderboardService LeaderboardService { get; private set; }

        // ---- Menu Data (coins + online) ----
        public static IMenuDataService MenuDataService { get; private set; }
        public static IFriendsService FriendsService { get; private set; }
        public static IPartyService PartyService { get; private set; } // [Aemy 2026-09-21]
        public static IChatService ChatService { get; private set; }   // [Aemy 2026-10-19] chat menu (to doi + ban be)

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; } // da co bootstrap tu scene truoc -> bo cai nay
            Instance = this;
            // Cấu hình Google Config
            GoogleWebClientId = _googleWebClientId;
            GoogleLocalPort = _googleLocalPort;
            
            // Auth
            var playFabAuth = new PlayFabAuthService();
            AuthService = playFabAuth;
            ExternalAuthService = playFabAuth;
            AccountRecoveryService = playFabAuth;
            EmailVerificationStatusService = playFabAuth;
            UsernameAvailabilityService = playFabAuth;
            
            // Khởi tạo PC Google Auth Service
            PCGoogleAuthService = new PCGoogleAuthService();

            // Matchmaking
            var config = new MatchmakingConfig
            {
                FfaQueueName = _ffaQueueName,
                TdmQueueName = _tdmQueueName,
                GiveUpAfterSeconds = _giveUpAfterSeconds,
                PollIntervalSeconds = _pollIntervalSeconds
            };
            MatchmakingService = new PlayFabMatchmakingService(this, config);
            // [UI-INTEGRATION 2026-09-12 - Aemy] đổi từ LoggingMatchHandoff (chỉ log) sang cầu Fusion thật.
            MatchHandoff = new FusionMatchHandoff();  // vào phòng Fusion: SessionName = match.MatchId

            // Player Data
            var playerData = new PlayFabPlayerDataService();
            PlayerProfileService = playerData;
            LoadoutService = playerData;
            SettingsService = playerData;
            PlayerStatsService = playerData;
            LeaderboardService = playerData;

            MenuDataService = playerData;
            FriendsService = playerData;
        PartyService = new PlayFabPartyService(); // [Aemy]
            ChatService = new PlayFabChatService();   // [Aemy 2026-10-19]

            DontDestroyOnLoad(gameObject);
        }

        public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);
        public void Stop(Coroutine routine) { if (routine != null) StopCoroutine(routine); }
    }
}
