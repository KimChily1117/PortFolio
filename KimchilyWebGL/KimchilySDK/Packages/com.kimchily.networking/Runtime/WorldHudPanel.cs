using System;
using System.Text;
using Kimchily.Creator.Mobile;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kimchily.Networking
{
    [Serializable]
    public sealed class WorldHudAction
    {
        public string id;
        public string label;
        public bool enabled;
    }

    [Serializable]
    public sealed class WorldHudModel
    {
        public string eyebrow;
        public string title;
        public string body;
        public string accent;
        public float progress;
        public WorldHudAction action;
    }

    /// <summary>
    /// TS가 작성한 데이터만 그리는 범용 TMP 패널이다. 게임 이름·상태·승패·서버 액션은 알지 못한다.
    /// 각 TS Behaviour가 자기 컴포넌트를 소유하므로 다른 스크립트의 HUD나 버튼 입력을 가져갈 수 없다.
    /// Unity 버튼은 VM을 직접 호출하지 않고 한 개의 액션 ID만 저장한다. 다음 TS Update에서 소비한다.
    /// </summary>
    public sealed class WorldHudPanel : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public Button ActionButton { get; private set; }
        public bool IsVisible { get; private set; }

        RectTransform safe;
        RectTransform card;
        RectTransform fill;
        TextMeshProUGUI headline;
        TextMeshProUGUI instruction;
        TextMeshProUGUI actionLabel;
        TextMeshProUGUI badge;
        Image accent;
        Image actionImage;
        Image progressImage;
        GameObject inputEvents;
        Rect lastSafe;
        Vector2 lastSize;
        WorldHudModel model;
        string lastJson;
        string pendingAction;

        [Serializable]
        sealed class JsonPropertyName
        {
            public string value = "";
        }

        public void ShowPanelJson(string json)
        {
            // 검증을 먼저 끝내서 잘못된 VM 입력이 Canvas 생성이나 기존 화면 변경을 유발하지 않게 한다.
            var next = ParseModel(json);
            if (!isActiveAndEnabled)
            {
                throw new InvalidOperationException("A disabled HUD cannot be shown.");
            }

            if (IsVisible && lastJson == json)
            {
                return;
            }

            EnsureCreated();
            pendingAction = null;
            lastJson = json;
            model = next;
            IsVisible = true;
            Canvas.gameObject.SetActive(true);
            ColorUtility.TryParseHtmlString(model.accent, out var color);
            accent.color = badge.color = actionImage.color = progressImage.color = color;
            badge.text = model.eyebrow;
            headline.text = model.title;
            instruction.text = model.body;
            fill.anchorMax = new Vector2(model.progress, 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            bool hasAction = model.action != null;
            ActionButton.gameObject.SetActive(hasAction);
            ActionButton.interactable = hasAction && model.action.enabled;
            actionLabel.text = hasAction ? model.action.label : "";
            RefreshLayout();
        }

        public string TakeAction()
        {
            // 조회만으로 UI를 만들지 않는다. 숨김/비활성 뒤 남은 클릭 역시 유효한 입력이 아니다.
            if (!IsVisible || !isActiveAndEnabled)
            {
                pendingAction = null;
                return null;
            }

            string result = pendingAction;
            pendingAction = null;
            return result;
        }

        public void Hide()
        {
            pendingAction = lastJson = null;
            model = null;
            IsVisible = false;
            if (Canvas != null)
            {
                Canvas.gameObject.SetActive(false);
            }
        }

        internal void QueueAction()
        {
            if (IsVisible
                && isActiveAndEnabled
                && model?.action != null
                && model.action.enabled
                && ActionButton != null
                && ActionButton.interactable)
            {
                pendingAction = model.action.id;
            }
        }

        static WorldHudModel ParseModel(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 4096 || Encoding.UTF8.GetByteCount(json) > 8192)
            {
                throw new ArgumentException("HUD JSON exceeds the limit.");
            }

            WorldHudModel value;
            try
            {
                value = JsonUtility.FromJson<WorldHudModel>(json);
            }
            catch (Exception error)
            {
                throw new ArgumentException("HUD JSON must be an object.", error);
            }

            if (value == null)
            {
                throw new ArgumentException("HUD JSON must be an object.");
            }

            value.eyebrow = value.eyebrow ?? "";
            value.body = value.body ?? "";
            value.accent = string.IsNullOrEmpty(value.accent) ? "#8CFAB8" : value.accent;
            if (!ValidText(value.title, 96, false) || !ValidText(value.eyebrow, 48, true) || !ValidText(value.body, 1200, true))
            {
                throw new ArgumentException("HUD text exceeds the limit or contains unsupported control characters.");
            }

            if (float.IsNaN(value.progress) || float.IsInfinity(value.progress) || value.progress < 0 || value.progress > 1)
            {
                throw new ArgumentException("HUD progress must be between 0 and 1.");
            }

            if (value.accent.Length != 7 || value.accent[0] != '#')
            {
                throw new ArgumentException("HUD accent must use #RRGGBB.");
            }

            for (int i = 1; i < value.accent.Length; i++)
            {
                if (!Uri.IsHexDigit(value.accent[i]))
                {
                    throw new ArgumentException("HUD accent must use #RRGGBB.");
                }
            }

            // JsonUtility는 생략/null인 중첩 클래스도 빈 인스턴스로 만들 수 있다.
            // 실제 JSON의 최상위 action 값을 확인하여 정상적인 무버튼 패널과 잘못된 {}를 구분한다.
            bool hasAction = HasNonNullAction(json);
            if (!hasAction)
            {
                value.action = null;
            }
            else if (value.action == null || !ChatValidation.IsId(value.action.id) || !ValidText(value.action.label, 48, false))
            {
                throw new ArgumentException("HUD action requires a bounded ID and label.");
            }

            return value;
        }

        static bool HasNonNullAction(string json)
        {
            int depth = 0;
            bool found = false;
            bool nonNull = false;
            for (int i = 0; i < json.Length; i++)
            {
                char current = json[i];
                if (current == '{' || current == '[')
                {
                    depth++;
                    continue;
                }

                if (current == '}' || current == ']')
                {
                    depth--;
                    continue;
                }

                if (current != '"')
                {
                    continue;
                }

                int start = i++;
                for (; i < json.Length; i++)
                {
                    if (json[i] == '\\')
                    {
                        i++;
                        continue;
                    }

                    if (json[i] == '"')
                    {
                        break;
                    }
                }

                if (i >= json.Length)
                {
                    throw new ArgumentException("HUD JSON contains an unterminated string.");
                }

                int colon = i + 1;
                while (colon < json.Length && char.IsWhiteSpace(json[colon]))
                {
                    colon++;
                }

                if (depth != 1 || colon >= json.Length || json[colon] != ':')
                {
                    continue;
                }

                // 문자열 내부의 \"action\":null이나 중첩 필드는 최상위 설정으로 오인하지 않는다.
                // 키의 유니코드 이스케이프 해석도 Unity와 일치시키기 위해 작은 문자열 DTO로 읽는다.
                string name = JsonUtility.FromJson<JsonPropertyName>("{\"value\":" + json.Substring(start, i - start + 1) + "}").value;
                if (name != "action")
                {
                    continue;
                }

                if (found)
                {
                    throw new ArgumentException("HUD action must appear only once.");
                }

                found = true;
                int begin = colon + 1;
                while (begin < json.Length && char.IsWhiteSpace(json[begin]))
                {
                    begin++;
                }

                int end = begin + 4;
                bool literalNull = end <= json.Length && string.CompareOrdinal(json, begin, "null", 0, 4) == 0;
                while (end < json.Length && char.IsWhiteSpace(json[end]))
                {
                    end++;
                }

                nonNull = !literalNull || end >= json.Length || (json[end] != ',' && json[end] != '}');
            }

            return found && nonNull;
        }

        static bool ValidText(string value, int limit, bool allowEmpty)
        {
            if (value == null || value.Length > limit || (!allowEmpty && string.IsNullOrWhiteSpace(value)))
            {
                return false;
            }

            foreach (char c in value)
            {
                if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                {
                    return false;
                }
            }

            return true;
        }

        void EnsureCreated()
        {
            if (Canvas != null)
            {
                return;
            }

            var root = UnityChatPanel.Node("Script HUD Canvas", transform);
            Canvas = root.gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 990;

            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 600);
            scaler.matchWidthOrHeight = 1;
            root.gameObject.AddComponent<GraphicRaycaster>();

            // 채팅이 없는 실행기에서도 uGUI 버튼을 사용할 수 있다. 이미 있는 EventSystem은 공유한다.
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                inputEvents = new GameObject("World HUD Input", typeof(EventSystem), typeof(StandaloneInputModule));
                inputEvents.transform.SetParent(transform, false);
            }

            safe = UnityChatPanel.Node("Safe Area", root);
            UnityChatPanel.Stretch(safe);

            card = UnityChatPanel.Node("Status", safe);
            Paint(card, new Color(.035f, .12f, .15f, .93f));

            var stripe = UnityChatPanel.Node("Accent", card);
            UnityChatPanel.Place(stripe, 0, 0, 4, 118);
            accent = Paint(stripe, Color.white);

            badge = UnityChatPanel.Label("Eyebrow", card, "", 10);
            badge.characterSpacing = 2;
            UnityChatPanel.Place(badge.rectTransform, 16, 9, 190, 15);

            headline = UnityChatPanel.Label("Title", card, "", 19);
            headline.enableAutoSizing = true;
            headline.fontSizeMin = 12;
            headline.fontSizeMax = 19;
            UnityChatPanel.Place(headline.rectTransform, 16, 25, 337, 36);

            instruction = UnityChatPanel.Label("Body", card, "", 12);
            instruction.color = new Color(.76f, .85f, .85f);
            // Noto CJK의 12pt 두 줄은 약 35px이다. 한글 두 번째 줄이 잘리지 않도록 여유를 둔다.
            UnityChatPanel.Place(instruction.rectTransform, 16, 62, 337, 38);

            var track = UnityChatPanel.Node("Progress Track", card);
            UnityChatPanel.Place(track, 16, 106, 337, 4);
            Paint(track, new Color(.16f, .28f, .30f));
            fill = UnityChatPanel.Node("Progress", track);
            UnityChatPanel.Stretch(fill);
            progressImage = Paint(fill, Color.white);

            var button = UnityChatPanel.Node("Action", safe);
            button.anchorMin = button.anchorMax = Vector2.zero;
            button.pivot = Vector2.zero;
            button.anchoredPosition = new Vector2(0, 24);
            button.sizeDelta = new Vector2(196, 43);
            actionImage = UnityChatPanel.Paint(button, Color.white);
            ActionButton = button.gameObject.AddComponent<Button>();
            ActionButton.targetGraphic = actionImage;
            ActionButton.onClick.AddListener(QueueAction);

            actionLabel = UnityChatPanel.Label("Action Label", button, "", 15);
            actionLabel.color = new Color(.03f, .17f, .14f);
            actionLabel.fontStyle = FontStyles.Bold;
            UnityChatPanel.Stretch(actionLabel.rectTransform);
            actionLabel.alignment = TextAlignmentOptions.Center;
        }

        static Image Paint(RectTransform rect, Color color)
        {
            var image = UnityChatPanel.Paint(rect, color);
            image.raycastTarget = false;
            return image;
        }

        void Update()
        {
            if (!IsVisible || Canvas == null)
            {
                return;
            }

            var size = new Vector2(Screen.width, Screen.height);
            if (lastSafe != Screen.safeArea || lastSize != size)
            {
                RefreshLayout();
            }
        }

        void RefreshLayout()
        {
            if (safe == null)
            {
                return;
            }

            lastSafe = Screen.safeArea;
            lastSize = new Vector2(Screen.width, Screen.height);
            if (lastSize.x <= 0 || lastSize.y <= 0)
            {
                return;
            }

            safe.anchorMin = lastSafe.min / lastSize;
            safe.anchorMax = lastSafe.max / lastSize;
            UnityEngine.Canvas.ForceUpdateCanvases();
            var statusRect = CalculateStatusRect(safe.rect.size);
            float width = statusRect.width;
            card.anchorMin = card.anchorMax = new Vector2(0, 1);
            card.pivot = new Vector2(0, 1);
            card.anchoredPosition = new Vector2(statusRect.x, -statusRect.y);
            card.sizeDelta = statusRect.size;
            foreach (var text in new[] { badge, headline, instruction })
            {
                text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 32);
            }

            ((RectTransform)fill.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 32);
            var button = ActionButton.GetComponent<RectTransform>();
            var actionRect = CalculateActionRect(lastSafe, Mathf.Max(.01f, Canvas.scaleFactor));
            button.anchoredPosition = actionRect.position;
            button.sizeDelta = actionRect.size;
            actionLabel.fontSize = button.rect.width < 175 ? 12 : 15;
        }

        internal static Rect CalculateStatusRect(Vector2 safeSize)
        {
            // 데스크톱의 중앙 영역은 캐릭터 머리 위 말풍선을 위해 비운다.
            if (safeSize.x >= 650)
            {
                return new Rect(12, 64, Mathf.Min(370, safeSize.x * .3f), 118);
            }

            float width = Mathf.Clamp(safeSize.x - 28, 180, 370);
            return new Rect((safeSize.x - width) * .5f, 112, width, 118);
        }

        internal static Rect CalculateActionRect(Rect safeArea, float canvasScale)
        {
            var controls = MobileControlLayout.Calculate(safeArea);
            float left = (controls.Joystick.xMax - safeArea.x) / canvasScale + 8;
            float right = (controls.Jump.xMin - safeArea.x) / canvasScale - 8;
            float safeWidth = safeArea.width / canvasScale;
            float width = Mathf.Min(196, right - left);
            if (width >= 110)
            {
                return new Rect((left + right - width) * .5f, 24, width, 43);
            }

            // 좁은 화면에서는 조이스틱/점프 입력을 가로채지 않도록 버튼을 컨트롤 위 줄로 옮긴다.
            width = Mathf.Min(196, Mathf.Max(1, safeWidth - 32));
            float bottom = (Mathf.Max(controls.Joystick.yMax, controls.Jump.yMax) - safeArea.y) / canvasScale + 12;
            return new Rect((safeWidth - width) * .5f, bottom, width, 43);
        }

        void OnDisable()
        {
            Hide();
        }

        void OnDestroy()
        {
            Hide();
            if (Canvas != null)
            {
                Destroy(Canvas.gameObject);
            }

            if (inputEvents != null)
            {
                Destroy(inputEvents);
            }
        }
    }
}
