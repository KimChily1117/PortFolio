using System;
using System.IO;
using Kimchily.World;
using Kimchily.Creator.Mobile;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kimchily.World.Editor
{
    public static class AndroidRuntimeBuilder
    {
        const string Generated = "Assets/Generated";
        const string BootstrapPath = Generated + "/Bootstrap.unity";
        const string DemoPath = Generated + "/DemoWorld.unity";

        [MenuItem("Kimchily/Android/Prepare Sample Scenes")]
        public static void PrepareScenes()
        {
            // This project owns these generated scenes. Never invoke on a user's
            // authoring project, or discard open dirty work in an interactive editor.
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("Scene preparation was cancelled.");
            if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder("Assets", "Generated");
            var bootstrap = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("KimchilyHostBridge").AddComponent<KimchilyHostBridge>();
            var bootstrapCamera = new GameObject("Bootstrap Camera").AddComponent<Camera>();
            bootstrapCamera.clearFlags = CameraClearFlags.SolidColor;
            bootstrapCamera.backgroundColor = new Color(0.04f, 0.06f, 0.1f);
            bootstrapCamera.cullingMask = 0;
            bootstrapCamera.depth = -10;
            EditorSceneManager.SaveScene(bootstrap, BootstrapPath);

            var demo = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.65f, 0.75f);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 2.2f, -5.5f);
            camera.transform.LookAt(new Vector3(0, 1.1f, 0));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.1f, 0.16f);
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(45, -35, 0);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "Display Platform";
            // A flattened Cylinder's default CapsuleCollider expands with X/Z
            // scale and floats above its visible top. Use the actual floor mesh.
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.AddComponent<MeshCollider>().sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;
            ground.transform.localScale = new Vector3(12, 0.08f, 12);
            ground.transform.position = new Vector3(0, -0.08f, 0);
            ground.GetComponent<Renderer>().sharedMaterial = MaterialAsset("Platform", new Color(0.08f, 0.24f, 0.29f));

            var spinner = new GameObject("Sample World");
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fixtures/Model.fbx");
            if (modelAsset == null) throw new InvalidOperationException("Run tools/prepare_runtime.py to copy the local FBX fixture first.");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Kimchily Sample Character";
            model.transform.SetParent(spinner.transform, false);
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("FBX has no renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.y > 0) model.transform.localScale *= 2f / bounds.size.y;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var characterMaterial = MaterialAsset("Character", new Color(0.35f, 0.85f, 0.72f));
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = characterMaterial;
                renderer.sharedMaterials = materials;
            }
            var behaviour = spinner.AddComponent<KimchilyDemoBehaviour>();
            behaviour.rotatingContent = model.transform;
            behaviour.animator = model.GetComponentInChildren<Animator>();
            // The bundled sample also demonstrates assigning an FBX to the controllable player.
            KimchilyMobilePlayerBootstrap.CreateForScene(demo, modelAsset);
            EditorSceneManager.SaveScene(demo, DemoPath);
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene(BootstrapPath, true),
                new EditorBuildSettingsScene(DemoPath, true)
            };
            EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("KIMCHILY_ANDROID_SCENES_READY");
        }

        static Material MaterialAsset(string name, Color color)
        {
            string path = Generated + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("Built-in Standard shader not found.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Glossiness", 0.2f);
            EditorUtility.SetDirty(material);
            return material;
        }

        [MenuItem("Kimchily/Android/Export Unity Library")]
        public static void Export()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Switch to Android, or launch Unity with -buildTarget Android.");
            PrepareScenes();
            PlayerSettings.companyName = "Kimchily";
            PlayerSettings.productName = "Kimchily World Runtime";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.kimchily.unityruntime");
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)32;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
            // Downloaded scenes can use built-in components absent from bootstrap/demo scenes.
            PlayerSettings.stripEngineCode = false;
            // The native host selects Landscape initially and exposes Portrait/Auto.
            // Allow all directions here so its policy can change without recreating Unity.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Android"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { BootstrapPath, DemoPath },
                target = BuildTarget.Android,
                locationPathName = output,
                options = BuildOptions.Development | BuildOptions.AcceptExternalModificationsToPlayer
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Unity Android export failed: " + report.summary.result);
            if (!File.Exists(Path.Combine(output, "unityLibrary", "build.gradle")))
                throw new InvalidOperationException("Export did not produce unityLibrary/build.gradle.");
            Debug.Log("KIMCHILY_ANDROID_EXPORT_READY " + output);
        }
    }
}
