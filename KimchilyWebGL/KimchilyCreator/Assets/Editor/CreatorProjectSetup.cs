using System;
using System.IO;
using Kimchily.Creator.Editor;
using Kimchily.Scripting;
using Kimchily.TypeScript;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kimchily.Creator.Project
{
    public static class CreatorProjectSetup
    {
        const string ScenePath = "Assets/World/MyWorld.unity";

        [MenuItem("Kimchily/Create Starter World")]
        public static void CreateStarterWorld()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("Starter world already exists; preserving your scene: " + ScenePath);
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.IsValidFolder("Assets/World")) AssetDatabase.CreateFolder("Assets", "World");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.72f, 0.8f);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 2.6f, -7);
            camera.transform.LookAt(new Vector3(0, 1, 0));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.075f, 0.12f);
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45, -35, 0);
            light.intensity = 1.1f;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "World Platform";
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.AddComponent<MeshCollider>().sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;
            ground.transform.localScale = new Vector3(4.5f, 0.1f, 4.5f);
            ground.transform.position = new Vector3(0, -0.1f, 0);
            ground.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Platform", new Color(0.09f, 0.3f, 0.35f));
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/World/Model.fbx");
            if (modelAsset == null) throw new InvalidOperationException("Run tools/prepare_creator.py first.");
            var pivot = new GameObject("My TypeScript Character");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.transform.SetParent(pivot.transform, false);
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.y > 0) model.transform.localScale *= 2 / bounds.size.y;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var material = MakeMaterial("Character", new Color(0.96f, 0.58f, 0.2f));
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "TypeScript Coroutine Beacon";
            beacon.transform.position = new Vector3(1.7f, 0.65f, 0);
            beacon.transform.localScale = Vector3.one * 0.5f;
            beacon.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Beacon", new Color(0.2f, 0.9f, 0.8f));
            AttachTypeScript(pivot, beacon);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "Kimchily";
            PlayerSettings.productName = "Kimchily Creator";
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            AssetDatabase.SaveAssets();
            Debug.Log("KIMCHILY_CREATOR_READY " + ScenePath);
        }

        static void AttachTypeScript(GameObject owner, GameObject beacon)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TypeScriptAsset>("Assets/World/Character.ts");
            if (asset == null || !asset.compiledSuccessfully)
                throw new InvalidOperationException("Character.ts must compile successfully before preparing the world.");
            var behaviour = owner.AddComponent<KimchilyTypeScriptBehaviour>();
            behaviour.ScriptAsset = asset;
            behaviour.Fields = new[] {
                new TypeScriptFieldBinding { name = "beacon", kind = "GameObject", useOverride = true, gameObjectValue = beacon }
            };
        }

        [MenuItem("Kimchily/Migrate Starter World to TypeScript")]
        public static void MigrateStarterWorldToTypeScript()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!File.Exists(ScenePath))
            {
                CreateStarterWorld();
                Debug.Log("KIMCHILY_TYPESCRIPT_MIGRATION_READY created=1");
                return;
            }
            var asset = AssetDatabase.LoadAssetAtPath<TypeScriptAsset>("Assets/World/Character.ts");
            if (asset == null || !asset.compiledSuccessfully)
                throw new InvalidOperationException("Fix TypeScript compiler errors before migrating the starter scene.");
            string backup = Path.Combine("Artifacts", "typescript-migration", "MyWorld.before-typescript.unity");
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int migrated = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var previous in root.GetComponentsInChildren<KimchilyLuaBehaviour>(true))
                {
                    if (AssetDatabase.GetAssetPath(previous.ScriptAsset) != "Assets/World/Character.lua") continue;
                    var owner = previous.gameObject;
                    if (owner.GetComponent<KimchilyTypeScriptBehaviour>() != null)
                        throw new InvalidOperationException("Starter object already has both script components; inspect it before migration.");
                    GameObject beacon = null;
                    foreach (var reference in previous.References)
                        if (reference != null && reference.name == "beacon") beacon = reference.target;
                    AttachTypeScript(owner, beacon);
                    UnityEngine.Object.DestroyImmediate(previous);
                    if (owner.name == "My Lua Character") owner.name = "My TypeScript Character";
                    if (beacon != null && beacon.name == "Lua Coroutine Beacon") beacon.name = "TypeScript Coroutine Beacon";
                    migrated++;
                }
            if (migrated > 0) EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("KIMCHILY_TYPESCRIPT_MIGRATION_READY converted=" + migrated);
        }

        static Material MakeMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Standard")) { color = color };
            material.SetFloat("_Glossiness", 0.25f);
            AssetDatabase.CreateAsset(material, "Assets/World/" + name + ".mat");
            return material;
        }

        public static void BuildStarterWorld()
        {
            CreateStarterWorld();
            var result = WorldContentBuilder.Build(new WorldBuildRequest {
                worldId = "my-first-world", entryScene = ScenePath, scenes = new[] { ScenePath },
                target = EditorUserBuildSettings.activeBuildTarget,
                requirePortableScripts = WorldContentBuilder.IsPublishTarget(EditorUserBuildSettings.activeBuildTarget),
                outputRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../WorldBuilds"))
            });
            Directory.CreateDirectory("Artifacts");
            File.WriteAllText("Artifacts/last-build.txt", result.Directory);
            Debug.Log("KIMCHILY_CREATOR_BUILD_READY " + result.Directory);
        }
    }
}
