using Sirenix.OdinInspector;
using UnityEngine;

public class SettingManager : MonoBehaviour
{
    public static SettingManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [SerializeField] private SettingData _settingData;

    public SettingData SettingData => _settingData;

    // save or load setting data to file
    

    [Button]
    private void UpdateMouseSensitivity()
    {
        if (_settingData == null) return;

        NPlayer[] players = FindObjectsByType<NPlayer>(
            FindObjectsSortMode.None
        );

        foreach (var player in players)
        {
            if (player.IsLocalPlayer)
            {
                player._movementHandler.RPC_SetCameraSpeed(
                    _settingData.MouseSensitivity
                );
                return;
            }
        }
    }
}
