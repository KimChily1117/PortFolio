using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ToastPopup : MonoBehaviour
{
    [SerializeField]
    private TMP_Text _contextText;

    [SerializeField]
    private Text _legacyContextText;

    [SerializeField]
    private CanvasGroup _canvasGroup;

    private Sequence _sequence;

    private void Awake()
    {
        Prepare();
    }

    public void Prepare()
    {
        ResolveReferences();

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

    public void Show(string message, float holdDuration = 1.5f)
    {
        if ((_contextText == null && _legacyContextText == null) || _canvasGroup == null)
            ResolveReferences();

        if (_contextText != null)
            _contextText.text = message;
        else if (_legacyContextText != null)
            _legacyContextText.text = message;
        else
            Debug.LogWarning("[UI] ToastPopup text component is not assigned. Expected TMP_Text or Text named Context.");

        if (_canvasGroup == null)
        {
            Debug.LogWarning("[UI] ToastPopup CanvasGroup is missing.");
            return;
        }

        _sequence?.Kill();
        gameObject.SetActive(true);

        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        _sequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(_canvasGroup.DOFade(1f, 0.2f))
            .AppendInterval(holdDuration)
            .Append(_canvasGroup.DOFade(0f, 0.35f))
            .OnComplete(() => gameObject.SetActive(false));
    }

    private void ResolveReferences()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>() ?? GetComponentInChildren<CanvasGroup>(true);

        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (_contextText == null)
            _contextText = GetComponentInChildren<TMP_Text>(true);

        if (_legacyContextText == null)
            _legacyContextText = GetComponentInChildren<Text>(true);
    }

    private void OnDisable()
    {
        _sequence?.Kill();
        _sequence = null;
    }
}
