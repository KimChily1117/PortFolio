using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class DemoBuild
{
    private const string ScenePath = "Assets/Scenes/Demo.unity";

    [MenuItem("DNF Demo/Configure and Validate Project")]
    public static void ValidateProject()
    {
        Configure();
        EnsureScene();
        Debug.Log("[DNF_BUILD_VALIDATED] Unity=" + Application.unityVersion
            + "; target=StandaloneWindows64; backend=Mono; scene=" + ScenePath);
    }

    [MenuItem("DNF Demo/Build Windows Demo")]
    public static void BuildWindows()
    {
        try
        {
            ValidateProject();
            string projectDirectory = Directory.GetParent(Application.dataPath).FullName;
            string outputDirectory = Path.GetFullPath(Path.Combine(projectDirectory, "..", "Build"));
            Directory.CreateDirectory(outputDirectory);
            string executablePath = Path.Combine(outputDirectory, "DnfRaidDemo.exe");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            });
            var summary = report.summary;
            string result = "result=" + summary.result + "; errors=" + summary.totalErrors
                + "; warnings=" + summary.totalWarnings + "; bytes=" + summary.totalSize
                + "; output=" + executablePath;
            File.WriteAllText(Path.Combine(outputDirectory, "build-summary.txt"),
                "Unity " + Application.unityVersion + Environment.NewLine + result + Environment.NewLine);
            var diagnostics = new System.Text.StringBuilder();
            foreach (var step in report.steps)
                foreach (var message in step.messages)
                    if (message.type != LogType.Log)
                    {
                        string detail = "[" + message.type + "] " + step.name + ": " + message.content;
                        diagnostics.AppendLine(detail);
                        Debug.Log("[DNF_BUILD_DIAGNOSTIC] " + detail);
                    }
            File.WriteAllText(Path.Combine(outputDirectory, "build-diagnostics.txt"), diagnostics.ToString());
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Standalone build failed: " + result);
            Debug.Log("[DNF_BUILD_SUCCEEDED] " + result);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    private static void Configure()
    {
        PlayerSettings.companyName = "Local Prototype";
        PlayerSettings.productName = "DNF Raid Demo";
        PlayerSettings.bundleVersion = "0.1.2";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "local.prototype.dnfraid");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Disabled);
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.usePlayerLog = true;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
            new[] { GraphicsDeviceType.Direct3D11 });
        QualitySettings.vSyncCount = 0;
        QualitySettings.antiAliasing = 0;
        AssetDatabase.SaveAssets();
    }

    private static void EnsureScene()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));
        // RuntimeInitializeOnLoadMethod owns the camera, arena, actors and UI.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new IOException("Could not save bootstrap scene: " + ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
    }
}

