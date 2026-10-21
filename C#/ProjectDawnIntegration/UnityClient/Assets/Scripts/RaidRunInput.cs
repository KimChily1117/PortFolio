namespace DnfRaidDemo
{
    // Sample once per rendered frame, before fixed ticks. Run is a held network state so a
    // lost/reordered movement packet cannot lose the second tap or fabricate another tap.
    public sealed class RaidRunInput
    {
        public const float DoubleTapWindow = .25f;
        int lastTapDirection, runDirection;
        float lastTapAt = float.NegativeInfinity;
        public bool Running { get { return runDirection != 0; } }
        public void Reset() { lastTapDirection = runDirection = 0; lastTapAt = float.NegativeInfinity; }
        public void Sample(int heldDirection, int pressedDirection, float now)
        {
            if (heldDirection == 0 || heldDirection != runDirection) runDirection = 0;
            if (pressedDirection == 0) return;
            if (heldDirection == pressedDirection && lastTapDirection == pressedDirection
                && now >= lastTapAt && now - lastTapAt <= DoubleTapWindow) runDirection = pressedDirection;
            lastTapDirection = pressedDirection;
            lastTapAt = now;
        }
    }
}
