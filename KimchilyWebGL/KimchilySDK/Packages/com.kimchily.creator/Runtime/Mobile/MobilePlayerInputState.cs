using UnityEngine;

namespace Kimchily.Creator.Mobile
{
    /// <summary>Screen coordinates use Unity's bottom-left origin, including safeArea.</summary>
    public struct MobileControlLayout
    {
        public Rect SafeArea;
        public Rect Joystick;
        public Rect Jump;
        public Rect Look;
        public float JoystickRadius => Joystick.width * .34f;

        public static MobileControlLayout Calculate(Rect safeArea)
        {
            float size = Mathf.Clamp(Mathf.Min(safeArea.width, safeArea.height) * .26f, 88, 260);
            size = Mathf.Min(size, safeArea.width * .36f, safeArea.height * .4f);
            float margin = size * .18f;
            float jumpSize = size * .66f;
            return new MobileControlLayout
            {
                SafeArea = safeArea,
                Joystick = new Rect(safeArea.x + margin, safeArea.y + margin, size, size),
                Jump = new Rect(safeArea.xMax - margin - jumpSize, safeArea.y + margin, jumpSize, jumpSize),
                // Native host header/exit controls occupy the upper part of the screen.
                Look = new Rect(safeArea.x + safeArea.width * .5f, safeArea.y,
                    safeArea.width * .5f, safeArea.height * .85f)
            };
        }
    }

    /// <summary>Independent finger ownership permits walking, looking and jumping together.</summary>
    public sealed class MobilePlayerInputState
    {
        private const int NoPointer = int.MinValue;
        private int movePointer = NoPointer;
        private int lookPointer = NoPointer;
        private int jumpPointer = NoPointer;
        private Vector2 lastLook;
        private Vector2 lookDelta;
        private bool jumpPending;
        public Vector2 Move { get; private set; }
        public bool IsMovingPointer => movePointer != NoPointer;
        public bool IsJumpPressed => jumpPointer != NoPointer;

        public void BeginPointer(int id, Vector2 position, MobileControlLayout layout)
        {
            if (!layout.SafeArea.Contains(position)) return;
            if (layout.Joystick.Contains(position) && movePointer == NoPointer)
            {
                movePointer = id;
                UpdateMove(position, layout);
            }
            else if (layout.Jump.Contains(position) && jumpPointer == NoPointer)
            {
                jumpPointer = id;
                jumpPending = true;
            }
            else if (layout.Look.Contains(position) && lookPointer == NoPointer)
            {
                lookPointer = id;
                lastLook = position;
            }
        }

        public void MovePointer(int id, Vector2 position, MobileControlLayout layout)
        {
            if (id == movePointer) UpdateMove(position, layout);
            if (id == lookPointer) { lookDelta += position - lastLook; lastLook = position; }
        }

        public void EndPointer(int id, bool cancelled = false)
        {
            if (id == movePointer) { movePointer = NoPointer; Move = Vector2.zero; }
            if (id == lookPointer) { lookPointer = NoPointer; if (cancelled) lookDelta = Vector2.zero; }
            if (id == jumpPointer) { jumpPointer = NoPointer; if (cancelled) jumpPending = false; }
        }

        public void AddLookDelta(Vector2 delta) { lookDelta += delta; }
        public void RequestJump() { jumpPending = true; }
        public bool ConsumeJump() { bool result = jumpPending; jumpPending = false; return result; }
        public Vector2 ConsumeLook() { Vector2 result = lookDelta; lookDelta = Vector2.zero; return result; }
        public void Clear()
        {
            movePointer = lookPointer = jumpPointer = NoPointer;
            Move = lookDelta = Vector2.zero;
            jumpPending = false;
        }

        private void UpdateMove(Vector2 position, MobileControlLayout layout)
        {
            Vector2 value = Vector2.ClampMagnitude((position - layout.Joystick.center) / Mathf.Max(1, layout.JoystickRadius), 1);
            Move = value.magnitude < .08f ? Vector2.zero : value;
        }
    }
}
