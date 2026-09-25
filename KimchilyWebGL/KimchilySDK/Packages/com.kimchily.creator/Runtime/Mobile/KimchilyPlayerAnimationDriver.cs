using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Kimchily.Creator.Mobile
{
    /// <summary>
    /// Main-thread, manually evaluated clip mixer. The caller owns movement and must
    /// call Dispose before destroying/replacing its visual model or disabling play.
    /// No AnimatorController parameters, transitions or asset mutations are required.
    /// </summary>
    public sealed class KimchilyPlayerAnimationDriver : IDisposable
    {
        const int Idle = 0, Walk = 1, Run = 2, Jump = 3, Fall = 4, Land = 5, ClipCount = 6;
        static readonly string[] StateNames = { "Idle", "Walk", "Run", "Jump", "Fall", "Land" };
        readonly AnimationClip[] clips = new AnimationClip[ClipCount];
        readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[ClipCount];
        readonly double[] times = new double[ClipCount];
        readonly float[] weights = new float[ClipCount];
        readonly float[] targets = new float[ClipCount];
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        Animator animator;
        KimchilyPlayerAnimationProfile profile;
        RuntimeAnimatorController originalController;
        AnimatorCullingMode originalCullingMode;
        bool originalRootMotion;
        bool hasGroundSample;
        bool previousGrounded;
        float landingRemaining;
        int state = Idle;

        public bool IsInitialized => graph.IsValid() && animator != null && profile != null;
        /// <summary>Target movement state; blends may still contain the previous pose.</summary>
        public string CurrentState { get; private set; } = "Disabled";
        /// <summary>Last actual planar movement speed, in metres per second.</summary>
        public float CurrentSpeed { get; private set; }
        /// <summary>Configuration failure, or warnings about ignored incompatible clips.</summary>
        public string LastError { get; private set; } = string.Empty;

        public bool Initialize(Animator targetAnimator, KimchilyPlayerAnimationProfile animationProfile)
        {
            Dispose();
            LastError = string.Empty;
            if (targetAnimator == null || animationProfile == null)
            {
                LastError = targetAnimator == null ? "The model has no Animator." : "No animation profile is assigned.";
                return false;
            }

            AnimationClip[] assigned = { animationProfile.idle, animationProfile.walk, animationProfile.run,
                animationProfile.jump, animationProfile.fall, animationProfile.land };
            var warnings = new List<string>();
            int available = 0;
            for (int index = 0; index < ClipCount; index++)
            {
                string error = KimchilyPlayerAnimationProfile.GetClipCompatibilityError(targetAnimator, assigned[index]);
                if (error != null) warnings.Add(StateNames[index] + ": " + error);
                clips[index] = error == null ? assigned[index] : null;
                if (clips[index] != null) available++;
            }
            LastError = string.Join("\n", warnings);
            if (available == 0)
            {
                if (LastError.Length == 0) LastError = "Assign at least one compatible animation clip.";
                return false;
            }

            animator = targetAnimator;
            profile = animationProfile;
            originalController = animator.runtimeAnimatorController;
            originalCullingMode = animator.cullingMode;
            originalRootMotion = animator.applyRootMotion;
            Transform animatorRoot = animator.transform;
            Vector3 mountPosition = animatorRoot.localPosition;
            Quaternion mountRotation = animatorRoot.localRotation;
            Vector3 mountScale = animatorRoot.localScale;
            try
            {
                // A manually evaluated graph must own this Animator exclusively.
                // Leaving its controller attached lets Unity's later animation
                // pass overwrite the mapped Humanoid pose after Tick returns.
                // Save the reference, detach the controller, then configure the
                // Animator before creating our graph (these setters can rebind).
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Kimchily Player Animation");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                mixer = AnimationMixerPlayable.Create(graph, ClipCount);
                for (int index = 0; index < ClipCount; index++)
                {
                    if (clips[index] == null) continue;
                    playables[index] = AnimationClipPlayable.Create(graph, clips[index]);
                    playables[index].SetApplyFootIK(false);
                    playables[index].SetApplyPlayableIK(false);
                    // Tick owns clip clocks, including looping non-loop-tagged
                    // locomotion clips without modifying their imported assets.
                    playables[index].SetSpeed(0);
                    graph.Connect(playables[index], 0, mixer, index);
                }
                var output = AnimationPlayableOutput.Create(graph, "Player Pose", animator);
                output.SetSourcePlayable(mixer);
                weights[Resolve(Idle)] = 1;
                ApplyWeights();
                CurrentState = StateNames[Idle];
                graph.Play();
                if (animator.isActiveAndEnabled) EvaluatePose(0);
                return true;
            }
            catch (Exception exception)
            {
                string error = "Could not initialize player animation: " + exception.Message;
                Dispose();
                LastError = error;
                return false;
            }
            finally
            {
                // Controller/root-motion changes can rebind animated transforms;
                // they must not change the model's fit relative to the player.
                if (animatorRoot != null)
                {
                    animatorRoot.localPosition = mountPosition;
                    animatorRoot.localRotation = mountRotation;
                    animatorRoot.localScale = mountScale;
                }
            }
        }

        public void Tick(float planarSpeed, bool grounded, float verticalVelocity, float deltaTime)
        {
            if (!IsInitialized)
            {
                if (graph.IsValid()) Dispose();
                return;
            }
            if (!animator.isActiveAndEnabled) return;
            float dt = Mathf.Clamp(Finite(deltaTime, 0), 0, .25f);
            CurrentSpeed = Mathf.Clamp(Finite(planarSpeed, 0), 0, 100);
            float vertical = Finite(verticalVelocity, 0);
            float landDuration = Mathf.Clamp(Finite(profile.landingDuration, .15f), 0, 2);
            if (!grounded) landingRemaining = 0;
            else if (hasGroundSample && !previousGrounded && clips[Land] != null && landDuration > 0)
            {
                landingRemaining = landDuration;
                times[Land] = 0;
            }

            float idleThreshold = Mathf.Clamp(Finite(profile.idleThreshold, .08f), 0, 5);
            float walkSpeed = Mathf.Max(idleThreshold + .01f, Finite(profile.walkSpeed, 2));
            float runSpeed = Mathf.Max(walkSpeed + .01f, Finite(profile.runSpeed, 5));
            float runThreshold = Mathf.Clamp(Finite(profile.runThreshold, 3), walkSpeed, runSpeed - .001f);
            int nextState = !grounded ? (vertical > .05f ? Jump : Fall) :
                landingRemaining > 0 ? Land : CurrentSpeed <= idleThreshold ? Idle :
                CurrentSpeed > runThreshold ? Run : Walk;
            if (nextState != state && (nextState == Jump || nextState == Fall || nextState == Land))
                times[nextState] = 0;
            state = nextState;
            CurrentState = StateNames[state];
            hasGroundSample = true;
            previousGrounded = grounded;

            Array.Clear(targets, 0, targets.Length);
            if (state == Jump || state == Fall || state == Land)
                targets[Resolve(state)] = 1;
            else if (CurrentSpeed <= idleThreshold)
                targets[Resolve(Idle)] = 1;
            else if (CurrentSpeed < walkSpeed)
            {
                float blend = Mathf.InverseLerp(idleThreshold, walkSpeed, CurrentSpeed);
                targets[Resolve(Idle)] += 1 - blend;
                targets[Resolve(Walk)] += blend;
            }
            else
            {
                float blend = Mathf.InverseLerp(runThreshold, runSpeed, CurrentSpeed);
                targets[Resolve(Walk)] += 1 - blend;
                targets[Resolve(Run)] += blend;
            }

            float duration = Mathf.Max(0, Finite(profile.blendDuration, .15f));
            float alpha = duration <= 0 ? 1 : 1 - Mathf.Exp(-3 * dt / duration);
            for (int index = 0; index < ClipCount; index++)
            {
                weights[index] = Mathf.Lerp(weights[index], targets[index], alpha);
                if (clips[index] == null) continue;
                double length = Math.Max(.0001, clips[index].length);
                double rate = index == Walk ? Mathf.Clamp(CurrentSpeed / walkSpeed, 0, 3) :
                    index == Run ? Mathf.Clamp(CurrentSpeed / runSpeed, 0, 3) :
                    index == Land && landDuration > 0 ? length / landDuration : 1;
                times[index] += dt * rate;
                // Jump and Land are one-shots; others are cyclic. Fall->Jump
                // fallback holds the last jump pose while descending.
                double sample = index == Jump || index == Land
                    ? Math.Min(times[index], length) : times[index] % length;
                playables[index].SetTime(sample);
            }
            ApplyWeights();
            // A disabled root motion flag prevents Animator from driving the
            // CharacterController; motion clips should still be authored in place.
            if (animator.applyRootMotion) animator.applyRootMotion = false;
            EvaluatePose(dt);
            if (grounded && landingRemaining > 0) landingRemaining = Mathf.Max(0, landingRemaining - dt);
        }

        int Resolve(int requested)
        {
            if (clips[requested] != null) return requested;
            if (requested == Walk && clips[Run] != null) return Run;
            if (requested == Run && clips[Walk] != null) return Walk;
            if (requested == Jump && clips[Fall] != null) return Fall;
            if (requested == Fall && clips[Jump] != null) return Jump;
            if (clips[Idle] != null) return Idle;
            for (int index = 0; index < ClipCount; index++) if (clips[index] != null) return index;
            return Idle; // Initialize guarantees that at least one clip exists.
        }

        void ApplyWeights()
        {
            for (int index = 0; index < ClipCount; index++) mixer.SetInputWeight(index, weights[index]);
        }

        void EvaluatePose(float deltaTime)
        {
            // Generic clips can directly bind the Animator object's Transform;
            // those curves are not necessarily classified as Animator root motion.
            // Preserve the model mounting offset and facing while still animating
            // its child bones. CharacterController remains the movement authority.
            Transform root = animator.transform;
            Vector3 position = root.localPosition;
            Quaternion rotation = root.localRotation;
            try { graph.Evaluate(deltaTime); }
            finally
            {
                if (root != null)
                {
                    root.localPosition = position;
                    root.localRotation = rotation;
                }
            }
        }

        static float Finite(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;

        public void Dispose()
        {
            Transform animatorRoot = animator != null ? animator.transform : null;
            Vector3 mountPosition = animatorRoot != null ? animatorRoot.localPosition : Vector3.zero;
            Quaternion mountRotation = animatorRoot != null ? animatorRoot.localRotation : Quaternion.identity;
            Vector3 mountScale = animatorRoot != null ? animatorRoot.localScale : Vector3.one;
            // Destroy our output before reconnecting the saved controller. Restore
            // root motion and culling first so controller rebinding sees its original
            // settings, without competing with a still-connected custom graph.
            if (graph.IsValid()) graph.Destroy();
            if (animator != null)
            {
                animator.applyRootMotion = originalRootMotion;
                animator.cullingMode = originalCullingMode;
                animator.runtimeAnimatorController = originalController;
                animatorRoot.localPosition = mountPosition;
                animatorRoot.localRotation = mountRotation;
                animatorRoot.localScale = mountScale;
            }
            graph = default;
            mixer = default;
            animator = null;
            profile = null;
            originalController = null;
            Array.Clear(clips, 0, clips.Length);
            Array.Clear(playables, 0, playables.Length);
            Array.Clear(times, 0, times.Length);
            Array.Clear(weights, 0, weights.Length);
            Array.Clear(targets, 0, targets.Length);
            hasGroundSample = false;
            previousGrounded = false;
            landingRemaining = 0;
            state = Idle;
            CurrentState = "Disabled";
            CurrentSpeed = 0;
        }
    }
}
