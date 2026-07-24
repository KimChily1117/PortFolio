using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ClientDisconnectNotifier : MonoBehaviour
{
    const float QuitDelaySeconds = 3f;
    static bool s_shown;

    public static void ShowAndQuit(string endPoint)
    {
        if (s_shown)
            return;

        s_shown = true;
        SceneLoadingOverlay.Hide();
        GameManager.Input.SetInputLocked(true);
        GameObject root = new GameObject("@ServerDisconnectNotifier");
        DontDestroyOnLoad(root);
        ClientDisconnectNotifier notifier = root.AddComponent<ClientDisconnectNotifier>();
        notifier.Build(endPoint);
        notifier.StartCoroutine(notifier.QuitAfterDelay());
    }

    void Build(string endPoint)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;
        gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject dim = CreateRect("Dim", transform, new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.GetComponent<RectTransform>());

        GameObject panel = CreateRect("Panel", transform, new Color(0.08f, 0.09f, 0.12f, 0.96f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(720f, 300f);
        panelRect.anchoredPosition = Vector2.zero;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.45f, 0.75f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        CreateText("Title", panel.transform, "서버 연결이 끊겼습니다", 42, FontStyle.Bold, new Vector2(0f, 78f), new Vector2(640f, 64f));
        string detail = string.IsNullOrEmpty(endPoint) ? "게임 서버와의 연결을 잃었습니다." : $"게임 서버와의 연결을 잃었습니다.\nEndpoint: {endPoint}";
        CreateText("Detail", panel.transform, detail, 26, FontStyle.Normal, new Vector2(0f, -8f), new Vector2(640f, 92f));
        CreateText("Countdown", panel.transform, "3초 후 클라이언트를 종료합니다.", 24, FontStyle.Normal, new Vector2(0f, -104f), new Vector2(640f, 42f));
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

    IEnumerator QuitAfterDelay()
    {
        yield return new WaitForSecondsRealtime(QuitDelaySeconds);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}


