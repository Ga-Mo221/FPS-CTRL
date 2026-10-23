using TMPro;
using UnityEngine;

public class UIGameState : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _fps_Text;
    [SerializeField] private TextMeshProUGUI _ping_Text;
    [SerializeField] private TextMeshProUGUI _loss_Text;
    [SerializeField] private TextMeshProUGUI _voice_Text;

    private void Update()
    {
        var gs = NetworkGameState.Instance;
        if (gs == null) return;
        
        if (_fps_Text != null)
            _fps_Text.text = $"FPS: {gs.FPS:F0}";
        
        if (_ping_Text != null)
            _ping_Text.text = $"Ping: {gs.PingMs} ms";

        var loss = NetLossMonitor.Instance;
        if (_loss_Text != null && loss != null)
            _loss_Text.text = loss.Summary;

        if (_voice_Text != null)
            _voice_Text.text = VoiceLine(GameVoiceClient.Instance);
    }

    private static string VoiceLine(GameVoiceClient voice)
    {
        if (voice == null) return "";
        if (!voice.MicOn && !voice.HearOn) return "Voice: đã tắt";
        switch (voice.Status)
        {
            case GameVoiceClient.VoiceStatus.Connecting: return "Voice: đang kết nối...";
            case GameVoiceClient.VoiceStatus.ServerFull: return "Voice tạm không khả dụng (máy chủ voice đầy)";
            case GameVoiceClient.VoiceStatus.Error:      return "Voice: mất kết nối";
            case GameVoiceClient.VoiceStatus.Connected:
                if (voice.IsTransmitting) return voice.TeamMode ? "Voice: đang nói (đội)" : "Voice: đang nói";
                if (!voice.MicOn) return "Voice: mic tắt";
                if (!voice.HearOn) return "Voice: đã tắt tiếng mọi người";
                bool ptt = SettingManager.Instance == null || SettingManager.Instance.SettingData.VoicePushToTalk;
                return ptt && !Application.isMobilePlatform ? "Voice: giữ T để nói" : "Voice: mở mic";
            default: return "";
        }
    }
}
