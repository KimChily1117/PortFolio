using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kimchily.Creator.Editor.Tests
{
    public sealed class WorldContentBuilderTests
    {
        string folder;
        Scene scene;
        Scene original;
        string scenePath;
        string prefabPath;
        string texturePath;
        string materialPath;
        string startupSceneAnchorPath;
        bool restoreUntitledScene;
        NewSceneSetup startupSceneSetup;
        string[] startupRoots;
        int startupSceneCount;

        [SetUp]
        public void SetUp()
        {
            // NUnit reuses this fixture instance. Clear state before a precondition
            // can skip setup, so teardown cannot touch an earlier test's assets.
            folder = null;
            scene = default;
            startupSceneAnchorPath = null;
            restoreUntitledScene = false;
            original = SceneManager.GetActiveScene();
            startupSceneCount = SceneManager.sceneCount;
            bool hasUntitledScene = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt).Any(open => string.IsNullOrEmpty(open.path));
            if (hasUntitledScene)
            {
                // Test Framework 1.1.33 EditModeLauncher.OpenNewScene creates a
                // clean DefaultGameObjects scene when starting with one untitled
                // scene, or a clean empty additive scene alongside saved scenes.
                // Adapt only these runner baselines; never save dirty scenes or
                // arbitrary untitled content to work around Unity's restriction.
                bool otherUntitledScene = Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt)
                    .Any(open => open != original && string.IsNullOrEmpty(open.path));
                if (!original.IsValid() || !string.IsNullOrEmpty(original.path) || original.isDirty ||
                    otherUntitledScene || (original.rootCount != 0 &&
                        (SceneManager.sceneCount != 1 || !HasDefaultGameObjects(original))))
                    Assert.Ignore("Additive scene tests require the clean empty/default scene created by Test Runner. User scenes were left unchanged.");
                startupSceneSetup = original.rootCount == 0
                    ? NewSceneSetup.EmptyScene : NewSceneSetup.DefaultGameObjects;
                startupRoots = DescribeRoots(original);
                restoreUntitledScene = true;
            }

            try { CreateFixture(); }
            catch
            {
                // NUnit need not invoke TearDown when SetUp itself fails.
                TearDown();
                throw;
            }
        }

        void CreateFixture()
        {
            folder = "Assets/__KimchilyTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            if (restoreUntitledScene)
            {
                startupSceneAnchorPath = folder + "/TestRunnerStartupScene.unity";
                Assert.IsTrue(EditorSceneManager.SaveScene(original, startupSceneAnchorPath),
                    "Could not save the Test Runner anchor scene.");
            }
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var texture = new Texture2D(2, 2);
            texture.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white });
            texture.Apply();
            texturePath = folder + "/Color.asset";
            AssetDatabase.CreateAsset(texture, texturePath);
            var material = new Material(Shader.Find("Unlit/Texture"));
            material.mainTexture = texture;
            materialPath = folder + "/Surface.mat";
            AssetDatabase.CreateAsset(material, materialPath);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.GetComponent<Renderer>().sharedMaterial = material;
            prefabPath = folder + "/Prop.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(cube, prefabPath);
            UnityEngine.Object.DestroyImmediate(cube);
            PrefabUtility.InstantiatePrefab(prefab, scene);
            scenePath = folder + "/World.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.CloseScene(scene, true);
        }

        [TearDown]
        public void TearDown()
        {
            if (scene.IsValid() && scene.isLoaded)
                Assert.IsTrue(EditorSceneManager.CloseScene(scene, true), "Could not close this test's scene.");
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (restoreUntitledScene && original.IsValid() && original.isLoaded &&
                original.path == startupSceneAnchorPath)
            {
                // If another editor extension/user changed the anchor during the
                // test, preserve it and its assets rather than discarding work.
                Assert.AreEqual(startupSceneCount, SceneManager.sceneCount);
                Assert.IsFalse(original.isDirty,
                    "The Test Runner startup scene changed. Its scene and temporary assets were preserved.");
                CollectionAssert.AreEqual(startupRoots, DescribeRoots(original),
                    "The Test Runner startup objects changed. Its scene and temporary assets were preserved.");
                // Additive replacement preserves every other scene, including
                // dirty named user scenes. Close only our temporary anchor.
                Scene restored = EditorSceneManager.NewScene(startupSceneSetup, NewSceneMode.Additive);
                Assert.IsTrue(EditorSceneManager.CloseScene(original, true));
                SceneManager.SetActiveScene(restored);
                Assert.IsTrue(restored.IsValid() && restored.isLoaded);
                Assert.IsTrue(string.IsNullOrEmpty(restored.path));
                Assert.IsFalse(restored.isDirty);
                Assert.AreEqual(startupRoots.Length, restored.rootCount);
                CollectionAssert.AreEqual(startupRoots, DescribeRoots(restored));
                Assert.AreEqual(startupSceneCount, SceneManager.sceneCount);
                restoreUntitledScene = false;
            }
            // Only this test's uniquely created Assets folder can be removed.
            if (!string.IsNullOrEmpty(folder) && folder.StartsWith("Assets/__KimchilyTests_", StringComparison.Ordinal))
            {
                Assert.IsFalse(Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt)
                    .Any(open => open.path.StartsWith(folder + "/", StringComparison.Ordinal)),
                    "A temporary scene is still open. Its assets were preserved to avoid losing work.");
                AssetDatabase.DeleteAsset(folder);
            }
        }

        static bool HasDefaultGameObjects(Scene candidate)
        {
            GameObject[] roots = candidate.GetRootGameObjects();
            if (roots.Length != 2 || roots.Any(root => root.transform.childCount != 0)) return false;
            GameObject camera = roots.SingleOrDefault(root => root.name == "Main Camera");
            GameObject light = roots.SingleOrDefault(root => root.name == "Directional Light");
            return camera != null && light != null && camera.CompareTag("MainCamera") &&
                camera.GetComponent<Camera>() != null && camera.GetComponent<AudioListener>() != null &&
                camera.GetComponents<Component>().Length == 3 && light.GetComponent<Light>() != null &&
                light.GetComponent<Light>().type == LightType.Directional &&
                light.GetComponents<Component>().Length == 2;
        }

        static string[] DescribeRoots(Scene candidate)
        {
            // Recreated built-in objects have new instance IDs; compare their
            // serialized state while excluding only those transient references.
            return candidate.GetRootGameObjects().OrderBy(root => root.name, StringComparer.Ordinal)
                .Select(root => root.name + "\n" + NormalizeObjectIds(EditorJsonUtility.ToJson(root)) + "\n" +
                    string.Join("\n", root.GetComponents<Component>().Select(component =>
                        component.GetType().FullName + ":" + NormalizeObjectIds(EditorJsonUtility.ToJson(component)))))
                .ToArray();
        }

        static string NormalizeObjectIds(string json) =>
            Regex.Replace(json, "\\\"instanceID\\\"\\s*:\\s*-?\\d+", "\"instanceID\":0");

        WorldBuildRequest Request() => new WorldBuildRequest
        {
            worldId = "test-world", entryScene = scenePath, scenes = new[] { scenePath }
        };

        [Test]
        public void CompilerDiagnosticsBlockBuildBeforeCreatingOutput()
        {
            string output = Path.GetFullPath("Temp/RejectedScript-" + Guid.NewGuid().ToString("N"));
            var request = Request();
            request.outputRoot = output;
            void Reject(string[] dependencies, System.Collections.Generic.List<string> errors)
            {
                Assert.Contains(scenePath, dependencies);
                errors.Add("Character.ts(4,1): incompatible public field type");
            }
            WorldContentBuilder.ValidateAdditionalAssets += Reject;
            try
            {
                var report = WorldContentBuilder.Validate(request);
                Assert.IsFalse(report.IsValid);
                Assert.IsTrue(report.Errors.Any(error => error.Contains("Character.ts")));
                Assert.Throws<InvalidOperationException>(() => WorldContentBuilder.Build(request));
                Assert.IsFalse(Directory.Exists(output));
            }
            finally { WorldContentBuilder.ValidateAdditionalAssets -= Reject; }
        }

        [Test]
        public void SceneReferencesCollectNestedPrefabMaterialAndTextureWithoutChangingLabels()
        {
            var importer = AssetImporter.GetAtPath(prefabPath);
            importer.assetBundleName = "existing-user-label";
            importer.SaveAndReimport();
            int sceneCount = SceneManager.sceneCount;
            var report = WorldContentBuilder.Validate(Request());
            Assert.IsTrue(report.IsValid, string.Join("\n", report.Errors));
            CollectionAssert.IsSubsetOf(new[] { prefabPath, materialPath, texturePath }, report.BundleAssets);
            Assert.AreEqual("existing-user-label", AssetImporter.GetAtPath(prefabPath).assetBundleName);
            Assert.AreEqual(sceneCount, SceneManager.sceneCount);
            Assert.AreEqual(original, SceneManager.GetActiveScene());
        }

        [Test]
        public void DynamicallyLoadedAssetIsIncludedOnlyWhenDeclared()
        {
            string path = folder + "/Dynamic.txt";
            File.WriteAllText(path, "runtime script module");
            AssetDatabase.ImportAsset(path);
            var request = Request();
            CollectionAssert.DoesNotContain(WorldContentBuilder.Validate(request).BundleAssets, path);
            request.additionalAssets = new[] { path };
            CollectionAssert.Contains(WorldContentBuilder.Validate(request).BundleAssets, path);
        }

        [Test]
        public void MissingEntrySceneBlocksBuild()
        {
            var request = Request();
            request.entryScene = folder + "/NotSelected.unity";
            Assert.IsFalse(WorldContentBuilder.Validate(request).IsValid);
        }

        [Test]
        public void FolderCannotBeUsedAsAnAdditionalAsset()
        {
            var request = Request();
            request.additionalAssets = new[] { folder };
            Assert.IsFalse(WorldContentBuilder.Validate(request).IsValid);
        }

        [Test]
        public void DirtySceneIsRejectedWithoutSavingOrClosingIt()
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var marker = new GameObject("UnsavedChange");
            SceneManager.MoveGameObjectToScene(marker, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            var report = WorldContentBuilder.Validate(Request());
            Assert.IsFalse(report.IsValid);
            Assert.IsTrue(scene.isDirty);
            Assert.IsTrue(scene.isLoaded);
        }

        [Test]
        public void OutputInsideAssetsIsRejectedBeforeBuilding()
        {
            var request = Request();
            request.outputRoot = Path.GetFullPath(folder);
            Assert.Throws<ArgumentException>(() => WorldContentBuilder.Build(request));
        }
    }
}
