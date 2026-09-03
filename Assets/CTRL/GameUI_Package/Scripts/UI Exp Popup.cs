using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ExpPopupUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _expTextPrefab;
    [SerializeField] private RectTransform _spawnPoint;
    [SerializeField] private int _poolSize = 8;
    [SerializeField] private float _displayDuration = 1.5f;
    [SerializeField] private float _floatSpeed = 80f;
    [SerializeField] private float _stackOffset = 30f;
    [SerializeField] private Color _killColor   = Color.yellow;
    [SerializeField] private Color _assistColor = new Color(0.6f, 1f, 0.6f);

    private List<TextMeshProUGUI> _pool = new List<TextMeshProUGUI>();
    private Queue<TextMeshProUGUI> _available = new Queue<TextMeshProUGUI>();
    private int _activeCount = 0;

    private NShootHandler _shootHandler;

    private void Awake()
    {
        _shootHandler = GetComponentInParent<NShootHandler>();

        for (int i = 0; i < _poolSize; i++)
        {
            var text = Instantiate(_expTextPrefab, _spawnPoint);
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.gameObject.SetActive(false);
            _pool.Add(text);
            _available.Enqueue(text);
        }
    }

    private void OnEnable()
    {
        if (_shootHandler != null)
            _shootHandler.OnExpGained += Show;
    }

    private void OnDisable()
    {
        if (_shootHandler != null)
            _shootHandler.OnExpGained -= Show;
    }

    private void Show(int amount, bool isKill)
    {
        TextMeshProUGUI text = _available.Count > 0 ? _available.Dequeue() : _pool[0];
        StartCoroutine(PlayPopup(text, amount, isKill));
    }

    private IEnumerator PlayPopup(TextMeshProUGUI text, int amount, bool isKill)
    {
        // Stack offset tính từ gốc spawn point (anchoredPosition = 0)
        Vector2 startPos = Vector2.up * (_activeCount * _stackOffset);
        _activeCount++;

        text.text  = $"+{amount} EXP";
        text.color = isKill ? _killColor : _assistColor;
        text.rectTransform.anchoredPosition = startPos;
        text.gameObject.SetActive(true);

        float elapsed   = 0f;
        Color baseColor = text.color;

        while (elapsed < _displayDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _displayDuration;

            text.rectTransform.anchoredPosition = startPos + Vector2.up * (_floatSpeed * elapsed);

            float alpha = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);
            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            yield return null;
        }

        text.gameObject.SetActive(false);
        text.rectTransform.anchoredPosition = Vector2.zero;
        _activeCount = Mathf.Max(0, _activeCount - 1);
        _available.Enqueue(text);
    }
}