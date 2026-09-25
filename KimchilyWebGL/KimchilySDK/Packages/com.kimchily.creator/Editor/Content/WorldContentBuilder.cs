using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kimchily.Creator.Content;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kimchily.Creator.Editor
{
    public sealed class WorldBuildRequest
    {
        public string worldId = "sample-world";
        public string entryScene;
        public string[] scenes = Array.Empty<string>();
        // Assets loaded by string/require are not discoverable from scene references.
        public string[] additionalAssets = Array.Empty<string>();
        public string outputRoot;
        public BuildTarget target = BuildTarget.StandaloneWindows64;
        // Published worlds execute installed SDK components plus interpreted scripts.
        public bool requirePortableScripts;
    }

    public sealed class WorldValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public string[] Dependencies = Array.Empty<string>();
        public string[] BundleAssets = Array.Empty<string>();
        public ScriptRequirement[] RequiredTypes = Array.Empty<ScriptRequirement>();
        public bool IsValid => Errors.Count == 0;
    }

    public sealed class WorldBuildResult
    {
        public string Directory { get; internal set; }
        public WorldContentManifest Manifest { get; internal set; }
        public WorldValidationReport Validation { get; internal set; }
    }

    public static class WorldContentBuilder
    {
        // Optional scripting importers add compiler/freshness checks without a reverse package dependency.
        public static event Action<string[], List<string>> ValidateAdditionalAssets;

        static readonly HashSet<string> CodeExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".cs", ".dll", ".asmdef", ".asmref", ".rsp", ".meta" };

        public static WorldValidationReport Validate(WorldBuildRequest request)
        {
            var report = new WorldValidationReport();
            if (request == null)
            {
                report.Errors.Add("No build request.");
                return report;
            }
            var scenes = request.scenes ?? Array.Empty<string>();
            var extras = request.additionalAssets ?? Array.Empty<string>();
            if (!WorldContentDownload.IsIdentifier(request.worldId))
                report.Errors.Add("World ID must be 1-80 letters, digits, '-' or '_'.");
            if (scenes.Length == 0) report.Errors.Add("Select at least one saved scene.");
            if (!scenes.Contains(request.entryScene, StringComparer.Ordinal))
                report.Errors.Add("Entry scene must be in the selected scene list.");
            if (scenes.Distinct(StringComparer.Ordinal).Count() != scenes.Length)
                report.Errors.Add("The scene list contains duplicates.");
            foreach (string path in scenes)
                if (string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    report.Errors.Add("Scene does not exist or is not imported: " + path);
            foreach (string path in extras)
            {
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path) ||
                    AssetDatabase.LoadMainAssetAtPath(path) == null)
                    report.Errors.Add("Additional asset must be an imported asset, not a folder: " + path);
                else if (CodeExtensions.Contains(Path.GetExtension(path)) || path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    report.Errors.Add("Put scenes in Scenes; C# code/DLLs belong in the installed player SDK: " + path);
            }
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isDirty && scenes.Contains(scene.path, StringComparer.Ordinal))
                    report.Errors.Add("Save the selected scene before inspecting/building it: " + scene.path);
            }
            if (!report.IsValid) return report;

            report.Dependencies = AssetDatabase.GetDependencies(scenes.Concat(extras).ToArray(), true)
                .Where(IsProjectAsset).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (ValidateAdditionalAssets != null)
                foreach (Action<string[], List<string>> validator in ValidateAdditionalAssets.GetInvocationList())
                {
                    try { validator(report.Dependencies, report.Errors); }
                    catch (Exception exception) { report.Errors.Add("Content validation failed: " + exception.Message); }
                }
            var required = new Dictionary<string, ScriptRequirement>(StringComparer.Ordinal);
            var bundleAssets = new List<string>();
            foreach (string path in report.Dependencies)
            {
                if (scenes.Contains(path, StringComparer.Ordinal)) continue;
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset is MonoScript script)
                {
                    // ScriptedImporter sources are importer dependencies, not player components.
                    Type scriptType = script.GetClass();
                    if (scriptType != null && (typeof(MonoBehaviour).IsAssignableFrom(scriptType) ||
                                              typeof(ScriptableObject).IsAssignableFrom(scriptType)))
                        AddRequiredType(scriptType, required);
                    continue;
                }
                if (CodeExtensions.Contains(Path.GetExtension(path))) continue;
                if (asset == null)
                {
                    report.Errors.Add("Dependency is not imported: " + path);
                    continue;
                }
                // Includes are consumed by importers; they are not player assets themselves.
                // AnimatorController/LightingDataAsset are authored by Editor types,
                // but their imported content is required by player scenes.
                bool editorOnly = asset.GetType().Assembly.GetName().Name.StartsWith("UnityEditor", StringComparison.Ordinal)
                    && !(asset is RuntimeAnimatorController) && !(asset is LightingDataAsset);
                if (asset is DefaultAsset || editorOnly)
                {
                    if (extras.Contains(path, StringComparer.Ordinal))
                        report.Errors.Add("Additional asset is not a runtime asset: " + path);
                    continue;
                }
                bundleAssets.Add(path);
                foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (subAsset is ScriptableObject) AddRequiredType(subAsset.GetType(), required);
                if (asset is GameObject prefab) InspectHierarchy(prefab, path, report, required);
            }
            foreach (string scenePath in scenes)
            {
                var previousActive = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var inspected = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
                bool openedForInspection = !inspected.IsValid() || !inspected.isLoaded;
                if (openedForInspection) inspected = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in inspected.GetRootGameObjects()) InspectHierarchy(root, scenePath, report, required);
                }
                finally
                {
                    if (openedForInspection) EditorSceneManager.CloseScene(inspected, true);
                    if (previousActive.IsValid() && previousActive.isLoaded)
                        UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousActive);
                }
            }
            report.BundleAssets = bundleAssets.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            report.RequiredTypes = required.Values.OrderBy(x => x.assembly).ThenBy(x => x.type).ToArray();
            if (request.requirePortableScripts)
            {
                foreach (var type in report.RequiredTypes.Where(type => !IsPortableType(type)))
                    report.Errors.Add(PortableTypeError(type));
                if (!IsPublishTarget(request.target)) report.Errors.Add("QR publishing requires Android or WebGL content.");
                if (scenes.Length != 1) report.Errors.Add("QR publishing currently supports one entry scene per world.");
                if (WorldContentSession.CurrentRenderPipeline != "builtin")
                    report.Errors.Add("This Android player uses the Built-in render pipeline. URP/HDRP worlds need a matching player.");
            }
            if (report.RequiredTypes.Length > 0)
                report.Warnings.Add("Referenced C# types must already exist in the World player. Bundles do not deliver their implementations.");
            report.Warnings.Add("Dynamic loads by string (Lua require, Resources.Load, etc.) must be included in Additional Assets.");
            report.Warnings.Add("Shader/render-pipeline, native plugins and mobile AOT compatibility still require target-device validation.");
            return report;
        }

        static void InspectHierarchy(GameObject root, string path, WorldValidationReport report,
            Dictionary<string, ScriptRequirement> required)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var go = transform.gameObject;
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
                    report.Errors.Add("Missing Script: " + path + " / " + go.name);
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    if (component is MonoBehaviour) AddRequiredType(component.GetType(), required);
                    if (component is IWorldContentValidatable validatable)
                    {
                        try
                        {
                            foreach (string error in validatable.ValidateContent())
                                report.Errors.Add(path + " / " + go.name + ": " + error);
                        }
                        catch (Exception exception) { report.Errors.Add(path + " / " + go.name + ": " + exception.Message); }
                    }
                    var serialized = new SerializedObject(component);
                    try
                    {
                        var property = serialized.GetIterator();
                        while (property.Next(true))
                            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                                property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                                report.Errors.Add("Missing reference: " + path + " / " + go.name + " / " + property.propertyPath);
                    }
                    finally { serialized.Dispose(); }
                }
            }
        }

        static void AddRequiredType(Type type, Dictionary<string, ScriptRequirement> required)
        {
            if (type == null) return;
            string assembly = type.Assembly.GetName().Name;
            required[assembly + ":" + type.FullName] = new ScriptRequirement { assembly = assembly, type = type.FullName };
        }

        public static void ValidatePortableTypes(ScriptRequirement[] types)
        {
            foreach (var type in types ?? Array.Empty<ScriptRequirement>())
                if (!IsPortableType(type)) throw new InvalidOperationException(PortableTypeError(type));
        }

        public static bool IsPublishTarget(BuildTarget target) => target == BuildTarget.Android || target == BuildTarget.WebGL;

        public static bool IsPortableType(ScriptRequirement type) => type != null &&
            !string.IsNullOrEmpty(type.type) && !string.IsNullOrEmpty(type.assembly) &&
            (type.assembly == "Kimchily.Creator.Runtime" || type.assembly == "Kimchily.Scripting.Runtime" ||
             type.assembly == "Kimchily.TypeScript.Runtime" ||
             type.assembly.StartsWith("UnityEngine.", StringComparison.Ordinal));

        static string PortableTypeError(ScriptRequirement type) =>
            "This C# component is not installed in the published-world player: " + type?.type +
            ". For a Mobile Player model, create a publishable model copy in its Inspector or this publishing window. " +
            "For required script behaviour, use KimchilyTypeScriptBehaviour or update the player SDK first.";

        static bool IsProjectAsset(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.StartsWith("Assets/", StringComparison.Ordinal)) return true;
            // ScriptedImporters can depend on tools/type declarations in ignored UPM
            // directories. Their hashes affect imports, but these files are not player assets.
            return path.StartsWith("Packages/", StringComparison.Ordinal) &&
                !path.Split('/').Any(part => part.EndsWith("~", StringComparison.Ordinal));
        }

        public static WorldBuildResult Build(WorldBuildRequest request)
        {
            var validation = Validate(request);
            if (!validation.IsValid) throw new InvalidOperationException(string.Join("\n", validation.Errors));
            if (string.IsNullOrWhiteSpace(request.outputRoot)) throw new ArgumentException("Select an output directory.");
            string outputRoot = Path.GetFullPath(request.outputRoot);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            foreach (string reserved in new[] { "Assets", "Packages", "Library", "ProjectSettings" })
            {
                string folder = Path.Combine(projectRoot, reserved);
                if (outputRoot.Equals(folder, StringComparison.OrdinalIgnoreCase) ||
                    outputRoot.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Build output must be outside Unity Assets/Packages/Library/ProjectSettings.");
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(request.target), request.target))
                throw new NotSupportedException("Unity build module is not installed for " + request.target);
            // Avoid mutating the user's active platform as a hidden side effect.
            if (EditorUserBuildSettings.activeBuildTarget != request.target)
                throw new InvalidOperationException("Switch Unity's active build target to " + request.target + " first.");

            string revision = (request.target == BuildTarget.WebGL ? "webgl-" : "") +
                DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string output = Path.Combine(outputRoot, revision);
            System.IO.Directory.CreateDirectory(output);
            var buildMap = new List<AssetBundleBuild>
            {
                new AssetBundleBuild { assetBundleName = "world-scenes", assetNames = request.scenes }
            };
            if (validation.BundleAssets.Length > 0)
                buildMap.Add(new AssetBundleBuild { assetBundleName = "world-assets", assetNames = validation.BundleAssets });

            try
            {
                var unityManifest = BuildPipeline.BuildAssetBundles(output, buildMap.ToArray(),
                    BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode |
                    BuildAssetBundleOptions.ForceRebuildAssetBundle, request.target);
                if (unityManifest == null) throw new InvalidOperationException("Unity AssetBundle build failed.");
                string[] names = unityManifest.GetAllAssetBundles();
                if (names.Length == 0 || !names.Contains("world-scenes"))
                    throw new InvalidOperationException("Build did not produce the scene bundle.");

                var files = new List<BundleFile>();
                foreach (string name in names)
                {
                    string filePath = Path.Combine(output, name);
                    if (!File.Exists(filePath) || !BuildPipeline.GetCRCForAssetBundle(filePath, out uint crc))
                        throw new InvalidOperationException("Missing/invalid built bundle: " + name);
                    files.Add(new BundleFile
                    {
                        name = name, fileName = name, sizeBytes = new FileInfo(filePath).Length,
                        sha256 = WorldContentSession.ComputeSha256(filePath), crc = crc,
                        unityHash = unityManifest.GetAssetBundleHash(name).ToString(),
                        dependencies = unityManifest.GetDirectDependencies(name)
                    });
                }
                var manifest = new WorldContentManifest
                {
                    worldId = request.worldId, revisionId = revision,
                    unityVersion = Application.unityVersion, platform = request.target.ToString(),
                    renderPipeline = WorldContentSession.CurrentRenderPipeline,
                    entryScene = request.entryScene, scenes = request.scenes.ToArray(),
                    bundles = files.ToArray(), requiredTypes = validation.RequiredTypes
                };
                // Publication marker is written last: incomplete directories never have world.json.
                File.WriteAllText(Path.Combine(output, "world.json"), JsonUtility.ToJson(manifest, true));
                return new WorldBuildResult { Directory = output, Manifest = manifest, Validation = validation };
            }
            catch (Exception error)
            {
                try { File.WriteAllText(Path.Combine(output, "build-failed.txt"), error.ToString()); }
                catch (IOException) { /* Preserve the actual build failure if diagnostic writing fails. */ }
                throw;
            }
        }
    }
}
