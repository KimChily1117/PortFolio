using Kimchily.Creator.Mobile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kimchily.Networking
{
    /// <summary>Compact, safe-area-aware TMP HUD. The world supplies its own meshes and effects through TypeScript.</summary>
    public sealed class PortalGameHud : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public Button ActionButton { get; private set; }
        CoopPortalClient client;
        RectTransform safe, card, fill;
        TextMeshProUGUI headline, instruction, actionLabel, badge;
        Image accent, actionImage;
        Rect lastSafe;
        Vector2 lastSize;
        static readonly Color Mint = new Color(.55f, .98f, .72f);
        static readonly Color Gold = new Color(1f, .8f, .36f);

        public void Initialize(CoopPortalClient owner)
        {
            client = owner;
            var root = UnityChatPanel.Node("Chili Island HUD", transform);
            Canvas = root.gameObject.AddComponent<Canvas>(); Canvas.renderMode = RenderMode.ScreenSpaceOverlay; Canvas.sortingOrder = 990;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(960, 600); scaler.matchWidthOrHeight = 1;
            root.gameObject.AddComponent<GraphicRaycaster>();
            safe = UnityChatPanel.Node("Safe Area", root); UnityChatPanel.Stretch(safe);
            card = UnityChatPanel.Node("Portal Status", safe);
            Paint(card, new Color(.035f, .12f, .15f, .93f));
            var stripe = UnityChatPanel.Node("Accent", card); UnityChatPanel.Place(stripe, 0, 0, 4, 118); accent = Paint(stripe, Mint);
            badge = UnityChatPanel.Label("Island Badge", card, "CHILI ISLAND", 10);
            badge.characterSpacing = 2; badge.color = Mint; UnityChatPanel.Place(badge.rectTransform, 16, 9, 190, 15);
            headline = UnityChatPanel.Label("Round Status", card, "친구들과 포털을 켜 보세요", 19);
            headline.enableAutoSizing = true; headline.fontSizeMin = 12; headline.fontSizeMax = 19;
            UnityChatPanel.Place(headline.rectTransform, 16, 25, 337, 36);
            instruction = UnityChatPanel.Label("Instructions", card, "", 12); instruction.color = new Color(.76f, .85f, .85f);
            UnityChatPanel.Place(instruction.rectTransform, 16, 62, 337, 38);
            var track = UnityChatPanel.Node("Charge Track", card); UnityChatPanel.Place(track, 16, 106, 337, 4);
            Paint(track, new Color(.16f, .28f, .30f));
            fill = UnityChatPanel.Node("Charge", track); UnityChatPanel.Stretch(fill); Paint(fill, Mint);
            var button = UnityChatPanel.Node("Round Action", safe);
            button.anchorMin = button.anchorMax = Vector2.zero; button.pivot = Vector2.zero;
            button.anchoredPosition = new Vector2(0, 24); button.sizeDelta = new Vector2(196, 43);
            actionImage = UnityChatPanel.Paint(button, Mint);
            ActionButton = button.gameObject.AddComponent<Button>(); ActionButton.targetGraphic = actionImage;
            ActionButton.onClick.AddListener(OnAction);
            actionLabel = UnityChatPanel.Label("Label", button, "모두 준비됐어요 · 시작", 15);
            actionLabel.color = new Color(.03f, .17f, .14f); actionLabel.fontStyle = FontStyles.Bold;
            UnityChatPanel.Stretch(actionLabel.rectTransform); actionLabel.alignment = TextAlignmentOptions.Center;
            client.Changed += Refresh;
            Refresh();
        }
        static Image Paint(RectTransform rect, Color color)
        {
            var image = UnityChatPanel.Paint(rect, color); image.raycastTarget = false; return image;
        }
        void OnAction()
        {
            if (client.State == null) return;
            if (client.State.phase == "waiting") client.StartRound();
            else client.Replay();
        }
        void Refresh()
        {
            if (Canvas == null) return;
            Canvas.gameObject.SetActive(client.GameEnabled);
            if (!client.GameEnabled) return;
            var state = client.State;
            bool connected = client.Chat.View.joined;
            bool ready = connected && state != null;
            bool complete = ready && state.phase == "complete";
            bool waiting = ready && state.phase == "waiting";
            bool shortPlayers = ready && client.Chat.View.players.Length < state.requiredPlayers;
            ActionButton.gameObject.SetActive(ready && (waiting || complete || shortPlayers));
            ActionButton.interactable = ready;
            accent.color = badge.color = complete ? Gold : Mint;
            actionImage.color = complete ? Gold : Mint;
            badge.text = complete ? "CHILI ISLAND · CLEAR!" : "CHILI ISLAND";
            float charge = ready && state.phase == "holding" ? 1 - state.remainingMs / (state.holdSeconds * 1000f) : complete ? 1 : 0;
            fill.anchorMax = new Vector2(Mathf.Clamp01(charge), 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            if (!ready)
            {
                headline.text = connected ? "섬의 신호를 기다리고 있어요" : "친구들과 섬에 연결 중…";
                instruction.text = connected ? "곧 협동 플레이를 시작할 수 있어요" : "채팅의 연결 상태를 확인해 주세요";
                return;
            }
            int occupied = 0;
            foreach (var pad in state.pads) if (pad.active && !string.IsNullOrEmpty(pad.playerId)) occupied++;
            if (waiting)
            {
                headline.text = "모이면 시작! · " + state.requiredPlayers + "인 협동";
                instruction.text = "같은 QR로 입장한 뒤 시작을 눌러 주세요";
                actionLabel.text = "모두 준비됐어요 · 시작";
            }
            else if (complete)
            {
                headline.text = "포털이 열렸어요!";
                instruction.text = "모두 함께 빛나는 포털로 이동해 보세요";
                actionLabel.text = "한 번 더 플레이";
            }
            else if (state.phase == "holding")
            {
                headline.text = "그대로!  " + Mathf.CeilToInt(state.remainingMs / 1000f) + "초";
                instruction.text = "발판에서 내려가면 에너지가 초기화돼요";
            }
            else
            {
                headline.text = "서로 다른 발판으로 · " + occupied + "/" + state.requiredPlayers;
                instruction.text = shortPlayers ? "친구가 나갔어요 · 인원을 다시 맞춰 주세요" : "빛나는 발판을 하나씩 맡아 3초 지켜 주세요";
            }
            if (shortPlayers && !waiting && !complete) actionLabel.text = "인원 다시 맞추기";
        }
        void Update()
        {
            if (safe == null) return;
            var size = new Vector2(Screen.width, Screen.height);
            if (lastSafe == Screen.safeArea && lastSize == size) return;
            lastSafe = Screen.safeArea; lastSize = size;
            if (size.x <= 0 || size.y <= 0) return;
            safe.anchorMin = lastSafe.min / size; safe.anchorMax = lastSafe.max / size;
            UnityEngine.Canvas.ForceUpdateCanvases();
            var statusRect = CalculateStatusRect(safe.rect.size);
            float width = statusRect.width;
            card.anchorMin = card.anchorMax = new Vector2(0, 1); card.pivot = new Vector2(0, 1);
            card.anchoredPosition = new Vector2(statusRect.x, -statusRect.y); card.sizeDelta = statusRect.size;
            foreach (var text in new[] { headline, instruction }) text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 32);
            ((RectTransform)fill.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 32);
            var button = ActionButton.GetComponent<RectTransform>();
            var actionRect = CalculateActionRect(lastSafe, Mathf.Max(.01f, Canvas.scaleFactor));
            button.anchoredPosition = actionRect.position; button.sizeDelta = actionRect.size;
            actionLabel.fontSize = button.rect.width < 175 ? 12 : 15;
        }
        internal static Rect CalculateStatusRect(Vector2 safeSize)
        {
            // Leave the center column free for the portal, character name and world-space speech bubble.
            if (safeSize.x >= 650) return new Rect(12, 64, Mathf.Min(370, safeSize.x * .3f), 118);
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
            if (width >= 110) return new Rect((left + right - width) * .5f, 24, width, 43);
            // On very narrow devices use a separate row above both controls instead of intercepting movement.
            width = Mathf.Min(196, Mathf.Max(1, safeWidth - 32));
            float bottom = (Mathf.Max(controls.Joystick.yMax, controls.Jump.yMax) - safeArea.y) / canvasScale + 12;
            return new Rect((safeWidth - width) * .5f, bottom, width, 43);
        }
        void OnDestroy() { if (client != null) client.Changed -= Refresh; }
    }
}
