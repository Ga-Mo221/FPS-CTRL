using Fusion;
using Photon.Realtime;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using GaMo.TrueInputSystem;
using UnityEngine;

/// <summary>
/// Voice chat trong trận (Photon Voice 2), gắn trên prefab NetworkRunner thay cho FusionVoiceClient.
///
/// • Tự vào phòng voice "&lt;session&gt;_voice" khi vào trận Fusion, tự rời khi rời trận (FusionVoiceClient lo).
/// • Đấu đội (TDM): chỉ nghe/nói với đồng đội — mỗi đội 1 interest group, âm thanh 2D (không theo khoảng cách).
/// • Đấu đơn (FFA): nghe/nói với tất cả — group 0, âm thanh 3D theo khoảng cách.
/// • Giới hạn CCU voice (gói Free 20 CCU, tính riêng với Fusion): khi Photon từ chối vì MaxCcuReached thì KHÔNG
///   tự kết nối lại liên tục như mặc định, chỉ thử lại sau <see cref="_ccuRetrySeconds"/> giây; game vẫn chơi bình thường.
/// • Cài đặt "Mic của tôi" (tắt = không phát giọng mình, vẫn nghe) và "Mic mọi người" (tắt = không nghe ai, vẫn nói được).
///   Tắt cả hai = rời phòng voice, không chiếm CCU voice.
/// </summary>
public class GameVoiceClient : FusionVoiceClient
{
    public enum VoiceStatus { Off, Connecting, Connected, ServerFull, Error }

    public static GameVoiceClient Instance { get; private set; }

    [Header("Game Voice")]
    [SerializeField] private float _ccuRetrySeconds = 60f;   // hết CCU voice: chờ rồi thử lại 1 lần
    [SerializeField] private float _maxHearDistance = 40f;  // FFA: xa hơn thì không nghe
    [SerializeField] private float _fullVolumeDistance = 3f;
    [SerializeField] private float _vadThreshold = 0.01f;    // mở mic liên tục: ngưỡng nhận giọng

    public VoiceStatus Status { get; private set; } = VoiceStatus.Off;
    public bool IsTransmitting => PrimaryRecorder != null && PrimaryRecorder.IsCurrentlyTransmitting;
    public bool TeamMode { get; private set; }
    public bool MicOn => Settings == null || Settings.VoiceMic;
    public bool HearOn => Settings == null || Settings.VoiceHear;

    private const byte GroupAll = 0;           // FFA: group 0 = mọi người trong phòng voice
    private const byte GroupBlue = 1, GroupRed = 2;

