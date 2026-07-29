using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIHPPlayer : MonoBehaviour
{
    private int _hp;
    [SerializeField] private int _helpHP = 30;
    [SerializeField] private Image _uiOnHitImage;
    [SerializeField] private float _hitImageAlpha = 80f;

    [SerializeField] private Image _uiDead_BlackImage;

    [Header("Flash Settings")]
    [SerializeField] private float _fadeInDuration  = 0.1f;   // Thời gian fade sáng lên
    [SerializeField] private float _fadeOutDuration = 0.4f;   // Thời gian fade tối xuống
    [SerializeField] private float _lowHPPauseDuration = 0.3f; // Khoảng nghỉ giữa 2 lần nhấp nháy liên tục

    private Coroutine _lowHPFlashCoroutine;
    private Coroutine _flashOnceCoroutine;

    private void Start()
    {
        SetImageAlpha(_uiOnHitImage, 0f);
        SetImageAlpha(_uiDead_BlackImage, 0f);
    }

    public void Init(int HP)
    {
        _hp = HP;
    }

    public void SetHP(int newHP)
    {
        if (!gameObject.activeSelf) return;
        
        int previousHP = _hp;
        _hp = newHP;

        // Máu giảm → nhấp nháy 1 lần
        if (_hp < previousHP)
        {
            TriggerFlashOnce();
        }

        // Kiểm tra trạng thái máu thấp
        if (_hp <= _helpHP)
        {
            StartLowHPFlash();
        }
        else
        {
            StopLowHPFlash();
        }

    }

    // ─── Flash 1 lần khi bị hit ───────────────────────────────────────────────

    private void TriggerFlashOnce()
    {
        // Nếu đang nhấp nháy liên tục thì không cần flash riêng
        if (_lowHPFlashCoroutine != null) return;

        if (_flashOnceCoroutine != null)
            StopCoroutine(_flashOnceCoroutine);

        _flashOnceCoroutine = StartCoroutine(FlashOnce());
    }

    private IEnumerator FlashOnce()
    {
        float targetAlpha = _hitImageAlpha / 255f;

        yield return StartCoroutine(FadeTo(_uiOnHitImage, targetAlpha, _fadeInDuration));
        yield return StartCoroutine(FadeTo(_uiOnHitImage, 0f, _fadeOutDuration));

        _flashOnceCoroutine = null;
    }

    // ─── Nhấp nháy liên tục khi máu thấp ─────────────────────────────────────

    private void StartLowHPFlash()
    {
        if (_lowHPFlashCoroutine != null) return;
        _lowHPFlashCoroutine = StartCoroutine(LowHPFlashLoop());
    }

    private void StopLowHPFlash()
    {
        if (_lowHPFlashCoroutine != null)
        {
            StopCoroutine(_lowHPFlashCoroutine);
            _lowHPFlashCoroutine = null;
        }

        // Nếu FlashOnce đang chạy thì để nó tự fade out, không chen vào
        if (_flashOnceCoroutine != null) return;

        _flashOnceCoroutine = StartCoroutine(FadeTo(_uiOnHitImage, 0f, _fadeOutDuration));
    }

    private IEnumerator LowHPFlashLoop()
    {
        float targetAlpha = _hitImageAlpha / 255f;

        while (true)
        {
            yield return StartCoroutine(FadeTo(_uiOnHitImage, targetAlpha, _fadeInDuration));
            yield return StartCoroutine(FadeTo(_uiOnHitImage, 0f, _fadeOutDuration));
            yield return new WaitForSeconds(_lowHPPauseDuration);
        }
    }

    // ─── Fade mượt theo thời gian ─────────────────────────────────────────────

    private IEnumerator FadeTo(Image image, float targetAlpha, float duration)
    {
        float startAlpha = image != null ? image.color.a : 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // EaseInOut để mượt hơn
            float smoothT = t * t * (3f - 2f * t);
            SetImageAlpha(image, Mathf.Lerp(startAlpha, targetAlpha, smoothT));
            yield return null;
        }

        SetImageAlpha(image, targetAlpha);
    }

    // ─── Utility ──────────────────────────────────────────────────────────────

    private void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        var color = image.color;
        color.a = alpha;
        image.color = color;
    }

    public void SetDead(bool isDead)
    {
        if (!gameObject.activeSelf) return;
        
        if (isDead) StartCoroutine(FadeTo(_uiDead_BlackImage, 1, 2.5f));
        else SetImageAlpha(_uiDead_BlackImage, 0f);
    }
}