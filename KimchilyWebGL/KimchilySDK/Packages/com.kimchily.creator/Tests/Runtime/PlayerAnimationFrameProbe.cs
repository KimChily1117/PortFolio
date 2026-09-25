using Kimchily.Creator.Mobile;
using UnityEngine;

namespace Kimchily.Creator.Tests
{
    /// <summary>Observes rendered-frame pose after the engine's animation update.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class PlayerAnimationFrameProbe : MonoBehaviour
    {
        public KimchilyPlayerAnimationDriver Driver;
        public Transform Target;
        public float PlanarSpeed;
        public float LastLatePosition { get; private set; }
        public int ObservedFrames { get; private set; }

        void Update() { Driver?.Tick(PlanarSpeed, true, 0, Time.deltaTime); }
        void LateUpdate()
        {
            if (Target == null) return;
            LastLatePosition = Target.localPosition.x;
            ObservedFrames++;
        }
    }
}
