using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class SceneLoadingOverlay : MonoBehaviour
{
    static SceneLoadingOverlay s_instance;

    CanvasGroup _canvasGroup;
    const float FadeDurationSeconds = 1.2f;

    Text _titleText;
    Text _detailText;

    public static bool IsVisible => s_instance != null && s_instance.gameObject.activeSelf;

    public static void Show(string title, string detail)
    {
        EnsureInstance();
        s_instance.SetText(title, detail);
        s_instance.gameObject.SetActive(true);
        s_instance.transform.SetAsLastSibling();
        s_instance.FadeIn();
    }

    public static void SetDetail(string detail)
    {
        if (s_instance == null)
            return;

        s_instance.SetText(null, detail);
    }

    public static void Hide()
    {
        if (s_instance == null)
            return;

        s_instance.FadeOut();
    }

    static void EnsureInstance()
    {
        if (s_instance != null)
            return;

        GameObject root = new GameObject("@SceneLoadingOverlay");
        DontDestroyOnLoad(root);
        s_instance = root.AddComponent<SceneLoadingOverlay>();
        s_instance.Build();
    }

    void FadeIn()
    {
        if (_canvasGroup == null)
            return;

        _canvasGroup.DOKill();
        _canvasGroup.alpha = 0f;
        _canvasGroup.DOFade(1f, FadeDurationSeconds).SetUpdate(true);
    }

    void FadeOut()
    {
        if (_canvasGroup == null)
        {
            gameObject.SetActive(false);
            return;
        }

        _canvasGroup.DOKill();
        _canvasGroup.DOFade(0f, FadeDurationSeconds).SetUpdate(true).OnComplete(() => gameObject.SetActive(false));
    }

    void Build()
    {
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 9000;

        gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        GameObject dim = CreateRect("Dim", transform, new Color(0.02f, 0.025f, 0.035f, 0.92f));
        Stretch(dim.GetComponent<RectTransform>());

        GameObject line = CreateRect("Accent", transform, new Color(0.35f, 0.6f, 1f, 0.95f));
        RectTransform lineRect = line.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.sizeDelta = new Vector2(520f, 4f);
        lineRect.anchoredPosition = new Vector2(0f, -34f);

        _titleText = CreateText("Title", transform, "매칭이 완료되어 던전으로 이동합니다", 48, FontStyle.Bold, new Vector2(0f, 42f), new Vector2(760f, 72f));
        _detailText = CreateText("Detail", transform, "전장에 입장하는 중입니다.", 26, FontStyle.Normal, new Vector2(0f, -88f), new Vector2(760f, 60f));
    }

    void SetText(string title, string detail)
    {
        if (!string.IsNullOrEmpty(title) && _titleText != null)
            _titleText.text = title;

        if (!string.IsNullOrEmpty(detail) && _detailText != null)
            _detailText.text = detail;
    }

    GameObject CreateRect(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        return go;
    }

    Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle style, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.text = value;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return text;
    }

    void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}


