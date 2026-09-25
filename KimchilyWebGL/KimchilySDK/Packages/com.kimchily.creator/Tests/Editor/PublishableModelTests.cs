using System;
using System.IO;
using System.Linq;
using Kimchily.Creator.Mobile;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Kimchily.Creator.Editor.Tests
{
    public sealed class PublishableModelTests
    {
        string folder;
        Scene preview;
        GameObject holder;
        string preparedPlayerAsset;
        bool createdPlayerFolder;

        [SetUp]
        public void SetUp()
        {
            folder = "Assets/__KimchilyModelTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            preview = EditorSceneManager.NewPreviewScene();
            holder = new GameObject("Test Holder") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false);
            SceneManager.MoveGameObjectToScene(holder, preview);
            preparedPlayerAsset = null;
            createdPlayerFolder = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (!string.IsNullOrEmpty(preparedPlayerAsset)) AssetDatabase.DeleteAsset(preparedPlayerAsset);
            if (createdPlayerFolder && Directory.Exists("Assets/PublishedModels") &&
                Directory.GetFileSystemEntries("Assets/PublishedModels").Length == 0)
                AssetDatabase.DeleteAsset("Assets/PublishedModels");
            if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
        }

        GameObject NewObject(string name)
        {
            var value = new GameObject(name);
            value.transform.SetParent(holder.transform, false);
            return value;
        }

        GameObject SourceWithScripts(string name = "Source")
        {
            GameObject root = NewObject(name);
            Assert.IsNotNull(root.AddComponent<PublishableModelRequiredBehaviour>(), "The fixture must be a runtime-attachable script, not an Editor-only script.");
            root.AddComponent<CoroutineScheduler>();
            GameObject source = PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + name + ".prefab");
            Assert.AreEqual(1, source.GetComponentsInChildren<PublishableModelRequiredBehaviour>(true).Length, "The original prefab must actually serialize the external component.");
            Assert.AreEqual(2, PublishableModelUtility.GetUnsupportedScripts(source).Length);
            return source;
        }

        static void AssertSameAsset(Object expected, Object actual)
        {
            // Prefab import can replace managed wrappers. Preserve the serialized asset identity,
            // including subasset IDs, rather than requiring C# reference identity after import.
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(expected, out string expectedGuid, out long expectedId));
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(actual, out string actualGuid, out long actualId));
            Assert.AreEqual(expectedGuid, actualGuid);
            Assert.AreEqual(expectedId, actualId);
        }

        [Test]
        public void NestedCopyPreservesSkinMaterialAndAnimatorWithoutOriginalPrefabDependencies()
        {
            var mesh = new Mesh { name = "Fixture Mesh" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, folder + "/Mesh.asset");
            var material = new Material(Shader.Find("Unlit/Color"));
            AssetDatabase.CreateAsset(material, folder + "/Surface.mat");
            var clip = new AnimationClip { name = "Idle" };
            AssetDatabase.CreateAsset(clip, folder + "/Idle.anim");
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Visual.controller");
            controller.layers[0].stateMachine.AddState("Idle").motion = clip;
            AssetDatabase.SaveAssetIfDirty(controller);

            GameObject child = NewObject("Nested Model");
            var bone = new GameObject("Bone");
            bone.transform.SetParent(child.transform, false);
            var skin = child.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh; skin.sharedMaterial = material;
            skin.rootBone = bone.transform; skin.bones = new[] { bone.transform };
            var avatar = AvatarBuilder.BuildGenericAvatar(child, "");
            AssetDatabase.CreateAsset(avatar, folder + "/Skeleton.asset");
            Animator animator = child.AddComponent<Animator>();
            animator.avatar = avatar; animator.runtimeAnimatorController = controller; animator.applyRootMotion = true;
            Assert.IsNotNull(child.AddComponent<PublishableModelRequiredBehaviour>());
            child.AddComponent<CoroutineScheduler>();
            GameObject nested = PrefabUtility.SaveAsPrefabAsset(child, folder + "/Nested.prefab");
            GameObject root = NewObject("Outer");
            var nestedInstance = (GameObject)PrefabUtility.InstantiatePrefab(nested, root.transform);
            nestedInstance.transform.localPosition = new Vector3(1, 2, 3);
            GameObject source = PrefabUtility.SaveAsPrefabAsset(root, folder + "/Source.prefab");
            Assert.AreEqual(2, PublishableModelUtility.GetUnsupportedScripts(source).Length, "The nested source must contain both unsupported scripts before conversion.");
            byte[] sourceBytes = File.ReadAllBytes(folder + "/Source.prefab");
            byte[] nestedBytes = File.ReadAllBytes(folder + "/Nested.prefab");
            PublishableModelExternalBehaviour.EnableCount = 0;

            GameObject copy = PublishableModelUtility.CreateCopy(source, folder + "/Copy.prefab", out string[] removed);

            CollectionAssert.AreEquivalent(new[] { typeof(PublishableModelExternalBehaviour).FullName,
                typeof(PublishableModelRequiredBehaviour).FullName }, removed);
            Assert.AreEqual(0, copy.GetComponentsInChildren<PublishableModelExternalBehaviour>(true).Length);
            Assert.AreEqual(1, copy.GetComponentsInChildren<CoroutineScheduler>(true).Length);
            Assert.AreEqual(0, PublishableModelExternalBehaviour.EnableCount, "Preparing an asset must not enable its ExecuteAlways scripts.");
            SkinnedMeshRenderer copiedSkin = copy.GetComponentInChildren<SkinnedMeshRenderer>(true);
            AssertSameAsset(mesh, copiedSkin.sharedMesh);
            AssertSameAsset(material, copiedSkin.sharedMaterial);
            Assert.IsTrue(copiedSkin.rootBone.IsChildOf(copy.transform));
            Assert.AreEqual(copiedSkin.rootBone.GetInstanceID(), copiedSkin.bones[0].GetInstanceID());
            Animator copiedAnimator = copy.GetComponentInChildren<Animator>(true);
            AssertSameAsset(controller, copiedAnimator.runtimeAnimatorController);
            AssertSameAsset(avatar, copiedAnimator.avatar);
            Assert.IsFalse(copiedAnimator.applyRootMotion);
            Assert.AreEqual(new Vector3(1, 2, 3), copy.transform.GetChild(0).localPosition);
            string[] dependencies = AssetDatabase.GetDependencies(folder + "/Copy.prefab", true);
            CollectionAssert.DoesNotContain(dependencies, folder + "/Source.prefab");
            CollectionAssert.DoesNotContain(dependencies, folder + "/Nested.prefab");
            CollectionAssert.Contains(dependencies, folder + "/Visual.controller");
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(folder + "/Source.prefab"));
            CollectionAssert.AreEqual(nestedBytes, File.ReadAllBytes(folder + "/Nested.prefab"));
        }

        [Test]
        public void StaticMeshModelNeedsNoAnimatorOrCharacterSpecificNames()
        {
            var mesh = new Mesh { name = "Custom Prop Mesh" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, folder + "/PropMesh.asset");
            var material = new Material(Shader.Find("Unlit/Color"));
            AssetDatabase.CreateAsset(material, folder + "/PropMaterial.mat");
            GameObject sourceObject = NewObject("Arbitrary Robot Prop");
            var child = new GameObject("Custom Geometry");
            child.transform.SetParent(sourceObject.transform, false);
            child.transform.localPosition = new Vector3(2, 3, 4);
            child.transform.localScale = new Vector3(1, 2, 3);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
            GameObject source = PrefabUtility.SaveAsPrefabAsset(sourceObject, folder + "/Prop.prefab");
            byte[] original = File.ReadAllBytes(folder + "/Prop.prefab");

            GameObject copy = PublishableModelUtility.CreateCopy(source, folder + "/PropCopy.prefab", out string[] removed);

            Assert.IsEmpty(removed);
            Assert.IsEmpty(copy.GetComponentsInChildren<Animator>(true));
            AssertSameAsset(mesh, copy.GetComponentInChildren<MeshFilter>(true).sharedMesh);
            AssertSameAsset(material, copy.GetComponentInChildren<MeshRenderer>(true).sharedMaterial);
            Assert.AreEqual(new Vector3(2, 3, 4), copy.transform.GetChild(0).localPosition);
            Assert.AreEqual(new Vector3(1, 2, 3), copy.transform.GetChild(0).localScale);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(folder + "/Prop.prefab"));
        }

        [Test]
        public void ImportedFbxCanBeUsedDirectlyWithoutACharacterPrefab()
        {
            const string path = "Assets/Fixtures/Model.fbx";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(source, "The validation project's imported FBX fixture must exist.");
            Assert.AreEqual(PrefabAssetType.Model, PrefabUtility.GetPrefabAssetType(source));
            byte[] original = File.ReadAllBytes(path);

            GameObject copy = PublishableModelUtility.CreateCopy(source, folder + "/ImportedCopy.prefab", out string[] removed);

            Assert.IsEmpty(removed);
            Assert.AreEqual(source.GetComponentsInChildren<Renderer>(true).Length, copy.GetComponentsInChildren<Renderer>(true).Length);
            Assert.Greater(copy.GetComponentsInChildren<Renderer>(true).Length, 0);
            CollectionAssert.Contains(AssetDatabase.GetDependencies(folder + "/ImportedCopy.prefab", true), path);
            foreach (Animator animator in copy.GetComponentsInChildren<Animator>(true)) Assert.IsFalse(animator.applyRootMotion);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        }

        [Test]
        public void ExistingAssetsAreNeverOverwritten()
        {
            GameObject source = SourceWithScripts();
            string path = AssetDatabase.GetAssetPath(source);
            byte[] original = File.ReadAllBytes(path);
            Assert.Throws<IOException>(() => PublishableModelUtility.CreateCopy(source, path, out _));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        }

        [TestCase("Packages/Copy.prefab")]
        [TestCase("Assets/../Copy.prefab")]
        [TestCase("Assets/Copy.asset")]
        public void OutputMustBeANewPrefabBeneathAssets(string path)
        {
            Assert.Throws<ArgumentException>(() => PublishableModelUtility.CreateCopy(SourceWithScripts(), path, out _));
        }

        [Test]
        public void ExternalAnimatorBehaviourIsRejectedAndNewOutputIsRemoved()
        {
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/External.controller");
            Assert.IsNotNull(controller.layers[0].stateMachine.AddState("Idle").AddStateMachineBehaviour<PublishableModelStateBehaviour>(),
                "The fixture must contain a serialized runtime StateMachineBehaviour.");
            AssetDatabase.SaveAssetIfDirty(controller);
            Assert.IsTrue(AssetDatabase.LoadAllAssetsAtPath(folder + "/External.controller").OfType<PublishableModelStateBehaviour>().Any());
            GameObject root = NewObject("Source");
            root.AddComponent<Animator>().runtimeAnimatorController = controller;
            GameObject source = PrefabUtility.SaveAsPrefabAsset(root, folder + "/Source.prefab");
            byte[] original = File.ReadAllBytes(folder + "/External.controller");
            string output = folder + "/Rejected.prefab";
            var error = Assert.Throws<InvalidOperationException>(() => PublishableModelUtility.CreateCopy(source, output, out _));
            StringAssert.Contains(typeof(PublishableModelStateBehaviour).FullName, error.Message);
            Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(output));
            Assert.IsFalse(File.Exists(output));
            Assert.IsFalse(File.Exists(output + ".meta"));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(folder + "/External.controller"));
        }

        [Test]
        public void PreparingAPlayerReplacesItsOldVisualAndKeepsSourceAsset()
        {
            GameObject source = SourceWithScripts("PlayerModel_" + Guid.NewGuid().ToString("N"));
            byte[] original = File.ReadAllBytes(AssetDatabase.GetAssetPath(source));
            GameObject root = NewObject("Player");
            var player = root.AddComponent<KimchilyMobilePlayer>();
            player.ModelPrefab = source;
            player.RefreshVisual();
            Assert.AreEqual(1, player.VisualRoot.GetComponentsInChildren<PublishableModelExternalBehaviour>(true).Length);
            createdPlayerFolder = !AssetDatabase.IsValidFolder("Assets/PublishedModels");

            preparedPlayerAsset = MobilePlayerModelPreparation.PreparePlayer(player);

            Assert.AreEqual(preparedPlayerAsset, AssetDatabase.GetAssetPath(player.ModelPrefab));
            Assert.AreNotSame(source, player.ModelPrefab);
            Assert.AreEqual(0, PublishableModelUtility.GetUnsupportedScripts(player.ModelPrefab).Length);
            Assert.AreEqual(0, player.VisualRoot.GetComponentsInChildren<PublishableModelExternalBehaviour>(true).Length);
            Assert.AreEqual(1, player.VisualRoot.GetComponentsInChildren<CoroutineScheduler>(true).Length);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(AssetDatabase.GetAssetPath(source)));
        }
    }
}
