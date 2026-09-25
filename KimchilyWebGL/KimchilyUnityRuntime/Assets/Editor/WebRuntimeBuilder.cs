using System;
using System.IO;
using System.Linq;
using Kimchily.TypeScript;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kimchily.World.Editor
{
    public static class WebRuntimeBuilder
    {
        const string BootstrapPath = "Assets/Generated/Bootstrap.unity";
        const string DemoPath = "Assets/Generated/DemoWorld.unity";
        const string ScriptPath = "Assets/Generated/WebDemo.ts";

        [MenuItem("Kimchily/Web/Prepare Sample Scenes")]
        public static void PrepareScenes()
        {
            AndroidRuntimeBuilder.PrepareScenes();
            var bootstrap = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            var bridge = bootstrap.GetRootGameObjects().Select(x => x.GetComponent<KimchilyHostBridge>()).First(x => x != null);
            bridge.gameObject.AddComponent<KimchilyWebBridge>();
            EditorSceneManager.SaveScene(bootstrap, BootstrapPath);

            // Import real TypeScript through the same pinned compiler used by creators.
            File.WriteAllText(Path.Combine(Application.dataPath, "Generated/WebDemo.ts"),
                "import { KimchilyScriptBehaviour } from 'Kimchily.Script';\n" +
                "import { Debug, Time, WaitForSeconds } from 'UnityEngine';\n" +
                "export default class WebDemo extends KimchilyScriptBehaviour {\n" +
                "  public speed: number = 30;\n" +
                "  private starts: Map<string, number> = new Map<string, number>();\n" +
                "  Start(): void { this.starts.set('count', 1); this.StartCoroutine(this.Ready()); }\n" +
                "  private *Ready(): Generator<WaitForSeconds, void, unknown> {\n" +
                "    yield new WaitForSeconds(0.1); Debug.Log('KIMCHILY_WEB_TYPESCRIPT_READY ' + this.starts.get('count'));\n" +
                "  }\n" +
                "  Update(): void { this.transform.Rotate(0, this.speed * Time.deltaTime, 0); }\n" +
                "}\n");
            AssetDatabase.ImportAsset(ScriptPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var script = AssetDatabase.LoadAssetAtPath<TypeScriptAsset>(ScriptPath);
            if (script == null || !script.compiledSuccessfully)
                throw new InvalidOperationException("Web demo TypeScript compilation failed. " +
                    (script == null ? "The TypeScript importer did not produce an asset." : string.Join("\n", script.diagnostics)));

            var demo = EditorSceneManager.OpenScene(DemoPath, OpenSceneMode.Single);
            // Opening a scene unloads unused native assets; a managed local alone
            // does not keep the imported ScriptableObject alive across that step.
            script = AssetDatabase.LoadAssetAtPath<TypeScriptAsset>(ScriptPath);
            if (script == null || !script.compiledSuccessfully)
                throw new InvalidOperationException("The compiled Web demo TypeScript asset could not be reloaded after opening DemoWorld.");
            var sample = demo.GetRootGameObjects().First(x => x.name == "Sample World");
            // Browser HTML owns presentation; remove the native demo overlay/rotation.
            UnityEngine.Object.DestroyImmediate(sample.GetComponent<KimchilyDemoBehaviour>());
            var behaviour = sample.AddComponent<KimchilyTypeScriptBehaviour>();
            behaviour.ScriptAsset = script;
            string[] errors = behaviour.ValidateContent().ToArray();
            if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors));
            EditorSceneManager.SaveScene(demo, DemoPath);
            EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("KIMCHILY_WEBGL_SCENES_READY");
        }

        [MenuItem("Kimchily/Web/Build Browser Player")]
        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("Switch to Web, or launch Unity with -buildTarget WebGL.");
            if (!File.Exists("Assets/link.xml"))
                throw new InvalidOperationException("Assets/link.xml is required to preserve downloaded world script types.");
            PrepareScenes();
            PlayerSettings.companyName = "Kimchily";
            PlayerSettings.productName = "Kimchily Web Worlds";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.WebGL, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.runInBackground = false;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            PlayerSettings.WebGL.template = "PROJECT:KimchilyWeb";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.threadsSupport = false;
            // Acornima/Jint and .NET culture APIs load embedded assembly resources.
            // Web builds omit them by default even when link.xml preserves the DLLs.
            PlayerSettings.WebGL.useEmbeddedResources = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            // Avoid stale player data during the development/republication loop.
            PlayerSettings.WebGL.dataCaching = false;
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/WebGL"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { BootstrapPath, DemoPath }, target = BuildTarget.WebGL,
                locationPathName = output, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded || !File.Exists(Path.Combine(output, "index.html")))
                throw new InvalidOperationException("Unity Web build failed: " + report.summary.result);
            Debug.Log("KIMCHILY_WEBGL_BUILD_READY " + output);
        }
    }
}
