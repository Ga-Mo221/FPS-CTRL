using TMPro;
using UnityEngine;

public class UIGameState : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _fps_Text;
    [SerializeField] private TextMeshProUGUI _ping_Text;

    private void Update()
    {
        var gs = NetworkGameState.Instance;
        if (gs == null) return;
        
        if (_fps_Text != null)
            _fps_Text.text = $"FPS: {gs.FPS:F0}";
        
        if (_ping_Text != null)
            _ping_Text.text = $"Ping: {gs.PingMs} ms";
    }
}
