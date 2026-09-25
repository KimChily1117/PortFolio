using System.Collections;
using System.Collections.Generic;
using Kimchily.Creator.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Kimchily.Creator.Tests
{
    public sealed class ThirdPersonMovementTests
    {
        private const int MoveFinger = 420;
        private readonly List<Scene> scenes = new List<Scene>();
        private int previousCaptureFramerate;
        private float previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            previousCaptureFramerate = Time.captureFramerate;
            previousTimeScale = Time.timeScale;
            Time.captureFramerate = 50;
            Time.timeScale = 1;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.captureFramerate = previousCaptureFramerate;
            Time.timeScale = previousTimeScale;
            foreach (Scene scene in scenes)
            {
                if (!scene.IsValid() || !scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (KimchilyMobilePlayer player in root.GetComponentsInChildren<KimchilyMobilePlayer>(true)) player.enabled = false;
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            scenes.Clear();
        }

        [UnityTest]
        public IEnumerator SustainedRightInputMovesStraightWhileOnlyCharacterTurns()
        {
            Scene scene = NewWorld();
            KimchilyMobilePlayer player = KimchilyMobilePlayerBootstrap.CreateForScene(scene);
            yield return null;
            yield return null;
            Camera camera = player.ViewCamera;
            Assert.IsNotNull(camera);
            Assert.IsNull(camera.transform.parent, "The follow camera must not inherit character rotation.");
            Assert.AreEqual(scene, camera.gameObject.scene);
            Vector3 cameraForward = Planar(camera.transform.forward);
            Vector3 expected = Vector3.Cross(Vector3.up, cameraForward);
            Vector3 start = player.transform.position;

            yield return WalkForFrames(player, Vector2.right, 40);

            AssertStraightMovement(start, player.transform.position, expected);
            Assert.Less(Vector3.Angle(cameraForward, Planar(camera.transform.forward)), .1f, "Movement must not rotate the viewing direction.");
            Assert.Less(Vector3.Angle(expected, Planar(player.transform.forward)), 3, "The character should face its travel direction.");
        }

        [UnityTest]
        public IEnumerator DragFromDrawnJoystickKeepsMovementOwnershipAcrossLookArea()
        {
            KimchilyMobilePlayer player = KimchilyMobilePlayerBootstrap.CreateForScene(NewWorld());
            yield return null;
            yield return null;
            MobileControlLayout layout = player.Controls.Layout;
            Vector2 drawnKnobCenter = layout.Joystick.center;
            Vector2 draggedPosition = new Vector2(layout.Look.center.x, drawnKnobCenter.y);
            Assert.IsTrue(layout.Joystick.Contains(drawnKnobCenter));
            Assert.IsFalse(layout.Look.Contains(drawnKnobCenter));
            Assert.IsTrue(layout.Look.Contains(draggedPosition));
            Vector3 cameraForward = Planar(player.ViewCamera.transform.forward);
            Vector3 expected = Vector3.Cross(Vector3.up, cameraForward);
            Vector3 start = player.transform.position;

            player.Controls.InputState.BeginPointer(MoveFinger, drawnKnobCenter, layout);
            player.Controls.InputState.MovePointer(MoveFinger, draggedPosition, layout);

            Assert.IsTrue(player.Controls.InputState.IsMovingPointer);
            Assert.Greater(player.Controls.Move.x, .99f);
            Assert.AreEqual(Vector2.zero, player.Controls.InputState.ConsumeLook(),
                "A finger that began on the drawn joystick must never become a look gesture.");
            for (int i = 0; i < 40; i++) yield return null;
            player.Controls.InputState.EndPointer(MoveFinger);

            AssertStraightMovement(start, player.transform.position, expected);
            Assert.Less(Vector3.Angle(cameraForward, Planar(player.ViewCamera.transform.forward)), .1f);
            Assert.AreEqual(Vector2.zero, player.Controls.Move);
        }

        [UnityTest]
        public IEnumerator RightSideDragChangesTheCameraBasisUsedByForwardInput()
        {
            KimchilyMobilePlayer player = KimchilyMobilePlayerBootstrap.CreateForScene(NewWorld());
            yield return null;
            yield return null;
            Vector3 originalForward = Planar(player.ViewCamera.transform.forward);
            MobileControlLayout layout = player.Controls.Layout;
            float scaleDimension = Mathf.Max(200, Mathf.Min(layout.SafeArea.width, layout.SafeArea.height));
            // Feed the same accumulated pixel delta produced by the right-side look
            // pointer, then let the actual LateUpdate consume it before walking.
            player.Controls.InputState.AddLookDelta(Vector2.right * (scaleDimension * .5f));
            yield return null;
            yield return null;
            Vector3 expected = Planar(player.ViewCamera.transform.forward);
            Assert.That(Vector3.Angle(originalForward, expected), Is.EqualTo(90).Within(.2f));
            Vector3 start = player.transform.position;

            yield return WalkForFrames(player, Vector2.up, 40);

            AssertStraightMovement(start, player.transform.position, expected);
            Assert.Less(Vector3.Angle(expected, Planar(player.ViewCamera.transform.forward)), .1f);
        }

        [UnityTest]
        public IEnumerator CustomCameraModeUsesActiveArtistCameraForStraightStrafing()
        {
            Scene scene = NewWorld();
            Camera artist = CreateArtistCamera(scene);
            artist.transform.rotation = Quaternion.Euler(25, 80, 0);
            KimchilyMobilePlayer player = KimchilyMobilePlayerBootstrap.CreateForScene(scene);
            player.UseThirdPersonCamera = false;
            Assert.IsTrue(artist.enabled);
            Assert.IsFalse(player.ViewCamera.gameObject.activeInHierarchy);
            yield return null;
            yield return null;
            Quaternion originalRotation = artist.transform.rotation;
            Vector3 expected = Vector3.Cross(Vector3.up, Planar(artist.transform.forward));
            Vector3 start = player.transform.position;

            yield return WalkForFrames(player, Vector2.right, 40);

            AssertStraightMovement(start, player.transform.position, expected);
            Assert.That(Quaternion.Angle(originalRotation, artist.transform.rotation), Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator WithoutExternalCameraMovementKeepsSpawnHeadingAndIgnoresPlayerChildCamera()
        {
            Scene scene = NewWorld();
            KimchilyMobilePlayer player = KimchilyMobilePlayerBootstrap.CreateForScene(scene);
            Vector3 expected = -Vector3.Cross(Vector3.up, Planar(player.transform.forward));
            player.UseThirdPersonCamera = false;
            var childCamera = new GameObject("Authored player child camera", typeof(Camera));
            childCamera.transform.SetParent(player.transform, false);
            childCamera.transform.localRotation = Quaternion.Euler(15, 45, 0);
            yield return null;
            yield return null;
            Vector3 start = player.transform.position;

            yield return WalkForFrames(player, Vector2.left, 40);

            AssertStraightMovement(start, player.transform.position, expected);
            Assert.Less(Vector3.Angle(expected, Planar(player.transform.forward)), 3);
        }

        [UnityTest]
        public IEnumerator DestroyingOnlyPlayerRemovesIndependentCameraAndRestoresArtistCamera()
        {
            Scene scene = NewWorld();
            Camera artist = CreateArtistCamera(scene);
            AudioListener artistListener = artist.gameObject.AddComponent<AudioListener>();
            KimchilyMobilePlayer player = KimchilyMobilePlayerBootstrap.CreateForScene(scene);
            Camera generatedCamera = player.ViewCamera;
            Assert.IsFalse(artist.enabled);
            Assert.IsFalse(artistListener.enabled);
            Assert.IsNull(generatedCamera.transform.parent);

            Object.Destroy(player.gameObject);
            yield return null;
            yield return null;

            Assert.IsTrue(scene.isLoaded, "Only the player was destroyed; the world is still open.");
            Assert.IsTrue(player == null);
            Assert.IsTrue(generatedCamera == null, "The independent camera must not be orphaned.");
            Assert.IsTrue(artist.enabled);
            Assert.IsTrue(artistListener.enabled);
        }

        [Test]
        public void ZeroDeltaTimePreservesGroundingAndDefersJumpUntilTimeAdvances()
        {
            KimchilyMobilePlayer player = KimchilyMobilePlayerBootstrap.CreateForScene(NewWorld());
            for (int i = 0; i < 20; i++) player.Simulate(.02f, Vector2.zero, false);
            Assert.IsTrue(player.IsGrounded);
            player.Simulate(.02f, Vector2.up, false);
            Assert.Greater(player.PlanarSpeed, 1);
            Vector3 position = player.transform.position;
            Quaternion rotation = player.transform.rotation;
            float verticalVelocity = player.VerticalVelocity;
            CollisionFlags collisions = player.LastCollisions;

            player.Simulate(0, Vector2.right, true);
            player.Simulate(0, Vector2.right, false);

            Assert.AreEqual(position, player.transform.position);
            Assert.That(Quaternion.Angle(rotation, player.transform.rotation), Is.LessThan(.001f));
            Assert.AreEqual(verticalVelocity, player.VerticalVelocity);
            Assert.AreEqual(collisions, player.LastCollisions);
            Assert.IsTrue(player.IsGrounded);
            Assert.AreEqual(0, player.PlanarSpeed);

            player.Simulate(.02f, Vector2.zero, false);

            Assert.Greater(player.VerticalVelocity, 0, "The buffered jump should begin only when simulation time advances.");
            Assert.Greater(player.transform.position.y, position.y);
        }

        [Test]
        public void SwitchingCameraModeClearsHeldInputAndReusesOneFollowCamera()
        {
            Scene scene = NewWorld();
            Camera artist = CreateArtistCamera(scene);
            KimchilyMobilePlayer player = KimchilyMobilePlayerBootstrap.CreateForScene(scene);
            Camera generatedCamera = player.ViewCamera;
            HoldInput(player, Vector2.right);
            player.Controls.InputState.RequestJump();
            player.Controls.InputState.AddLookDelta(new Vector2(40, 20));

            player.UseThirdPersonCamera = false;

            AssertInputCleared(player);
            Assert.IsTrue(artist.enabled);
            Assert.IsFalse(generatedCamera.gameObject.activeInHierarchy);
            HoldInput(player, Vector2.left);
            player.Controls.InputState.RequestJump();
            player.Controls.InputState.AddLookDelta(new Vector2(-25, 10));

            player.UseThirdPersonCamera = true;

            AssertInputCleared(player);
            Assert.AreSame(generatedCamera, player.ViewCamera);
            Assert.IsTrue(generatedCamera.gameObject.activeInHierarchy);
            Assert.IsFalse(artist.enabled);
            int cameraCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects()) cameraCount += root.GetComponentsInChildren<Camera>(true).Length;
            Assert.AreEqual(2, cameraCount);
        }

        private static IEnumerator WalkForFrames(KimchilyMobilePlayer player, Vector2 direction, int frames)
        {
            HoldInput(player, direction);
            for (int i = 0; i < frames; i++) yield return null;
            player.Controls.InputState.EndPointer(MoveFinger);
        }

        private static void HoldInput(KimchilyMobilePlayer player, Vector2 direction)
        {
            MobileControlLayout layout = player.Controls.Layout;
            player.Controls.InputState.BeginPointer(MoveFinger, layout.Joystick.center, layout);
            player.Controls.InputState.MovePointer(MoveFinger, layout.Joystick.center + direction * layout.JoystickRadius, layout);
        }

        private static void AssertStraightMovement(Vector3 start, Vector3 end, Vector3 expected)
        {
            Vector3 displacement = Vector3.ProjectOnPlane(end - start, Vector3.up);
            Assert.Greater(Vector3.Dot(displacement, expected), 2, "Held input must advance over actual Update frames.");
            Assert.Less(Vector3.ProjectOnPlane(displacement, expected).magnitude, .06f, "Sustained lateral input must not curve around the player's changing heading.");
        }

        private static void AssertInputCleared(KimchilyMobilePlayer player)
        {
            Assert.AreEqual(Vector2.zero, player.Controls.Move);
            Assert.IsFalse(player.Controls.InputState.IsMovingPointer);
            Assert.IsFalse(player.Controls.InputState.ConsumeJump());
            Assert.AreEqual(Vector2.zero, player.Controls.InputState.ConsumeLook());
        }

        private static Vector3 Planar(Vector3 value) => Vector3.ProjectOnPlane(value, Vector3.up).normalized;

        private Scene NewWorld()
        {
            Scene scene = SceneManager.CreateScene("Third person movement test " + System.Guid.NewGuid().ToString("N"));
            scenes.Add(scene);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Third person test floor";
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.position = new Vector3(1000, -.5f, 0);
            floor.transform.localScale = new Vector3(40, 1, 40);
            return scene;
        }

        private static Camera CreateArtistCamera(Scene scene)
        {
            var owner = new GameObject("Artist camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(owner, scene);
            owner.transform.position = new Vector3(1000, 5, -10);
            return owner.GetComponent<Camera>();
        }
    }
}
