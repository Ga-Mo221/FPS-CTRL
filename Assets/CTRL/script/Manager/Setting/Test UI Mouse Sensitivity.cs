using System;
using UnityEngine;
using UnityEngine.UI;

public class TestUIMouseSensitivity : MonoBehaviour
{
    [SerializeField] private Slider _slider;

    private SettingManager _setting;

    private void Awake()
    {
        _setting = SettingManager.Instance;
    }

    private void Start()
    {
        if (_setting == null)
            _setting = SettingManager.Instance;
        _slider.value = _setting.SettingData.MouseSensitivity;
    }

    public void UpdateMouseSensitivity(float sensitivity)
    {
        _setting.SetMouseSensitivity(sensitivity);
        _setting.UpdateMouseSensitivity();
    }
}
