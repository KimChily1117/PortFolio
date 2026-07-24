using System;
using Google.Protobuf.Protocol;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ChannelSelectionOverlay : MonoBehaviour
{
    const string RoomsApiUrl = "http://127.0.0.1:8090/api/rooms";
    const int TownChannelCapacity = 50;

    static ChannelSelectionOverlay s_instance;

    Transform _listRoot;
    Text _statusText;
    Button _refreshButton;
    Button _closeButton;
    bool _isRefreshing;
    bool _isMoving;
    string _statusOverride;

    [Serializable]
    class RoomsArrayWrapper
    {
        public RoomDto[] rooms;
    }

    [Serializable]
    class RoomsValueWrapper
    {
        public RoomDto[] value;
    }

    [Serializable]
    class RoomDto
    {
        public int roomId;
        public string roomType;
        public string state;
        public int playerCount;
        public bool isDungeonRoom;
        public PlayerDto[] players;
    }

    [Serializable]
    class PlayerDto
    {
        public string name;
        public int objectId;
    }

    public static void Toggle()
    {
        EnsureInstance();
        if (s_instance.gameObject.activeSelf)
            s_instance.Hide();
        else
            s_instance.Show();
    }

    public static void HideIfVisible()
    {
        if (s_instance == null || !s_instance.gameObject.activeSelf)
            return;

        s_instance.Hide();
    }

    public static void HandleChannelMoveResult(S_ChannelMove result)
    {
        if (result == null)
            return;

        EnsureInstance();
        s_instance._isMoving = false;

        if (result.Success)
        {
            TownScene.Current?.BeginChannelReenter();
            GameManager.ObjectManager.RemoveRemoteObjects();
            s_instance._statusOverride = $"채널 이동 완료: Room {result.TargetRoomId}";
            s_instance.Refresh();
            return;
        }

        s_instance._statusOverride = $"채널 이동 실패: {FormatChannelMoveReason(result.Reason)}";
        if (s_instance.gameObject.activeSelf)
            s_instance.Refresh();
    }
    static void EnsureInstance()
    {
        if (s_instance != null)
            return;

        GameObject root = new GameObject("@ChannelSelectionOverlay", typeof(RectTransform));
        DontDestroyOnLoad(root);
        s_instance = root.AddComponent<ChannelSelectionOverlay>();
        s_instance.Build();
        root.SetActive(false);
    }

    void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    void Hide()
    {
        gameObject.SetActive(false);
    }

    void Refresh()
    {
        if (_isRefreshing)
            return;

        StartCoroutine(FetchRooms());
    }

    IEnumerator FetchRooms()
    {
        _isRefreshing = true;
        if (string.IsNullOrEmpty(_statusOverride))
            SetStatus("채널 정보를 불러오는 중입니다.");
        ClearList();

        using (UnityWebRequest request = UnityWebRequest.Get(RoomsApiUrl))
        {
            request.timeout = 3;
            yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool failed = request.result != UnityWebRequest.Result.Success;
#else
            bool failed = request.isNetworkError || request.isHttpError;
#endif
            if (failed)
            {
                SetStatus("채널 정보를 불러올 수 없습니다.");
                AddRow("Monitoring API 연결 실패", request.error ?? "Unknown error", false, false, 0);
                _isRefreshing = false;
                yield break;
            }

            RoomDto[] rooms = ParseRooms(request.downloadHandler.text);
            RenderRooms(rooms);
        }

        _isRefreshing = false;
    }

    RoomDto[] ParseRooms(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new RoomDto[0];

        string trimmed = json.Trim();
        try
        {
            if (trimmed.StartsWith("["))
            {
                RoomsArrayWrapper wrapper = JsonUtility.FromJson<RoomsArrayWrapper>($"{{\"rooms\":{trimmed}}}");
                return wrapper?.rooms ?? new RoomDto[0];
            }

            RoomsValueWrapper valueWrapper = JsonUtility.FromJson<RoomsValueWrapper>(trimmed);
            if (valueWrapper?.value != null)
                return valueWrapper.value;

            RoomsArrayWrapper roomsWrapper = JsonUtility.FromJson<RoomsArrayWrapper>(trimmed);
            return roomsWrapper?.rooms ?? new RoomDto[0];
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CHANNEL_UI] Failed to parse rooms json. Error={ex.Message}");
            return new RoomDto[0];
        }
    }

    void RenderRooms(RoomDto[] rooms)
    {
        ClearList();

        string myName = GameManager.MyName ?? string.Empty;
        int rendered = 0;
        int currentRoomId = FindCurrentRoomId(rooms, myName);

        foreach (RoomDto room in rooms ?? new RoomDto[0])
        {
            if (room == null || !string.Equals(room.roomType, "Town", StringComparison.OrdinalIgnoreCase))
                continue;

            rendered++;
            bool isCurrent = room.roomId == currentRoomId;
            bool isFull = room.playerCount >= TownChannelCapacity;
            string label = $"Town CH {rendered}  Room {room.roomId}";
            string detail = $"{room.playerCount}/{TownChannelCapacity}명  {GetCongestion(room.playerCount)}";
            AddRow(label, detail, isCurrent, isFull, room.roomId);
        }

        if (rendered == 0)
        {
            SetStatus("표시할 Town 채널이 없습니다.");
            AddRow("Town 채널 없음", "서버 상태를 확인해주세요.", false, false, 0);
            return;
        }

        if (!string.IsNullOrEmpty(_statusOverride))
        {
            SetStatus(_statusOverride);
            _statusOverride = null;
        }
        else
        {
            SetStatus(currentRoomId > 0 ? $"현재 채널: Room {currentRoomId}" : "현재 채널을 찾는 중입니다.");
        }
        RebuildListLayout();
    }

    int FindCurrentRoomId(RoomDto[] rooms, string myName)
    {
        if (string.IsNullOrEmpty(myName))
            return 0;

        foreach (RoomDto room in rooms ?? new RoomDto[0])
        {
            foreach (PlayerDto player in room.players ?? new PlayerDto[0])
            {
                if (player != null && string.Equals(player.name, myName, StringComparison.Ordinal))
                    return room.roomId;
            }
        }

        return 0;
    }

    string GetCongestion(int playerCount)
    {
        if (playerCount >= TownChannelCapacity)
            return "Full";
        if (playerCount >= 40)
            return "Busy";
        if (playerCount >= 20)
            return "Normal";
        return "Comfortable";
    }

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 8500;

        gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        GameObject dim = CreateRect("Dim", transform, new Color(0f, 0f, 0f, 0.52f));
        Stretch(dim.GetComponent<RectTransform>());

        GameObject panel = CreateRect("Panel", transform, new Color(0.075f, 0.085f, 0.105f, 0.97f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 580f);
        panelRect.anchoredPosition = Vector2.zero;

        CreateText("Title", panel.transform, "채널 선택", 36, FontStyle.Bold, new Vector2(-210f, 238f), new Vector2(280f, 52f), TextAnchor.MiddleLeft);
        _statusText = CreateText("Status", panel.transform, "채널 정보를 불러오는 중입니다.", 22, FontStyle.Normal, new Vector2(-132f, 190f), new Vector2(436f, 42f), TextAnchor.MiddleLeft);

        _closeButton = CreateButton("Close", panel.transform, "닫기", new Vector2(286f, 238f), new Vector2(118f, 44f));
        _closeButton.onClick.AddListener(Hide);

        _refreshButton = CreateButton("Refresh", panel.transform, "새로고침", new Vector2(152f, 238f), new Vector2(132f, 44f));
        _refreshButton.onClick.AddListener(Refresh);

        GameObject list = new GameObject("List", typeof(RectTransform));
        list.transform.SetParent(panel.transform, false);
        _listRoot = list.transform;
        RectTransform listRect = list.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.5f, 0.5f);
        listRect.anchorMax = new Vector2(0.5f, 0.5f);
        listRect.pivot = new Vector2(0.5f, 1f);
        listRect.sizeDelta = new Vector2(660f, 390f);
        listRect.anchoredPosition = new Vector2(0f, 146f);

        VerticalLayoutGroup layout = list.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 10f;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
    }

    void AddRow(string label, string detail, bool isCurrent, bool isFull, int targetRoomId)
    {
        if (_listRoot == null)
            return;

        GameObject row = CreateRect("ChannelRow", _listRoot, isCurrent ? new Color(0.13f, 0.22f, 0.32f, 0.96f) : new Color(0.12f, 0.13f, 0.15f, 0.96f));
        row.transform.SetParent(_listRoot, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, 72f);
        rowRect.anchoredPosition = Vector2.zero;

        LayoutElement layout = row.AddComponent<LayoutElement>();
        layout.preferredHeight = 72f;
        layout.minHeight = 72f;

        CreateText("Label", row.transform, isCurrent ? $"{label}  현재" : label, 24, FontStyle.Bold, new Vector2(-184f, 12f), new Vector2(280f, 34f), TextAnchor.MiddleLeft);
        CreateText("Detail", row.transform, detail, 19, FontStyle.Normal, new Vector2(-184f, -18f), new Vector2(280f, 28f), TextAnchor.MiddleLeft);

        string buttonLabel = isCurrent ? "접속 중" : (isFull ? "가득 참" : (_isMoving ? "이동 중" : "이동"));
        Button button = CreateButton("Move", row.transform, buttonLabel, new Vector2(224f, 0f), new Vector2(150f, 42f));
        button.interactable = !isCurrent && !isFull && !_isMoving && targetRoomId > 0;
        if (button.interactable)
        {
            int roomId = targetRoomId;
            button.onClick.AddListener(() => RequestChannelMove(roomId));
        }

        RebuildListLayout();
    }

    void RequestChannelMove(int targetRoomId)
    {
        if (_isMoving || targetRoomId <= 0)
            return;

        _isMoving = true;
        SetStatus($"Room {targetRoomId} 채널로 이동 중입니다.");
        C_ChannelMove request = new C_ChannelMove { TargetRoomId = targetRoomId };
        GameManager.Network.Send(request);
        Refresh();
    }

    static string FormatChannelMoveReason(string reason)
    {
        switch (reason)
        {
            case "AlreadyInChannel": return "이미 접속 중인 채널입니다.";
            case "ChannelFull": return "채널이 가득 찼습니다.";
            case "InvalidTarget": return "잘못된 채널입니다.";
            case "NotInTown": return "마을에서만 채널 이동이 가능합니다.";
            case "TargetNotFound": return "채널을 찾을 수 없습니다.";
            case "TargetNotTown": return "마을 채널이 아닙니다.";
            case "RoomChanged": return "현재 위치가 바뀌었습니다. 다시 시도해주세요.";
            default: return string.IsNullOrWhiteSpace(reason) ? "Unknown" : reason;
        }
    }

    Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size)
    {
        GameObject go = CreateRect(name, parent, new Color(0.18f, 0.29f, 0.42f, 1f));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Button button = go.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.16f, 0.17f, 0.19f, 0.75f);
        colors.highlightedColor = new Color(0.24f, 0.38f, 0.56f, 1f);
        button.colors = colors;

        CreateText("Text", go.transform, label, 20, FontStyle.Bold, Vector2.zero, size, TextAnchor.MiddleCenter);
        return button;
    }

    Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle style, Vector2 position, Vector2 size, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.text = value;
        text.alignment = anchor;
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

    GameObject CreateRect(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        return go;
    }

    void ClearList()
    {
        if (_listRoot == null)
            return;

        for (int i = _listRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = _listRoot.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        RebuildListLayout();
    }

    void RebuildListLayout()
    {
        RectTransform rect = _listRoot as RectTransform;
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    void SetStatus(string value)
    {
        if (_statusText != null)
            _statusText.text = value;
    }

    void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}




