using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_ComboCounter : MonoBehaviour
{
    private const float ResetDelay = 1.6f;

    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private TextMeshProUGUI _comboText;
    private Sequence _sequence;
    private int _comboCount;
    private float _lastHitTime;

    public static UI_ComboCounter Create(Transform parent)
    {
        GameObject go = new GameObject("UI_ComboCounter");
        go.transform.SetParent(parent, false);

        UI_ComboCounter counter = go.AddComponent<UI_ComboCounter>();
        counter.Build();
        counter.HideImmediate();
        return counter;
    }

    public void ShowHit()
    {
        _comboCount++;
        _lastHitTime = Time.unscaledTime;
        _comboText.text = $"{_comboCount} HIT";

        _sequence?.Kill();
        _canvasGroup.alpha = 1f;
        _rectTransform.localScale = Vector3.one * 1.09f;
        _rectTransform.anchoredPosition = new Vector2(560f, 80f);

        _sequence = DOTween.Sequence().SetUpdate(true)
            .Append(_rectTransform.DOScale(1.79f, 0.08f).SetEase(Ease.OutBack))
            .Join(_rectTransform.DOAnchorPos(new Vector2(560f, 100f), 0.08f).SetEase(Ease.OutQuad))
            .Append(_rectTransform.DOScale(1.4f, 0.1f).SetEase(Ease.OutQuad))
            .AppendInterval(ResetDelay)
            .AppendCallback(() =>
            {
                if (Time.unscaledTime - _lastHitTime >= ResetDelay)
                    ResetCombo();
            });
    }

    public void ResetCombo()
    {
        _comboCount = 0;
        _sequence?.Kill();
        _sequence = DOTween.Sequence().SetUpdate(true)
            .Append(_canvasGroup.DOFade(0f, 0.25f))
            .Join(_rectTransform.DOScale(1.19f, 0.25f).SetEase(Ease.InQuad));
    }

    private void Build()
    {
        _rectTransform = gameObject.AddComponent<RectTransform>();
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _rectTransform.sizeDelta = new Vector2(476f, 168f);
        _rectTransform.anchoredPosition = new Vector2(560f, 80f);

        _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        GameObject textObject = new GameObject("ComboText");
        textObject.transform.SetParent(transform, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _comboText = textObject.AddComponent<TextMeshProUGUI>();
        _comboText.alignment = TextAlignmentOptions.Center;
        _comboText.fontSize = 69.5f;
        _comboText.fontStyle = FontStyles.Bold;
        _comboText.color = new Color(1f, 0.92f, 0.25f, 1f);
        _comboText.raycastTarget = false;
        _comboText.outlineWidth = 0.18f;
        _comboText.outlineColor = Color.black;

        ContentSizeFitter fitter = textObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    private void HideImmediate()
    {
        _comboCount = 0;
        _canvasGroup.alpha = 0f;
        _rectTransform.localScale = Vector3.one * 1.19f;
    }
}
