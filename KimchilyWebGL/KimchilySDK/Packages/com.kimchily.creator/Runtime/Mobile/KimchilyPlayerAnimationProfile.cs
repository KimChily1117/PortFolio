using UnityEngine;

namespace Kimchily.Creator.Mobile
{
    /// <summary>Per-model clip mapping. Generic clips must use this model's bone hierarchy.</summary>
    [CreateAssetMenu(fileName = "PlayerAnimationProfile", menuName = "Kimchily/Player Animation Profile")]
    public sealed class KimchilyPlayerAnimationProfile : ScriptableObject
    {
        [Header("Model animation clips")]
        public AnimationClip idle;
        public AnimationClip walk;
        public AnimationClip run;
        public AnimationClip jump;
        public AnimationClip fall;
        public AnimationClip land;

        [Header("Movement reference speeds (metres / second)")]
        [Min(.01f), Tooltip("The movement speed represented by one second of the Walk clip.")]
        public float walkSpeed = 2;
        [Min(.01f), Tooltip("The movement speed represented by one second of the Run clip.")]
        public float runSpeed = 5;
        [Min(0), Tooltip("Start blending Walk into Run above this movement speed; fully Run at Run Speed.")]
        public float runThreshold = 3;
        [Min(0), Tooltip("Actual movement at or below this speed targets Idle.")]
        public float idleThreshold = .08f;

        [Header("Transitions")]
        [Min(0), Tooltip("Seconds for a transition to reach approximately 95% of its target blend. Zero switches immediately.")]
        public float blendDuration = .15f;
        [Min(0), Tooltip("Play the optional Land clip once over this many seconds. Zero skips Land.")]
        public float landingDuration = .15f;

        /// <summary>
        /// Checks known runtime incompatibilities. A Generic clip's exact bone-path
        /// compatibility still requires authoring validation on the selected model.
        /// </summary>
        public static string GetClipCompatibilityError(Animator animator, AnimationClip clip)
        {
            if (clip == null) return null;
            if (clip.legacy) return "Legacy clips are unsupported; import this clip as Humanoid or Generic.";
            if (animator == null) return "The model has no Animator.";
            Avatar avatar = animator.avatar;
            bool humanoid = avatar != null && avatar.isValid && avatar.isHuman;
            if (clip.humanMotion && !humanoid)
                return "A Humanoid clip requires a valid Humanoid Avatar on the model Animator.";
            if (!clip.humanMotion && humanoid)
                return "Use Humanoid clips for this Humanoid model, or matching Generic clips with a Generic model.";
            if (avatar != null && !avatar.isValid)
                return "The model Animator has an invalid Avatar.";
            return null;
        }
    }
}
