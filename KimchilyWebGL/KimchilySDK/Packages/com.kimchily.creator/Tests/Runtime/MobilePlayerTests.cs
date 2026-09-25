using System.Collections;
using System.Collections.Generic;
using Kimchily.Creator.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Kimchily.Creator.Tests
{
    public sealed class MobilePlayerTests
    {
        private readonly List<Scene> scenes = new List<Scene>();

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (Scene scene in scenes)
            {
                if (!scene.IsValid() || !scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (KimchilyMobilePlayer player in root.GetComponentsInChildren<KimchilyMobilePlayer>(true)) player.enabled = false;
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            scenes.Clear();
        }

        [Test]
        public void ThreeFingersMoveLookAndJumpIndependentlyAndCancelPendingInput()
        {
            MobileControlLayout layout = MobileControlLayout.Calculate(new Rect(10, 30, 1000, 600));
            var state = new MobilePlayerInputState();
            state.BeginPointer(1, layout.Joystick.center, layout);
            state.MovePointer(1, layout.Joystick.center + Vector2.up * layout.JoystickRadius, layout);
            Vector2 lookStart = new Vector2(800, 400);
            state.BeginPointer(2, lookStart, layout);
            state.BeginPointer(3, layout.Jump.center, layout);
            state.MovePointer(2, lookStart + new Vector2(23, -11), layout);
            Assert.That(Vector2.Distance(Vector2.up, state.Move), Is.LessThan(.00001f));
            Assert.AreEqual(new Vector2(23, -11), state.ConsumeLook());
            Assert.IsTrue(state.ConsumeJump());
            Assert.IsFalse(state.ConsumeJump());
            state.EndPointer(3);
            Assert.That(Vector2.Distance(Vector2.up, state.Move), Is.LessThan(.00001f));
            state.MovePointer(2, lookStart + Vector2.one * 30, layout);
            state.EndPointer(2, true);
            Assert.AreEqual(Vector2.zero, state.ConsumeLook());
            state.BeginPointer(4, layout.Jump.center, layout);
            state.EndPointer(4, true);
            Assert.IsFalse(state.ConsumeJump());
            state.EndPointer(1, true);
            Assert.AreEqual(Vector2.zero, state.Move);
            Assert.IsFalse(state.IsMovingPointer);
        }

        [TestCase(30, 50, 1080, 1800)]
        [TestCase(70, 25, 2100, 900)]
        public void LayoutStaysInsideSafeAreaAndExcludesNativeHeader(float x, float y, float width, float height)
        {
            Rect safe = new Rect(x, y, width, height);
            MobileControlLayout layout = MobileControlLayout.Calculate(safe);
            AssertRectInside(safe, layout.Joystick);
            AssertRectInside(safe, layout.Jump);
            AssertRectInside(safe, layout.Look);
            Assert.IsFalse(layout.Jump.Overlaps(layout.Joystick));
            Assert.LessOrEqual(layout.Look.yMax, safe.y + safe.height * .85f + .01f);
            var state = new MobilePlayerInputState();
            state.BeginPointer(1, new Vector2(safe.xMax - 1, safe.yMax - 1), layout);
            state.MovePointer(1, safe.center, layout);
            Assert.AreEqual(Vector2.zero, state.ConsumeLook());
        }

        [Test]
        public void OrientationPauseAndDisableClearHeldInput()
        {
            Scene scene = NewScene();
            GameObject owner = NewObject(scene, "Input controls");
            var controls = owner.AddComponent<KimchilyMobileControls>();
            controls.RefreshLayout(new Rect(0, 0, 1200, 800));
            HoldAll(controls);
            controls.RefreshLayout(new Rect(20, 40, 760, 1160));
            AssertCleared(controls);
            HoldAll(controls);
            owner.SendMessage("OnApplicationPause", true);
            AssertCleared(controls);
            HoldAll(controls);
            controls.enabled = false;
            AssertCleared(controls);
        }

        [UnityTest]
        public IEnumerator SceneBootstrapReusesPlayerRestoresArtistCameraAndUnloadsEverything()
        {
            Scene scene = NewScene();
            CreateFloor(scene);
            var artist = NewObject(scene, "Artist camera").AddComponent<Camera>();
            Vector3 artistPosition = artist.transform.position = new Vector3(1000, 5, -5);
            artist.clearFlags = CameraClearFlags.SolidColor;
            Color artistBackground = artist.backgroundColor = new Color(.03f, .08f, .13f, 1);
            artist.fieldOfView = 42;
            KimchilyMobilePlayer player = KimchilyMobilePlayerBootstrap.EnsureForScene(scene);
            Assert.AreSame(player, KimchilyMobilePlayerBootstrap.EnsureForScene(scene));
            Assert.IsFalse(artist.enabled);
            Assert.AreEqual(scene, player.gameObject.scene);
            Assert.AreEqual(scene, player.ViewCamera.gameObject.scene);
            Assert.AreEqual(scene, player.VisualRoot.gameObject.scene);
            Assert.IsTrue(player.ViewCamera.gameObject.activeInHierarchy);
            Assert.AreEqual(CameraClearFlags.SolidColor, player.ViewCamera.clearFlags);
            Assert.AreEqual(artistBackground, player.ViewCamera.backgroundColor);
            Assert.That(player.ViewCamera.fieldOfView, Is.EqualTo(60).Within(.001f));
            player.enabled = false;
            Assert.IsTrue(artist.enabled);
            Assert.AreEqual(artistPosition, artist.transform.position);
            Assert.AreEqual(CameraClearFlags.SolidColor, artist.clearFlags);
            Assert.AreEqual(artistBackground, artist.backgroundColor);
            Assert.That(artist.fieldOfView, Is.EqualTo(42).Within(.001f));
            Assert.IsFalse(player.ViewCamera.gameObject.activeSelf);
            player.enabled = true;
            Assert.IsFalse(artist.enabled);
            Camera generatedCamera = player.ViewCamera;
            yield return SceneManager.UnloadSceneAsync(scene);
            Assert.IsTrue(player == null);
            Assert.IsTrue(generatedCamera == null);
        }

        [Test]
        public void SmallRoundFloorAvoidsCenterPropAndFindsStandingSpace()
        {
            Scene scene = NewScene();
            GameObject floor = Primitive(scene, PrimitiveType.Cylinder, "Small floor", new Vector3(1000, -.1f, 0), new Vector3(3, .1f, 3));
            // The primitive's capsule preserves its radius under nonuniform scaling,
            // so use the actual flat cylinder mesh as the walking surface.
            Collider primitiveCollider = floor.GetComponent<Collider>();
            primitiveCollider.enabled = false;
            Object.Destroy(primitiveCollider);
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            Primitive(scene, PrimitiveType.Cube, "Origin prop", new Vector3(1000, .4f, 0), new Vector3(.55f, .8f, .55f));
            Assert.IsTrue(KimchilyMobilePlayerBootstrap.FindSpawn(scene, out Vector3 spawn, out _));
            Vector3 planar = Vector3.ProjectOnPlane(spawn - floor.transform.position, Vector3.up);
            Assert.Greater(planar.magnitude, .6f);
            Assert.Less(planar.magnitude, 1.5f);
            Assert.That(spawn.y, Is.EqualTo(.08f).Within(.06f));
        }

        [TestCase("World Platform", 4.5f, .1f)]
        [TestCase("Display Platform", 3, .08f)]
        public void ExactLegacyStarterFloorIsRepairedOnlyInLoadedScene(string name, float width, float thickness)
        {
            Scene scene = NewScene();
            GameObject floor = Primitive(scene, PrimitiveType.Cylinder, name, new Vector3(0, -thickness, 0), new Vector3(width, thickness, width));
            CapsuleCollider original = floor.GetComponent<CapsuleCollider>();
            Mesh originalMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            Vector3 originalScale = floor.transform.localScale;
            KimchilyMobilePlayer player = KimchilyMobilePlayerBootstrap.EnsureForScene(scene);
            Assert.IsFalse(original.enabled);
            MeshCollider replacement = floor.GetComponent<MeshCollider>();
            Assert.IsNotNull(replacement);
            Assert.AreSame(originalMesh, replacement.sharedMesh);
            Assert.IsFalse(replacement.convex);
            Assert.AreEqual(originalScale, floor.transform.localScale);
            Assert.AreEqual(new Vector3(0, -thickness, 0), floor.transform.position);
            Assert.That(player.SpawnPosition.y, Is.EqualTo(.08f).Within(.06f));
            Assert.AreSame(player, KimchilyMobilePlayerBootstrap.EnsureForScene(scene));
            Assert.AreEqual(1, floor.GetComponents<MeshCollider>().Length);
        }

        [TestCase("name")]
        [TestCase("scale")]
        [TestCase("position")]
        [TestCase("radius")]
        [TestCase("extraCollider")]
        [TestCase("customMesh")]
        public void AuthoredFloorVariationsNeverReceiveLegacyCompatibility(string change)
        {
            Scene scene = NewScene();
            GameObject floor = Primitive(scene, PrimitiveType.Cylinder, "World Platform", new Vector3(0, -.1f, 0), new Vector3(4.5f, .1f, 4.5f));
            CapsuleCollider original = floor.GetComponent<CapsuleCollider>();
            Mesh customMesh = null;
            switch (change)
            {
                case "name": floor.name = "My authored platform"; break;
                case "scale": floor.transform.localScale += Vector3.right * .1f; break;
                case "position": floor.transform.position += Vector3.right; break;
                case "radius": original.radius = .6f; break;
                case "extraCollider": floor.AddComponent<BoxCollider>(); break;
                case "customMesh":
                    customMesh = Object.Instantiate(floor.GetComponent<MeshFilter>().sharedMesh);
                    customMesh.name = "Cylinder";
                    floor.GetComponent<MeshFilter>().sharedMesh = customMesh;
                    break;
            }
            KimchilyMobilePlayerBootstrap.EnsureForScene(scene);
            Assert.IsTrue(original.enabled);
            Assert.IsNull(floor.GetComponent<MeshCollider>());
            if (customMesh != null) Object.Destroy(customMesh);
        }

        [Test]
        public void ControllerJumpsLandsAndStopsAtWall()
        {
            Scene scene = NewScene();
            CreateFloor(scene);
            KimchilyMobilePlayer player = KimchilyMobilePlayerBootstrap.CreateForScene(scene);
            for (int i = 0; i < 20; i++) player.Simulate(.02f, Vector2.zero, false);
            Assert.IsTrue(player.Controller.isGrounded);
            float groundY = player.transform.position.y;
            player.Simulate(.02f, Vector2.zero, true);
            Assert.Greater(player.VerticalVelocity, 0);
            float firstVelocity = player.VerticalVelocity;
            player.Simulate(.02f, Vector2.zero, true);
            Assert.Less(player.VerticalVelocity, firstVelocity, "A second airborne press must not grant another jump.");
            float peak = player.transform.position.y;
            for (int i = 0; i < 100; i++) { player.Simulate(.02f, Vector2.zero, false); peak = Mathf.Max(peak, player.transform.position.y); }
            Assert.Greater(peak - groundY, .8f);
            Assert.Less(peak - groundY, 1.5f);
            Assert.IsTrue(player.Controller.isGrounded);
            Assert.That(player.transform.position.y, Is.EqualTo(groundY).Within(.1f));

            Vector3 start = player.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(player.ViewCamera.transform.forward, Vector3.up).normalized;
            GameObject wall = Primitive(scene, PrimitiveType.Cube, "Collision wall", start + forward * 2 + Vector3.up, new Vector3(5, 3, .3f));
            wall.transform.rotation = Quaternion.LookRotation(forward);
            Physics.SyncTransforms();
            for (int i = 0; i < 80; i++) player.Simulate(.02f, Vector2.up, false);
            float distance = Vector3.Dot(player.transform.position - start, forward);
            Assert.Greater(distance, .5f);
            Assert.Less(distance, 1.9f, "CharacterController must be stopped by the authored wall collider.");
        }

        [Test]
        public void FallingWithoutGroundRespawnsAndClearsInput()
        {
            KimchilyMobilePlayer player = KimchilyMobilePlayerBootstrap.CreateForScene(NewScene());
            player.Controller.enabled = false;
            player.transform.position = player.SpawnPosition - Vector3.up * 31;
            player.Controller.enabled = true;
            player.Controls.InputState.RequestJump();
            player.Controls.InputState.AddLookDelta(Vector2.one);
            player.Simulate(.02f, Vector2.zero, false);
            Assert.AreEqual(player.SpawnPosition, player.transform.position);
            Assert.AreEqual(0, player.VerticalVelocity);
            AssertCleared(player.Controls);
            Assert.IsNotNull(player.ViewCamera);
        }

        [Test]
        public void PlayerModelsRejectSelfHierarchyAndNestedPlayersBeforeCloning()
        {
            Scene scene = NewScene();
            KimchilyMobilePlayer player = KimchilyMobilePlayerBootstrap.CreateForScene(scene);
            Assert.Throws<System.ArgumentException>(() => player.ModelPrefab = player.gameObject);
            Assert.Throws<System.ArgumentException>(() => player.ModelPrefab = player.VisualRoot.gameObject);
            GameObject nestedPlayer = NewObject(scene, "Invalid nested player model");
            nestedPlayer.SetActive(false);
            nestedPlayer.AddComponent<KimchilyMobilePlayer>();
            Assert.Throws<System.ArgumentException>(() => player.ModelPrefab = nestedPlayer);
            Assert.IsNull(player.ModelPrefab);

            // Serialized Inspector data can bypass the public property; the execution
            // path and publish validation must enforce the same restriction.
            typeof(KimchilyMobilePlayer).GetField("modelPrefab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(player, nestedPlayer);
            Assert.IsNotEmpty(new List<string>(player.ValidateContent()));
            Transform originalVisual = player.VisualRoot;
            Assert.Throws<System.InvalidOperationException>(() => player.RefreshVisual());
            Assert.AreSame(originalVisual, player.VisualRoot);
            Assert.AreEqual(1, originalVisual.childCount);
        }

        [Test]
        public void ModelIsClonedFittedAndNeverChangesOriginalTransformOrColliders()
        {
            Scene sourceScene = NewScene();
            GameObject source = Primitive(sourceScene, PrimitiveType.Cube, "Selected model", new Vector3(600, 4, 30), new Vector3(2, 4, 3));
            source.SetActive(false);
            Vector3 originalPosition = source.transform.position;
            Vector3 originalScale = source.transform.localScale;
            Quaternion originalRotation = source.transform.rotation;
            Scene worldScene = NewScene();
            CreateFloor(worldScene);
            KimchilyMobilePlayer player = KimchilyMobilePlayerBootstrap.CreateForScene(worldScene, source);
            Assert.AreSame(source, player.ModelPrefab);
            Assert.AreEqual(originalPosition, source.transform.position);
            Assert.AreEqual(originalScale, source.transform.localScale);
            Assert.AreEqual(originalRotation, source.transform.rotation);
            Assert.IsFalse(source.activeSelf);
            Assert.IsTrue(source.GetComponent<Collider>().enabled);
            Assert.AreEqual(1, player.VisualRoot.childCount);
            GameObject clone = player.VisualRoot.GetChild(0).gameObject;
            Assert.AreNotSame(source, clone);
            Assert.AreEqual(worldScene, clone.scene);
            Assert.IsFalse(clone.GetComponent<Collider>().enabled);
            Bounds bounds = clone.GetComponent<Renderer>().bounds;
            Assert.That(bounds.size.y, Is.EqualTo(player.Controller.height).Within(.03f));
            Assert.That(bounds.min.y, Is.EqualTo(player.transform.position.y).Within(.03f));
        }

        private Scene NewScene()
        {
            Scene scene = SceneManager.CreateScene("Kimchily mobile test " + System.Guid.NewGuid().ToString("N"));
            scenes.Add(scene);
            return scene;
        }
        private static GameObject NewObject(Scene scene, string name)
        {
            var value = new GameObject(name);
            SceneManager.MoveGameObjectToScene(value, scene);
            return value;
        }
        private static GameObject Primitive(Scene scene, PrimitiveType type, string name, Vector3 position, Vector3 scale)
        {
            GameObject value = GameObject.CreatePrimitive(type);
            value.name = name;
            SceneManager.MoveGameObjectToScene(value, scene);
            value.transform.position = position;
            value.transform.localScale = scale;
            return value;
        }
        private static void CreateFloor(Scene scene) => Primitive(scene, PrimitiveType.Cube, "Floor", new Vector3(1000, -.5f, 0), new Vector3(30, 1, 30));
        private static void AssertRectInside(Rect parent, Rect child)
        {
            Assert.GreaterOrEqual(child.xMin, parent.xMin);
            Assert.GreaterOrEqual(child.yMin, parent.yMin);
            Assert.LessOrEqual(child.xMax, parent.xMax);
            Assert.LessOrEqual(child.yMax, parent.yMax);
        }
        private static void HoldAll(KimchilyMobileControls controls)
        {
            MobileControlLayout layout = controls.Layout;
            controls.InputState.BeginPointer(1, layout.Joystick.center + Vector2.up * layout.JoystickRadius, layout);
            controls.InputState.RequestJump();
            controls.InputState.AddLookDelta(Vector2.one);
        }
        private static void AssertCleared(KimchilyMobileControls controls)
        {
            Assert.AreEqual(Vector2.zero, controls.Move);
            Assert.AreEqual(Vector2.zero, controls.InputState.ConsumeLook());
            Assert.IsFalse(controls.InputState.ConsumeJump());
            Assert.IsFalse(controls.InputState.IsMovingPointer);
        }
    }
}
