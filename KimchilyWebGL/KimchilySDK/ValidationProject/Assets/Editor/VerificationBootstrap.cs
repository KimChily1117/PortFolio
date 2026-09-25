using System;
using System.IO;
using System.Linq;
using Kimchily.Creator;
using Kimchily.Creator.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kimchily.Validation
{
    public static class VerificationBootstrap
    {
        public static void BuildFixture()
        {
            string folderName = "ValidationFixture_" + Guid.NewGuid().ToString("N");
            string folder = "Assets/" + folderName;
            AssetDatabase.CreateFolder("Assets", folderName);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var texture = new Texture2D(2, 2);
            texture.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white }); texture.Apply();
            AssetDatabase.CreateAsset(texture, folder + "/Texture.asset");
            var material = new Material(Shader.Find("Unlit/Texture")) { mainTexture = texture };
            AssetDatabase.CreateAsset(material, folder + "/Material.mat");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fixtures/Model.fbx");
            if (model == null) throw new InvalidOperationException("The FBX test fixture did not import.");
            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            modelInstance.name = "ImportedModel";
            foreach (var renderer in modelInstance.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(x => material).ToArray();
            var clip = new AnimationClip();
            new GameObject("AnimationProbe").transform.SetParent(modelInstance.transform, false);
            clip.SetCurve("AnimationProbe", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
            AssetDatabase.CreateAsset(clip, folder + "/Motion.anim");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Animation.controller");
            controller.AddMotion(clip);
            var animator = modelInstance.GetComponent<Animator>();
            if (animator == null) animator = modelInstance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            var nested = PrefabUtility.SaveAsPrefabAsset(modelInstance, folder + "/NestedModel.prefab");
            UnityEngine.Object.DestroyImmediate(modelInstance);
            var root = new GameObject("KimchilyRoundTripRoot");
            root.AddComponent<CoroutineScheduler>();
            var child = (GameObject)PrefabUtility.InstantiatePrefab(nested);
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(2, 3, 4);
            string scenePath = folder + "/World.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            // Close authoring scene before the runtime tests load the bundled copy.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var request = new WorldBuildRequest
            {
                worldId = "fbx-roundtrip", entryScene = scenePath, scenes = new[] { scenePath },
                outputRoot = Path.GetFullPath("../Artifacts/content"),
                target = BuildTarget.StandaloneWindows64
            };
            var report = WorldContentBuilder.Validate(request);
            if (!report.IsValid) throw new InvalidOperationException(string.Join("\n", report.Errors));
            if (!report.Dependencies.Contains("Assets/Fixtures/Model.fbx") ||
                !report.Dependencies.Contains(folder + "/Texture.asset") ||
                !report.Dependencies.Contains(folder + "/Animation.controller"))
                throw new InvalidOperationException("FBX/material/animation dependency collection failed.");
            var result = WorldContentBuilder.Build(request);
            File.WriteAllText(Path.GetFullPath("../Artifacts/world-fixture-path.txt"), result.Directory);
            Debug.Log("KIMCHILY_FIXTURE_BUILT " + result.Directory);
        }
    }
}
