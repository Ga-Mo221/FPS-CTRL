using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class UICrosshair : MonoBehaviour
{
    [FoldoutGroup("Basic")]
    [SerializeField] private Image _background;
    [FoldoutGroup("Basic")]
    [SerializeField] private Image _verticalDivider_U;
    [FoldoutGroup("Basic")]
    [SerializeField] private Image _verticalDivider_D;
    [FoldoutGroup("Basic")]
    [SerializeField] private Image _horizontalDivider_L;
    [FoldoutGroup("Basic")]
    [SerializeField] private Image _horizontalDivider_R;
    
    [FoldoutGroup("Hit")]
    [SerializeField] private Image _hit_verticalDivider_U;
    [FoldoutGroup("Hit")]
    [SerializeField] private Image _hit_verticalDivider_D;
    [FoldoutGroup("Hit")]
    [SerializeField] private Image _hit_horizontalDivider_L;
    [FoldoutGroup("Hit")]
    [SerializeField] private Image _hit_horizontalDivider_R;
    
    [FoldoutGroup("Spread")]
    [Tooltip("Khoảng giãn (px) cộng thêm vào gap mỗi phát bắn.")]
    [SerializeField] private float _spreadPerShot = 6f;
    [FoldoutGroup("Spread")]
    [Tooltip("Khoảng giãn tối đa (px).")]
    [SerializeField] private float _maxSpread = 30f;
    [FoldoutGroup("Spread")]
    [Tooltip("Tốc độ co lại (px/giây) sau khi ngừng bắn.")]
    [SerializeField] private float _recoverSpeed = 80f;
    [FoldoutGroup("Spread")]
    [Tooltip("Chờ (giây) sau phát cuối rồi mới bắt đầu co lại.")]
    [SerializeField] private float _recoverDelay = 0.08f;

    private Coroutine _hitCoroutine;

    private float _baseGap;
    private float _spread;
    private float _lastFireTime;

    private void Awake()
    {
        // Gap gốc = vị trí đặt sẵn trong prefab (cho tới khi SetGap() được gọi).
        _baseGap = _verticalDivider_U.rectTransform.localPosition.y;
    }

    private void Update()
    {
        if (_spread <= 0f) return;
        if (Time.unscaledTime - _lastFireTime < _recoverDelay) return;

        _spread = Mathf.MoveTowards(_spread, 0f, _recoverSpeed * Time.unscaledDeltaTime);
        ApplyGap();
    }

    /// <summary>Gọi mỗi phát bắn của local player: tâm giãn ra rồi tự co lại.</summary>
    [Button]
    public void Fire(float multiplier = 1f)
    {
        _spread = Mathf.Min(_maxSpread, _spread + _spreadPerShot * multiplier);
        _lastFireTime = Time.unscaledTime;
        ApplyGap();
    }

    [Button]
    public void HideCenter(bool hide) => _background.gameObject.SetActive(hide);

    [Button]
    public void SetSize(float width, float height)
    {
        _verticalDivider_U.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, width);
        _verticalDivider_U.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal, height);

        _verticalDivider_D.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, width);
        _verticalDivider_D.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal, height);

        _horizontalDivider_L.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal, width);
        _horizontalDivider_L.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, height);

        _horizontalDivider_R.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal, width);
        _horizontalDivider_R.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, height);
    }
    
    [Button]
    public void SetGap(float gap)
    {
        _baseGap = gap;
        ApplyGap();
    }

    private void ApplyGap()
    {
        float gap = _baseGap + _spread;

        _verticalDivider_U.rectTransform.SetLocalPositionAndRotation(
            new Vector3(0, gap, 0), Quaternion.identity);
        
        _verticalDivider_D.rectTransform.SetLocalPositionAndRotation(
            new Vector3(0, -gap, 0), Quaternion.identity);
        
        _horizontalDivider_L.rectTransform.SetLocalPositionAndRotation(
            new Vector3(-gap, 0, 0), Quaternion.identity);
        
        _horizontalDivider_R.rectTransform.SetLocalPositionAndRotation(
            new Vector3(gap, 0, 0), Quaternion.identity);
    }

    [Button]
    public void SetColor(Color color)
    {
        _background.color = color;
        _verticalDivider_U.color = color;
        _verticalDivider_D.color = color;
        _horizontalDivider_L.color = color;
        _horizontalDivider_R.color = color;
    }

    [Button]
    public void SetADS(bool ads)
    {
        // Tắt tâm (ngắm/sniper) → reset độ giãn để lúc hiện lại không bị "nở" sẵn.
        if (!ads && _spread > 0f)
        {
            _spread = 0f;
            ApplyGap();
        }
        _background.transform.parent.gameObject.SetActive(ads);
    }

    [Button]
    public void Hit(float speed = 50f)
    {
        _hit_horizontalDivider_L.transform.parent.gameObject.SetActive(true);
        if (_hitCoroutine != null)
            StopCoroutine(_hitCoroutine);
        _hitCoroutine = StartCoroutine(hitCoroutine(speed));
    }

    private IEnumerator hitCoroutine(float speed)
    {
        float gap = 15;
        while (gap < 20f)
        {
            gap += Time.deltaTime * speed;
            SetHitVisuals(gap);
            yield return null;
        }
        _hit_horizontalDivider_L.transform.parent.gameObject.SetActive(false);
        _hitCoroutine = null;
    }

    private void SetHitVisuals(float gap)
    {
        _hit_verticalDivider_U.rectTransform.localPosition = new Vector3(0, gap, 0);
        
        _hit_verticalDivider_D.rectTransform.localPosition = new Vector3(0, -gap, 0);
        
        _hit_horizontalDivider_L.rectTransform.localPosition = new Vector3(-gap, 0, 0);
        
        _hit_horizontalDivider_R.rectTransform.localPosition = new Vector3(gap, 0, 0);
    }
}
