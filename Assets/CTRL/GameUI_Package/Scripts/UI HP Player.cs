using System.Collections;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIHPPlayer : MonoBehaviour
{
    private int _hp;
    [TabGroup("UI","Hit Damage")]
    [SerializeField] private Image _uiOnHitImage;
    [TabGroup("UI","Hit Damage")]
    [SerializeField] private int _helpHP = 30;
    [TabGroup("UI","Hit Damage")]
    [SerializeField] private float _hitImageAlpha = 80f;

    [TabGroup("UI", "Dead")]
    [SerializeField] private Image _uiDead_BlackImage;
    [TabGroup("UI", "Dead")]
    [SerializeField] private TextMeshProUGUI _deadStatus_Text;
    [TabGroup("UI", "Dead")]
    [SerializeField] private TextMeshProUGUI _recoverTime_Text;
    
    [TabGroup("UI","Flash")]
    [SerializeField] private Image _uiFlash_WhiteImage;
    [TabGroup("UI","Flash")]
    [SerializeField] private float _flashbangDuration = 5f;

    [TabGroup("UI","Flash")]
    [SerializeField] private float _fadeInDuration  = 0.1f;   // Thời gian fade sáng lên
    [TabGroup("UI","Flash")]
    [SerializeField] private float _fadeOutDuration = 0.4f;   // Thời gian fade tối xuống
    [TabGroup("UI","Flash")]
    [SerializeField] private float _lowHPPauseDuration = 0.3f; // Khoảng nghỉ giữa 2 lần nhấp nháy liên tục

    private Coroutine _lowHPFlashCoroutine;
    private Coroutine _flashOnceCoroutine;
    
    private PlayerData _data;
    private CharComponentManager _componentManager;

    private void Start()
    {
        SetImageAlpha(_uiOnHitImage, 0f);
        SetImageAlpha(_uiFlash_WhiteImage, 0f);
    }

    public void Init(int HP, PlayerData data,  CharComponentManager componentManager)
    {
        _hp = HP;
        _data = data;
        _componentManager = componentManager;
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
        if (gameObject.activeSelf)
            _lowHPFlashCoroutine = StartCoroutine(LowHPFlashLoop());
    }

    private void StopLowHPFlash()
    {
        if (_lowHPFlashCoroutine != null)
        {
            if (gameObject.activeSelf)
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

    
    public void Flash(float distance, float radius)
    {
        float time = _flashbangDuration * (1 - distance / radius);
        if (gameObject.activeSelf)
            StartCoroutine(startFlashGrenade(time));
        //Debug.Log($"{transform.root.name} Choang {time}");
    }

    IEnumerator startFlashGrenade(float duration)
    {
        if (_componentManager.NetworkObject.HasInputAuthority)
        {
            AudioManager.Instance.PlayClip
            (
                _componentManager.Audio_Flashbang,
                _data.FlashbangClips,
                _data.Volume_Flashbang,
                0.2f, 4f, 2f
            );
        }
        yield return StartCoroutine(FadeTo(_uiFlash_WhiteImage, 1f, 0.2f));
        yield return new WaitForSeconds(duration);
        GameManager.Instance.FlashbangEffect.TriggerFlashbang(6f);
        yield return StartCoroutine(FadeTo(_uiFlash_WhiteImage, 0f, 3f));
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
        var group = _uiDead_BlackImage.GetComponent<CanvasGroup>();
        if (isDead)
        {
            StartCoroutine(FadeToCanvasGroup(group, 0.9f, 2.5f));
        }
        else
        {
            if (group != null) group.alpha = 0f;
        }
    }
    
    private IEnumerator FadeToCanvasGroup(CanvasGroup canvasGroup, float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 0f;
        float _canvasAlphaTime = 0f;
        
        while (_canvasAlphaTime < duration)
        {
            _canvasAlphaTime += Time.deltaTime;
            float t = Mathf.Clamp01(_canvasAlphaTime / duration);
            // EaseInOut để mượt hơn
            float smoothT = t * t * (3f - 2f * t);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);
            yield return null;
        }
        
        canvasGroup.alpha = targetAlpha;
    }

    public void SetDeadStatus(string killer, string weaponname)
    {
        string text = $"BỊ TIÊU DIỆT BỞI {killer} · {weaponname}";
        _deadStatus_Text.text = text;
    }

    public void SetRecoverTime(int time)
    {
        _recoverTime_Text.text = time.ToString();
    }
}