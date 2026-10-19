using UnityEngine;
using UnityEngine.UI;

// Khoi phuc class bi lo vo hieu hoa khi don Settings trung.
// Slider HUD chinh do nhay chuot (test UI). Noi lai voi SettingManager da merge
// (ban cu cua lead dung Setting rieng cua lead - da thay bang UI_Package SettingManager).
public class TestUIMouseSensitivity : MonoBehaviour
{
    [SerializeField] private Slider _slider;

    private void OnEnable()
    {
        if (_slider == null) return;
        var mgr = SettingManager.Instance;
        if (mgr != null && mgr.SettingData != null)
            _slider.SetValueWithoutNotify(mgr.SettingData.MouseSensitivity);
        _slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnDisable()
    {
        if (_slider != null) _slider.onValueChanged.RemoveListener(OnSliderChanged);
    }

    private void OnSliderChanged(float v)
    {
        var mgr = SettingManager.Instance;
        if (mgr == null || mgr.SettingData == null) return;
        mgr.SettingData.MouseSensitivity = v;
        mgr.Apply();
        mgr.Save();
    }
}
