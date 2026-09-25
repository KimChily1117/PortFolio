using System.Collections;
using System.Collections.Generic;
using Kimchily.Creator.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kimchily.Creator.Tests
{
    public sealed class PlayerAnimationTests
    {
        readonly List<Object> created = new List<Object>();
        KimchilyPlayerAnimationDriver driver;
        KimchilyPlayerAnimationProfile profile;
        Animator animator;
        Transform probe;

        [SetUp]
        public void SetUp()
        {
            var root = new GameObject("Animation driver test");
            created.Add(root);
            animator = root.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = true;
            probe = new GameObject("Probe").transform;
            probe.SetParent(root.transform, false);
            profile = ScriptableObject.CreateInstance<KimchilyPlayerAnimationProfile>();
            created.Add(profile);
            driver = new KimchilyPlayerAnimationDriver();
        }

        [TearDown]
        public void TearDown()
        {
            driver.Dispose();
            for (int index = created.Count - 1; index >= 0; index--)
                if (created[index] != null) Object.DestroyImmediate(created[index]);
            created.Clear();
        }

        [Test]
        public void MissingProfileOrAnimatorLeavesExistingAnimatorConfigurationAlone()
        {
#if UNITY_EDITOR
            var controller = new UnityEditor.Animations.AnimatorController();
            controller.AddLayer("Base Layer");
            created.Add(controller);
            animator.runtimeAnimatorController = controller;
#else
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
#endif
            Assert.IsFalse(driver.Initialize(animator, null));
            Assert.IsFalse(driver.IsInitialized);
            Assert.AreSame(controller, animator.runtimeAnimatorController);
            Assert.IsTrue(animator.applyRootMotion);
            Assert.IsFalse(driver.Initialize(null, profile));
            driver.Tick(3, true, 0, .02f);
            Assert.AreEqual("Disabled", driver.CurrentState);
        }

        [Test]
        public void ActualSpeedSmoothlyBlendsIdleWalkAndRunPoses()
        {
            profile.idle = Clip("Idle", 0);
            profile.walk = Clip("Walk", 10);
            profile.run = Clip("Run", 20);
            profile.blendDuration = .2f;
            Assert.IsTrue(driver.Initialize(animator, profile), driver.LastError);
            Assert.IsFalse(animator.applyRootMotion);
            driver.Tick(profile.walkSpeed, true, 0, .01f);
            Assert.Greater(probe.localPosition.x, 0);
            Assert.Less(probe.localPosition.x, 10);
            Settle(profile.walkSpeed);
            Assert.AreEqual("Walk", driver.CurrentState);
            Assert.That(probe.localPosition.x, Is.EqualTo(10).Within(.01f));
            Settle(profile.runSpeed);
            Assert.AreEqual("Run", driver.CurrentState);
            Assert.AreEqual(profile.runSpeed, driver.CurrentSpeed);
            Assert.That(probe.localPosition.x, Is.EqualTo(20).Within(.01f));
            Settle(0);
            Assert.AreEqual("Idle", driver.CurrentState);
            Assert.That(probe.localPosition.x, Is.EqualTo(0).Within(.01f));
        }

        [Test]
        public void JumpFallAndLandingUseTheirMappedClipsThenReturnToLocomotion()
        {
            profile.idle = Clip("Idle", 0);
            profile.jump = Clip("Jump", 30);
            profile.fall = Clip("Fall", 40);
            profile.land = Clip("Land", 50);
            profile.blendDuration = 0;
            profile.landingDuration = .15f;
            Assert.IsTrue(driver.Initialize(animator, profile), driver.LastError);
            driver.Tick(0, true, 0, .02f);
            driver.Tick(0, false, 4, .02f);
            Assert.AreEqual("Jump", driver.CurrentState);
            Assert.That(probe.localPosition.x, Is.EqualTo(30).Within(.01f));
            driver.Tick(0, false, -1, .02f);
            Assert.AreEqual("Fall", driver.CurrentState);
            Assert.That(probe.localPosition.x, Is.EqualTo(40).Within(.01f));
            driver.Tick(0, true, -2, .02f);
            Assert.AreEqual("Land", driver.CurrentState);
            Assert.That(probe.localPosition.x, Is.EqualTo(50).Within(.01f));
            for (int index = 0; index < 20; index++) driver.Tick(0, true, -2, .02f);
            Assert.AreEqual("Idle", driver.CurrentState);
            Assert.That(probe.localPosition.x, Is.EqualTo(0).Within(.01f));
        }

        [Test]
        public void MissingAirAndMovementClipsUseIdleAndMissingLandDoesNotPauseMovement()
        {
            profile.idle = Clip("Only Idle", 7);
            profile.blendDuration = 0;
            Assert.IsTrue(driver.Initialize(animator, profile), driver.LastError);
            driver.Tick(5, true, 0, .02f);
            Assert.AreEqual("Run", driver.CurrentState);
            Assert.That(probe.localPosition.x, Is.EqualTo(7).Within(.01f));
            driver.Tick(0, false, 4, .02f);
            Assert.AreEqual("Jump", driver.CurrentState);
            driver.Tick(0, false, -4, .02f);
            Assert.AreEqual("Fall", driver.CurrentState);
            Assert.That(probe.localPosition.x, Is.EqualTo(7).Within(.01f));
            driver.Tick(0, true, -2, .02f);
            Assert.AreEqual("Idle", driver.CurrentState);
        }

        [Test]
        public void LegacyClipsAreRejectedWithoutTakingControlOfTheAnimator()
        {
            profile.idle = Clip("Legacy", 1);
            profile.idle.legacy = true;
            Assert.IsFalse(driver.Initialize(animator, profile));
            StringAssert.Contains("Legacy", driver.LastError);
            Assert.IsFalse(driver.IsInitialized);
            Assert.IsTrue(animator.applyRootMotion);
            profile.walk = Clip("Valid Walk", 2);
            Assert.IsTrue(driver.Initialize(animator, profile));
            Assert.IsFalse(animator.applyRootMotion);
            StringAssert.Contains("Legacy", driver.LastError);
        }

        [Test]
        public void WalkPlaybackUsesMeasuredSpeedAndLoopsWithoutChangingTheClipAsset()
        {
            AnimationClip clip = new AnimationClip { name = "Walk distance probe", legacy = false };
            created.Add(clip);
            clip.SetCurve("Probe", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
            profile.walk = clip;
            profile.walkSpeed = 2;
            profile.blendDuration = 0;
            WrapMode originalWrapMode = clip.wrapMode;
            Assert.IsTrue(driver.Initialize(animator, profile), driver.LastError);
            driver.Tick(1, true, 0, .2f);
            Assert.That(probe.localPosition.x, Is.EqualTo(.1f).Within(.02f));
            driver.Tick(2, true, 0, .2f);
            Assert.That(probe.localPosition.x, Is.EqualTo(.3f).Within(.02f));
            driver.Tick(5, true, 0, .2f);
            Assert.That(probe.localPosition.x, Is.EqualTo(.8f).Within(.02f));
            driver.Tick(2, true, 0, .25f);
            Assert.That(probe.localPosition.x, Is.EqualTo(.05f).Within(.02f));
            Assert.AreEqual(originalWrapMode, clip.wrapMode);
        }

        [Test]
        public void ReinitializeAndRepeatedDisposeReleaseTheGraphAndRestoreRootMotion()
        {
#if UNITY_EDITOR
            RuntimeAnimatorController originalController = ControllerWithClip(Clip("Authored controller pose", 1));
            animator.runtimeAnimatorController = originalController;
#else
            RuntimeAnimatorController originalController = animator.runtimeAnimatorController;
#endif
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            profile.idle = Clip("Idle", 3);
            Assert.IsTrue(driver.Initialize(animator, profile), driver.LastError);
            Assert.IsFalse(animator.applyRootMotion);
            Assert.IsNull(animator.runtimeAnimatorController);
            Assert.AreEqual(AnimatorCullingMode.AlwaysAnimate, animator.cullingMode);
            Assert.IsTrue(driver.Initialize(animator, profile), driver.LastError);
            Assert.IsNull(animator.runtimeAnimatorController);
            driver.Dispose();
            driver.Dispose();
            Assert.IsFalse(driver.IsInitialized);
            Assert.IsTrue(animator.applyRootMotion);
            Assert.AreSame(originalController, animator.runtimeAnimatorController);
            Assert.AreEqual(AnimatorCullingMode.CullCompletely, animator.cullingMode);
            Assert.AreEqual("Disabled", driver.CurrentState);
            Assert.IsTrue(driver.Initialize(animator, profile), driver.LastError);
            Assert.IsFalse(driver.Initialize(animator, null));
            Assert.IsTrue(animator.applyRootMotion);
            Assert.AreSame(originalController, animator.runtimeAnimatorController);
            Assert.AreEqual(AnimatorCullingMode.CullCompletely, animator.cullingMode);
        }

        [UnityTest]
        public IEnumerator MappedPoseSurvivesTheAnimatorPassAndOriginalControllerResumesAfterDispose()
        {
#if UNITY_EDITOR
            RuntimeAnimatorController originalController = ControllerWithClip(Clip("Original pose", 2));
            animator.runtimeAnimatorController = originalController;
            animator.Update(0);
            Assert.That(probe.localPosition.x, Is.EqualTo(2).Within(.01f));
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            profile.idle = Clip("Mapped Idle", 3);
            profile.walk = Clip("Mapped Walk", 10);
            profile.run = Clip("Mapped Run", 20);
            profile.blendDuration = 0;
            Assert.IsTrue(driver.Initialize(animator, profile), driver.LastError);
            var observer = animator.gameObject.AddComponent<PlayerAnimationFrameProbe>();
            observer.Driver = driver;
            observer.Target = probe;
            observer.PlanarSpeed = profile.walkSpeed;
            for (int index = 0; index < 8; index++) yield return null;
            Assert.GreaterOrEqual(observer.ObservedFrames, 5);
            Assert.That(observer.LastLatePosition, Is.EqualTo(10).Within(.01f),
                "The mapped pose must remain after Unity's automatic Animator evaluation, not only immediately after Tick.");
            Assert.IsNull(animator.runtimeAnimatorController);
            observer.PlanarSpeed = profile.runSpeed;
            for (int index = 0; index < 5; index++) yield return null;
            Assert.That(observer.LastLatePosition, Is.EqualTo(20).Within(.01f));

            driver.Dispose();
            Assert.AreSame(originalController, animator.runtimeAnimatorController);
            Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms, animator.cullingMode);
            Assert.IsTrue(animator.applyRootMotion);
            // This fixture has no renderer; make the restored controller observable
            // in headless tests after first checking its authored culling was restored.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            for (int index = 0; index < 5; index++) yield return null;
            Assert.That(observer.LastLatePosition, Is.EqualTo(2).Within(.01f));
#else
            Assert.Ignore("The regression fixture constructs an AnimatorController in the Editor.");
            yield break;
#endif
        }

        [Test]
        public void GenericRootTransformCurvesCannotDetachTheVisualFromItsMovementRoot()
        {
            Vector3 mount = new Vector3(2, 3, 4);
            Quaternion facing = Quaternion.Euler(0, 35, 0);
            animator.transform.localPosition = mount;
            animator.transform.localRotation = facing;
            var clip = Clip("Generic root movement", 5);
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Constant(0, 1, 50));
            profile.idle = clip;
            Assert.IsTrue(driver.Initialize(animator, profile), driver.LastError);
            for (int index = 0; index < 20; index++) driver.Tick(0, true, 0, .02f);
            Assert.That(Vector3.Distance(mount, animator.transform.localPosition), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(facing, animator.transform.localRotation), Is.LessThan(.001f));
            Assert.That(probe.localPosition.x, Is.EqualTo(5).Within(.01f), "Child-bone animation must still be evaluated.");
        }

        [Test]
        public void NonFiniteMovementInputsCannotPoisonAnimationWeights()
        {
            profile.idle = Clip("Idle", 2);
            profile.blendDuration = float.NaN;
            Assert.IsTrue(driver.Initialize(animator, profile), driver.LastError);
            driver.Tick(float.NaN, true, float.PositiveInfinity, float.NaN);
            Assert.AreEqual(0, driver.CurrentSpeed);
            Assert.AreEqual("Idle", driver.CurrentState);
            Assert.IsFalse(float.IsNaN(probe.localPosition.x));
            Assert.IsFalse(float.IsInfinity(probe.localPosition.x));
        }

        AnimationClip Clip(string name, float value)
        {
            var clip = new AnimationClip { name = name, legacy = false };
            clip.SetCurve("Probe", typeof(Transform), "localPosition.x", AnimationCurve.Constant(0, 1, value));
            created.Add(clip);
            return clip;
        }

#if UNITY_EDITOR
        RuntimeAnimatorController ControllerWithClip(AnimationClip clip)
        {
            var controller = new UnityEditor.Animations.AnimatorController();
            created.Add(controller);
            controller.AddLayer("Base Layer");
            var machine = controller.layers[0].stateMachine;
            var state = machine.AddState("Authored pose");
            state.motion = clip;
            machine.defaultState = state;
            return controller;
        }
#endif

        void Settle(float speed)
        {
            for (int index = 0; index < 80; index++) driver.Tick(speed, true, 0, 1f / 60);
        }
    }
}
