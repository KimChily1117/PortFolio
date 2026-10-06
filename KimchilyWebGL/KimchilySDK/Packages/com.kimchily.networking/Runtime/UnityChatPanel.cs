using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kimchily.Networking
{
    /// <summary>Canvas/TMP UI shared by Editor, native players and WebGL.</summary>
    public sealed class UnityChatPanel : MonoBehaviour
    {
        public TMP_InputField MessageInput { get; private set; }
        public Canvas Canvas { get; private set; }

        public static TMP_FontAsset Font
        {
            get
            {
                return Resources.Load<TMP_FontAsset>("KimchilyChatFont") ?? TMP_Settings.defaultFontAsset;
            }
        }

        InGameChat chat;
        RectTransform safe;
        RectTransform panel;
        GameObject settings;
        GameObject compose;
        TMP_InputField nickname;
        TMP_InputField endpoint;
        TMP_InputField room;
        TextMeshProUGUI title;
        TextMeshProUGUI status;
        TextMeshProUGUI members;
        TextMeshProUGUI history;
        TextMeshProUGUI toggleText;
        Button reconnect;
        ScrollRect scroll;
        string lastHistory;
        Rect lastSafe;
        Vector2 lastSize;

        public void Initialize(InGameChat owner)
        {
            chat = owner;
            var canvasRoot = Node("Kimchily Chat Canvas", transform);
            Canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 1000;

            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 600);
            scaler.matchWidthOrHeight = 1;
            canvasRoot.gameObject.AddComponent<GraphicRaycaster>();

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Kimchily UI Events", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }

            safe = Node("Safe Area", canvasRoot);
            Stretch(safe);
            var toggle = MakeButton("Chat Toggle", safe, "채팅", () => chat.SetExpanded(chat.View.expanded ? "false" : "true"));
            TopRight(toggle.GetComponent<RectTransform>(), new Vector2(132, 38), new Vector2(-12, -62));
            toggleText = toggle.GetComponentInChildren<TextMeshProUGUI>();

            panel = Node("Chat Panel", safe);
            Paint(panel, new Color(.04f, .13f, .14f, .96f));
            TopRight(panel, new Vector2(370, 390), new Vector2(-12, -107));
            title = Label("Title", panel, "월드 채팅", 21);
            Place(title.rectTransform, 16, 8, 335, 36);
            status = Label("Status", panel, "", 14);
            Place(status.rectTransform, 16, 43, 335, 40);
            status.color = new Color(.78f, .96f, .61f);
            members = Label("Members", panel, "", 13);
            Place(members.rectTransform, 16, 86, 335, 42);

            settings = Node("Connection Settings", panel).gameObject;
            Place((RectTransform)settings.transform, 16, 125, 335, 185);
            nickname = Input("Nickname", settings.transform, "닉네임 (1–24자)", 24);
            Place(nickname.GetComponent<RectTransform>(), 0, 0, 335, 43);

            room = Input("Room", settings.transform, "방 코드", 64);
            Place(room.GetComponent<RectTransform>(), 0, 49, 335, 43);
            room.text = "playground";

            endpoint = Input("Server", settings.transform, "서버 WS/WSS 주소", 256);
            Place(endpoint.GetComponent<RectTransform>(), 0, 98, 335, 43);
            endpoint.text = chat.View.settings.endpoint;

            reconnect = MakeButton("Reconnect", settings.transform, "연결하기", () =>
            {
                chat.Connect(JsonUtility.ToJson(new ChatSettings
                {
                    name = nickname.text.Trim(),
                    endpoint = endpoint.text.Trim(),
                    roomId = room.text.Trim()
                }));
            });
            Place(reconnect.GetComponent<RectTransform>(), 0, 147, 335, 36);

            var viewport = Node("Chat Viewport", panel);
            Place(viewport, 16, 125, 335, 190);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;

            // A transparent graphic lets the scroll view receive drag events.
            Paint(viewport, new Color(0, 0, 0, .01f));
            history = Label("Chat History", viewport, "", 17);
            history.alignment = TextAlignmentOptions.TopLeft;
            history.rectTransform.anchorMin = new Vector2(0, 1);
            history.rectTransform.anchorMax = Vector2.one;
            history.rectTransform.pivot = new Vector2(.5f, 1);
            history.rectTransform.sizeDelta = new Vector2(0, 190);
            scroll.content = history.rectTransform;

            compose = Node("Compose", panel).gameObject;
            var composeRect = (RectTransform)compose.transform;
            composeRect.anchorMin = new Vector2(0, 0);
            composeRect.anchorMax = new Vector2(1, 0);
            composeRect.pivot = new Vector2(.5f, 0);
            composeRect.offsetMin = new Vector2(16, 16);
            composeRect.offsetMax = new Vector2(-16, 63);

            MessageInput = Input("Chat Message", compose.transform, "메시지를 입력하세요", 300);
            var inputRect = MessageInput.GetComponent<RectTransform>();
            Stretch(inputRect);
            inputRect.offsetMax = new Vector2(-73, 0);

            var send = MakeButton("Send Chat", compose.transform, "전송", Send);
            var sendRect = send.GetComponent<RectTransform>();
            sendRect.anchorMin = new Vector2(1, 0);
            sendRect.anchorMax = Vector2.one;
            sendRect.pivot = new Vector2(1, .5f);
            sendRect.sizeDelta = new Vector2(66, 0);
            sendRect.anchoredPosition = Vector2.zero;

            MessageInput.onSubmit.AddListener(_ => Send());
            chat.Changed += Refresh;
            Refresh();
        }

        void Send()
        {
            if (!chat.View.joined || !ChatValidation.IsText(MessageInput.text.Trim(), 300))
            {
                return;
            }

            chat.SendChat(MessageInput.text);
            MessageInput.text = "";
            MessageInput.ActivateInputField();
        }

        void Refresh()
        {
            var view = chat.View;
            panel.gameObject.SetActive(view.expanded);
            toggleText.text = view.expanded ? "채팅 닫기" : "채팅 · " + view.players.Length;
            title.text = view.worldId == "lobby" ? "월드 입장 준비" : "월드 채팅";
            status.text = view.status;
            if (view.joined)
            {
                members.text = view.settings.roomId + " · " + view.players.Length + "/8  "
                    + string.Join(" · ", System.Array.ConvertAll(view.players, p => p.name));
            }
            else
            {
                members.text = view.worldId + " / " + view.revisionId;
            }

            settings.SetActive(!view.joined && !view.connecting);
            compose.SetActive(view.joined);
            scroll.gameObject.SetActive(view.joined);
            if (!nickname.isFocused && !string.IsNullOrEmpty(view.settings.name))
            {
                nickname.SetTextWithoutNotify(view.settings.name);
            }

            if (!endpoint.isFocused)
            {
                endpoint.SetTextWithoutNotify(view.settings.endpoint);
            }

            if (!room.isFocused)
            {
                room.SetTextWithoutNotify(view.settings.roomId);
            }

            var text = new StringBuilder();
            int first = Mathf.Max(0, view.messages.Length - 40);
            for (int i = first; i < view.messages.Length; i++)
            {
                text.Append(view.messages[i].name).Append(": ").Append(view.messages[i].text).Append("\n\n");
            }

            string content = text.ToString();
            if (content != lastHistory)
            {
                lastHistory = content;
                history.text = content;
                ResizeHistory();
                scroll.verticalNormalizedPosition = 0;
            }

            MessageInput.interactable = view.joined;
        }

        void Update()
        {
            var size = new Vector2(Screen.width, Screen.height);
            if (lastSafe == Screen.safeArea && lastSize == size)
            {
                return;
            }

            lastSafe = Screen.safeArea;
            lastSize = size;
            if (size.x <= 0 || size.y <= 0)
            {
                return;
            }

            safe.anchorMin = lastSafe.min / size;
            safe.anchorMax = lastSafe.max / size;
            UnityEngine.Canvas.ForceUpdateCanvases();
            float width = Mathf.Min(370, safe.rect.width - 24);
            float height = Mathf.Min(440, safe.rect.height - 120);
            panel.sizeDelta = new Vector2(width, Mathf.Max(260, height));
            foreach (var text in new[] { title, status, members })
            {
                text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 32);
            }

            settings.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 32);
            foreach (RectTransform child in settings.transform)
            {
                child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 32);
            }

            scroll.GetComponent<RectTransform>().sizeDelta = new Vector2(width - 32, Mathf.Max(60, height - 205));
            ResizeHistory();
        }

        void ResizeHistory()
        {
            history.ForceMeshUpdate();
            history.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(scroll.viewport.rect.height, history.preferredHeight + 10));
        }

        void OnDestroy()
        {
            if (chat != null)
            {
                chat.Changed -= Refresh;
            }
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(parent, false);
            return node;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        static void TopRight(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        public static Image Paint(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text, int size)
        {
            var label = Node(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Font;
            label.fontSize = size;
            label.text = text;
            label.color = new Color(.95f, .98f, .95f);
            label.richText = false;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }

        static Button MakeButton(string name, Transform parent, string text, UnityEngine.Events.UnityAction action)
        {
            var rect = Node(name, parent);
            var image = Paint(rect, new Color(.16f, .35f, .31f));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            var label = Label("Label", rect, text, 17);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        static TMP_InputField Input(string name, Transform parent, string placeholder, int limit)
        {
            var root = Node(name, parent);
            var image = Paint(root, new Color(.02f, .08f, .09f));
            var field = root.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = image;

            var viewport = Node("Text Area", root);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(10, 5);
            viewport.offsetMax = new Vector2(-10, -5);
            viewport.gameObject.AddComponent<RectMask2D>();

            var text = Label("Text", viewport, "", 18);
            Stretch(text.rectTransform);
            text.overflowMode = TextOverflowModes.Overflow;

            var hint = Label("Placeholder", viewport, placeholder, 16);
            Stretch(hint.rectTransform);
            hint.color = new Color(.6f, .73f, .69f);

            field.textViewport = viewport;
            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = limit;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.richText = false;
            field.customCaretColor = true;
            field.caretColor = Color.white;
            field.selectionColor = new Color(.45f, .8f, .6f, .4f);
            return field;
        }
    }
}