    private byte _subscribedGroup;              // group đang nghe (TDM)
    private bool _groupApplied;
    private float _retryAt = -1f;
    private float _speakerRefreshTimer;
    private bool _lastHearOn = true;
    private string _appliedMic;                  // thiết bị thu âm đã áp vào Recorder (null = chưa áp)

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        if (PrimaryRecorder == null) PrimaryRecorder = GetComponentInChildren<Recorder>(true);
        if (PrimaryRecorder != null)
        {
            PrimaryRecorder.TransmitEnabled = false;
            PrimaryRecorder.RecordWhenJoined = true;
        }
        AutoConnectAndJoin = VoiceEnabledInSettings();
        UpdateMicDevice();
        RemoteVoiceAdded += link => Debug.Log($"[Voice] Nhận luồng voice của player #{link.PlayerId} (voice {link.VoiceId}).");
    }

    protected override void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (VoiceEnabledInSettings() &&
            !UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
#endif
        base.Start();
    }

    protected override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    private static SettingData Settings => SettingManager.Instance != null ? SettingManager.Instance.SettingData : null;
    /// <summary>Còn cần voice khi mình còn nói HOẶC còn nghe; tắt cả hai thì rời voice để trả CCU.</summary>
    private static bool VoiceEnabledInSettings() => Settings == null || Settings.VoiceMic || Settings.VoiceHear;

    // ── Trạng thái kết nối ───────────────────────────────────────────────────
    protected override void OnVoiceStateChanged(ClientState fromState, ClientState toState)
    {
        if (toState == ClientState.Disconnected && Client.DisconnectedCause == DisconnectCause.MaxCcuReached)
        {
            // Mặc định VoiceFollowClient sẽ kết nối lại ngay lập tức → spam server. Chặn lại và hẹn giờ thử lại.
            AutoConnectAndJoin = false;
            Status = VoiceStatus.ServerFull;
            _retryAt = Time.unscaledTime + _ccuRetrySeconds;
            Debug.LogWarning($"[Voice] Máy chủ voice đã đủ CCU, thử lại sau {_ccuRetrySeconds:0}s.");
        }
        else if (toState == ClientState.Joined)
        {
            Status = VoiceStatus.Connected;
            _groupApplied = false;
        }
        else if (toState == ClientState.Disconnected)
        {
            Status = AutoConnectAndJoin ? VoiceStatus.Error : (Status == VoiceStatus.ServerFull ? VoiceStatus.ServerFull : VoiceStatus.Off);
        }
        else if (Status != VoiceStatus.ServerFull)
        {
            Status = VoiceStatus.Connecting;
        }

        base.OnVoiceStateChanged(fromState, toState);
    }

    protected override void Update()
    {
        base.Update();

        bool wanted = VoiceEnabledInSettings();
        if (!wanted)
        {
            // Tắt cả mic mình lẫn mic mọi người → rời voice để trả CCU.
            AutoConnectAndJoin = false;
            _retryAt = -1f;
            if (Client.IsConnected && Client.State != ClientState.Disconnecting) Disconnect();
            Status = VoiceStatus.Off;
            if (PrimaryRecorder != null) PrimaryRecorder.TransmitEnabled = false;
            return;
        }

        if (Status == VoiceStatus.ServerFull)
        {
            if (_retryAt > 0f && Time.unscaledTime >= _retryAt)
            {
                _retryAt = -1f;
                AutoConnectAndJoin = true;
                Status = VoiceStatus.Connecting;
                if (!Client.IsConnected) ConnectAndJoinRoom();
            }
        }
        else if (!AutoConnectAndJoin)
        {
            // Vừa bật lại mic mình / mic mọi người trong Cài đặt.
            AutoConnectAndJoin = true;
            if (!Client.IsConnected && Runner != null && Runner.IsRunning) ConnectAndJoinRoom();
        }

        UpdateMicDevice();
        UpdateMode();
        UpdateGroups();
        UpdateTransmit();

        _speakerRefreshTimer -= Time.unscaledDeltaTime;
        if (_speakerRefreshTimer <= 0f || HearOn != _lastHearOn) // đổi "Mic mọi người" thì áp dụng ngay
        {
            _speakerRefreshTimer = 0.5f;
            RefreshSpeakers();
        }
    }

    private NetworkRunner Runner => GetComponent<NetworkRunner>();

    // ── Thiết bị thu âm (Cài đặt → Âm thanh → Thiết bị thu âm) ──────────────
    // Máy có nhiều mic (tai nghe, webcam, mic ảo Steam...) thì "mặc định" của Windows có thể là mic ảo không có tiếng.
    private void UpdateMicDevice()
    {
        var rec = PrimaryRecorder;
        if (rec == null) return;
        string want = Settings != null ? Settings.VoiceMicDevice : "";
        if (want == _appliedMic) return;
        _appliedMic = want;

        var devices = Microphone.devices;
        bool found = !string.IsNullOrEmpty(want) && System.Array.IndexOf(devices, want) >= 0;
        rec.MicrophoneType = Recorder.MicType.Unity; // chọn theo tên Microphone.devices
        rec.MicrophoneDevice = found ? new Photon.Voice.DeviceInfo(want) : Photon.Voice.DeviceInfo.Default;
        Debug.Log($"[Voice] Thiết bị thu âm: {(found ? want : "mặc định hệ thống")}" +
                  (!string.IsNullOrEmpty(want) && !found ? $" (không thấy '{want}')" : "") +
                  $". Máy có: {(devices.Length > 0 ? string.Join(", ", devices) : "không có mic nào")}.");
    }

    private void UpdateMode()
    {
        var gs = NetworkGameState.Instance;
        TeamMode = gs != null && gs.Mode == GamePlayMode.Team;
    }

    /// <summary>Group của đội mình (TDM). 255 = chưa biết đội.</summary>
    private byte MyTeamGroup()
    {
        var me = NPlayer.Local;
        if (me == null || me.Object == null || !me.Object.IsValid) return 255;
        switch (me.NetworkedTeam)
        {
            case PlayerTeam.Blue: return GroupBlue;
            case PlayerTeam.Red: return GroupRed;
            default: return 255;
        }
    }

    // ── Kênh nghe (interest group) ───────────────────────────────────────────
    private void UpdateGroups()
    {
        if (!Client.InRoom) return;
        byte want = TeamMode ? MyTeamGroup() : GroupAll;
        if (want == 255) return; // TDM chưa có đội: chưa đổi gì

        if (_groupApplied && want == _subscribedGroup) return;
        // Bỏ nghe mọi group cũ (byte[0] = tất cả) rồi chỉ nghe group cần. Group 0 luôn nhận được, TDM không ai nói ở group 0.
        if (Client.OpChangeGroups(new byte[0], want == GroupAll ? null : new[] { want }))
        {
            _subscribedGroup = want;
            _groupApplied = true;
            if (PrimaryRecorder != null) PrimaryRecorder.InterestGroup = want;
            Debug.Log(TeamMode
                ? $"[Voice] Đấu đội: chỉ nghe/nói với đội {(want == GroupBlue ? "Xanh" : "Đỏ")} (group {want}). Phòng voice có {Client.CurrentRoom.PlayerCount} người."
                : $"[Voice] Đấu đơn: nghe/nói với tất cả, 3D theo khoảng cách. Phòng voice có {Client.CurrentRoom.PlayerCount} người.");
        }
    }

    // ── Nói: giữ phím (push-to-talk) hoặc mở mic tự nhận giọng ───────────────
    private void UpdateTransmit()
    {
        var rec = PrimaryRecorder;
        if (rec == null) return;

        bool canTalk = MicOn && Status == VoiceStatus.Connected && _groupApplied && !Paused();
        // Mobile chưa có nút giữ-để-nói → luôn mở mic tự nhận giọng.
        bool ptt = (Settings == null || Settings.VoicePushToTalk) && !Application.isMobilePlatform;
        bool talk;
        if (ptt)
        {
            var input = TISInputBridge.Instance;
            talk = input != null && input.GetSkillAction("Voice", TISButtonReadMode.Hold);
            rec.VoiceDetection = false;
        }
        else
        {
            talk = true;
            rec.VoiceDetection = true;
            rec.VoiceDetectionThreshold = _vadThreshold;
        }
        bool transmit = canTalk && talk;
        if (rec.TransmitEnabled != transmit)
        {
            rec.TransmitEnabled = transmit;
            if (ptt) Debug.Log(transmit ? $"[Voice] Bắt đầu nói (group {rec.InterestGroup})." : "[Voice] Ngừng nói.");
        }
    }

    private static bool Paused()
    {
        // Đang gõ chữ / mở menu thì không mở mic bằng phím.
        var es = UnityEngine.EventSystems.EventSystem.current;
        var sel = es != null ? es.currentSelectedGameObject : null;
        return sel != null && sel.GetComponent<TMPro.TMP_InputField>() != null;
    }

    // ── Loa của người khác: 2D (TDM) hoặc 3D theo khoảng cách (FFA) ─────────
    private void RefreshSpeakers()
    {
        float volume = Settings != null ? Settings.VoiceVolume : 1f;
        bool hear = HearOn;
        _lastHearOn = hear;
        foreach (var vno in FindObjectsByType<VoiceNetworkObject>(FindObjectsSortMode.None))
        {
            var speaker = vno.SpeakerInUse;
            if (speaker == null) continue;
            var src = speaker.GetComponent<AudioSource>();
            if (src == null) continue;
            src.volume = volume;
            src.mute = !hear;
            if (TeamMode)
            {
                src.spatialBlend = 0f;
            }
            else
            {
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = _fullVolumeDistance;
                src.maxDistance = _maxHearDistance;
                src.dopplerLevel = 0f;
            }
        }
    }
}
