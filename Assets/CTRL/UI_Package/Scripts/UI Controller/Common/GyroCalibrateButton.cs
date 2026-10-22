// [Aemy 2026-10-19] FILE MOI. Nut "HIỆU CHỈNH" gyro trong tab CHUỘT (MenuShell + Pause).
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Bam -> nhac dat tay cam yen tren ban, dem nguoc vai giay lay mau gyro, roi ap offset (chong troi tam).
    /// Nhan cua nut hien trang thai; tu tra ve "HIỆU CHỈNH" sau vai giay. Logic gyro nam o MotionSettingsApplier (SRP).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class GyroCalibrateButton : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private float _sampleSeconds = 2f;
        [SerializeField] private float _resultSeconds = 2.5f;

        private const string Idle     = "HIỆU CHỈNH";
        private const string Wait     = "ĐẶT TAY CẦM XUỐNG… {0}";
        private const string Done     = "ĐÃ HIỆU CHỈNH ✓";
        private const string Failed   = "CHƯA LẤY ĐƯỢC MẪU";
        private const string NoGyro   = "KHÔNG CÓ GYRO";

        private Button _button;
        private Coroutine _run;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_label == null) _label = GetComponentInChildren<TMP_Text>(true);
            _button.onClick.AddListener(OnClick);
        }

        private void OnEnable()  { SetLabel(Idle); }

        private void OnDisable()
        {
            if (_run != null) { StopCoroutine(_run); _run = null; }
            if (MotionSettingsApplier.IsCalibrating) MotionSettingsApplier.EndCalibration(CurrentData());
        }

        private void OnClick()
        {
            if (_run != null) return;
            _run = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (!MotionSettingsApplier.BeginCalibration())
            {
                SetLabel(NoGyro);
                yield return new WaitForSecondsRealtime(_resultSeconds);
                SetLabel(Idle);
                _run = null;
                yield break;
            }

            float end = Time.unscaledTime + _sampleSeconds;
            while (Time.unscaledTime < end)
            {
                SetLabel(string.Format(Loc.T(Wait), Mathf.CeilToInt(end - Time.unscaledTime)), false);
                yield return null;
            }

            bool ok = MotionSettingsApplier.EndCalibration(CurrentData());
            SetLabel(ok ? Done : Failed);
            yield return new WaitForSecondsRealtime(_resultSeconds);
            SetLabel(Idle);
            _run = null;
        }

        private static SettingData CurrentData()
        {
            return SettingManager.Instance != null ? SettingManager.Instance.SettingData : null;
        }

        private void SetLabel(string vi, bool translate = true)
        {
            if (_label != null) _label.text = translate ? Loc.T(vi) : vi;
        }
    }
}
