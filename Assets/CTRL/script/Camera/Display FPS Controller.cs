using UnityEngine;

public class DisplayFPSController : MonoBehaviour
{
    [SerializeField] private int minFPS = 30;

    private void Awake()
    {
        Application.runInBackground = true;
        ApplyMaxFPS();
    }

    public void ApplyMaxFPS()
    {
        QualitySettings.vSyncCount = 0;

        double refreshRate = Screen.currentResolution.refreshRateRatio.value;

        int maxFPS = Mathf.Max(
            minFPS,
            Mathf.RoundToInt((float)refreshRate)
        );

        Application.targetFrameRate = maxFPS;

        Debug.Log(
            $"Refresh Rate: {refreshRate}Hz | " +
            $"Target FPS: {Application.targetFrameRate}"
        );
    }
}