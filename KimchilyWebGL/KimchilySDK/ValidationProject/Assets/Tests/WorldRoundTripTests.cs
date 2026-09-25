using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Kimchily.Creator;
using Kimchily.Creator.Content;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Kimchily.Validation.Tests
{
    public sealed class WorldRoundTripTests
    {
        [UnityTest]
        public IEnumerator BuiltFbxScenePreservesMeshesMaterialTextureAnimatorAndHierarchyAcrossReentry()
        {
            string locator = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/world-fixture-path.txt"));
            Assert.IsTrue(File.Exists(locator), "Run VerificationBootstrap.BuildFixture first.");
            string folder = File.ReadAllText(locator).Trim();
            for (int iteration = 0; iteration < 2; iteration++)
            {
                var session = WorldContentSession.OpenLocal(folder);
                yield return session.LoadEntrySceneAsync();
                Scene scene = SceneManager.GetSceneByPath(session.Manifest.entryScene);
                Assert.IsTrue(scene.isLoaded);
                var root = scene.GetRootGameObjects().Single(x => x.name == "KimchilyRoundTripRoot");
                Assert.IsNotNull(root.GetComponent<CoroutineScheduler>());
                Assert.AreEqual(new Vector3(2, 3, 4), root.transform.GetChild(0).localPosition);
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                Assert.Greater(renderers.Length, 0);
                foreach (var renderer in renderers)
                {
                    Assert.IsNotNull(renderer.sharedMaterial);
                    Assert.IsNotNull(renderer.sharedMaterial.mainTexture);
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        Assert.IsNotNull(skin.sharedMesh);
                        Assert.Greater(skin.sharedMesh.vertexCount, 0);
                        Assert.Greater(skin.bones.Length, 0);
                    }
                    else if (renderer.GetComponent<MeshFilter>() is MeshFilter filter)
                        Assert.IsNotNull(filter.sharedMesh);
                }
                var animator = root.GetComponentInChildren<Animator>(true);
                Assert.IsNotNull(animator.runtimeAnimatorController);
                Assert.Greater(animator.runtimeAnimatorController.animationClips.Length, 0);
                Assert.Throws<InvalidOperationException>(() => session.Dispose());
                yield return SceneManager.UnloadSceneAsync(scene);
                session.Dispose();
                session.Dispose();
            }
        }
    }
}
