using UnityEngine;

namespace Kimchily.Creator.Mobile
{
    /// <summary>Input uses finger IDs; IMGUI draws the overlay without owning pointer events.</summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Kimchily/Mobile Controls")]
    public sealed class KimchilyMobileControls : MonoBehaviour
    {
        private readonly MobilePlayerInputState input = new MobilePlayerInputState();
        private Texture2D circle;
        private GUIStyle jumpStyle;
        private Rect currentSafeArea;
        private int screenWidth;
        private int screenHeight;
        private Vector2 keyboardMove;
        private Vector2 lastMouse;
        private bool rightMouseHeld;
        public MobilePlayerInputState InputState => input;
        public MobileControlLayout Layout { get; private set; }
        public Vector2 Move => isActiveAndEnabled ? Vector2.ClampMagnitude(input.Move + keyboardMove, 1) : Vector2.zero;

        private void OnEnable() { RefreshLayout(Screen.safeArea); ClearInput(); }
        private void OnDisable() { ClearInput(); }
        private void OnApplicationPause(bool paused) { if (paused) ClearInput(); }
        private void OnApplicationFocus(bool focused) { if (!focused) ClearInput(); }
        public void ClearInput() { input.Clear(); keyboardMove = Vector2.zero; rightMouseHeld = false; }
        public void RefreshLayout(Rect safeArea)
        {
            if (safeArea.width <= 0 || safeArea.height <= 0) safeArea = new Rect(0, 0, Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            currentSafeArea = safeArea;
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            Layout = MobileControlLayout.Calculate(safeArea);
            ClearInput();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (screenWidth != Screen.width || screenHeight != Screen.height || currentSafeArea != Screen.safeArea) RefreshLayout(Screen.safeArea);
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                switch (touch.phase)
                {
                    case TouchPhase.Began: input.BeginPointer(touch.fingerId, touch.position, Layout); break;
                    case TouchPhase.Moved: input.MovePointer(touch.fingerId, touch.position, Layout); break;
                    case TouchPhase.Ended: input.EndPointer(touch.fingerId); break;
                    case TouchPhase.Canceled: input.EndPointer(touch.fingerId, true); break;
                }
            }
            keyboardMove = Vector2.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) keyboardMove.y += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) keyboardMove.y -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keyboardMove.x += 1;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keyboardMove.x -= 1;
            if (Input.GetKeyDown(KeyCode.Space)) input.RequestJump();
            if (Input.touchCount == 0)
            {
                Vector2 mouse = Input.mousePosition;
                if (Input.GetMouseButtonDown(0)) input.BeginPointer(-1, mouse, Layout);
                if (Input.GetMouseButton(0)) input.MovePointer(-1, mouse, Layout);
                if (Input.GetMouseButtonUp(0)) input.EndPointer(-1);
                if (Input.GetMouseButtonDown(1) && Layout.Look.Contains(mouse)) { rightMouseHeld = true; lastMouse = mouse; }
                if (rightMouseHeld && Input.GetMouseButton(1)) { input.AddLookDelta(mouse - lastMouse); lastMouse = mouse; }
                if (Input.GetMouseButtonUp(1)) rightMouseHeld = false;
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || Event.current.type != EventType.Repaint) return;
            if (circle == null) CreateCircle();
            Color previous = GUI.color;
            int previousDepth = GUI.depth;
            GUI.depth = -100;
            DrawCircle(Layout.Joystick, new Color(.02f, .07f, .09f, .68f));
            float knobSize = Layout.Joystick.width * .4f;
            Vector2 center = Layout.Joystick.center + input.Move * Layout.JoystickRadius;
            DrawCircle(new Rect(center.x - knobSize / 2, center.y - knobSize / 2, knobSize, knobSize), new Color(.48f, .95f, .83f, .96f));
            DrawCircle(Layout.Jump, input.IsJumpPressed ? new Color(.95f, .77f, .31f, 1) : new Color(.08f, .25f, .28f, .92f));
            if (jumpStyle == null) jumpStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            jumpStyle.fontSize = Mathf.RoundToInt(Layout.Jump.width * .22f);
            jumpStyle.normal.textColor = Color.white;
            GUI.color = Color.white;
            GUI.Label(ToGui(Layout.Jump), "JUMP", jumpStyle);
            GUI.color = previous;
            GUI.depth = previousDepth;
        }

        private static Rect ToGui(Rect rect) => new Rect(rect.x, Screen.height - rect.yMax, rect.width, rect.height);
        private void DrawCircle(Rect rect, Color color) { GUI.color = color; GUI.DrawTexture(ToGui(rect), circle); }
        private void CreateCircle()
        {
            const int size = 64;
            circle = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Kimchily Mobile Control", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x - (size - 1) * .5f, y - (size - 1) * .5f).magnitude;
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(size * .5f - distance));
            }
            circle.SetPixels(pixels); circle.Apply(false, true);
        }
        private void OnDestroy() { if (circle != null) Destroy(circle); }
    }
}
