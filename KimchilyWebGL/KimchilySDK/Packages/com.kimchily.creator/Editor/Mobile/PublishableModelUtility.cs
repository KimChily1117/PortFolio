using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kimchily.Creator.Content;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Kimchily.Creator.Editor
{
    /// <summary>Creates an independent visual prefab without changing the imported model or source prefab.</summary>
    public static class PublishableModelUtility
    {
        const string MissingScript = "Missing Script";

        public static string[] GetUnsupportedScripts(GameObject source)
        {
            ValidateSource(source);
            return source.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(component => component == null || !IsPortable(component.GetType()))
                .Select(component => component == null ? MissingScript : component.GetType().FullName)
                .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        public static GameObject CreateCopy(GameObject source, string outputPath, out string[] removedTypes)
        {
            ValidateSource(source);
            outputPath = ValidateOutputPath(outputPath);
            removedTypes = GetUnsupportedScripts(source);
            Scene preview = default;
            bool ownsOutput = false;
            try
            {
                preview = EditorSceneManager.NewPreviewScene();
                var holder = new GameObject("Kimchily Model Preparation") { hideFlags = HideFlags.HideAndDontSave };
                holder.SetActive(false);
                SceneManager.MoveGameObjectToScene(holder, preview);
                // Instantiate beneath an inactive parent from the start: imported ExecuteAlways
                // behaviours must not be enabled merely to prepare their visual hierarchy.
                GameObject copy = Object.Instantiate(source, holder.transform, false);
                copy.name = source.name;
                copy.hideFlags = HideFlags.None;
                UnpackCompletely(copy);
                RemoveUnsupportedComponents(copy);
                foreach (Animator animator in copy.GetComponentsInChildren<Animator>(true))
                    animator.applyRootMotion = false; // The outer CharacterController owns movement.

                // Check again immediately before creation; existing user assets are never overwritten.
                ValidateOutputPath(outputPath);
                ownsOutput = true;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(copy, outputPath, out bool success);
                if (!success || prefab == null) throw new InvalidOperationException("Could not save the publishable model copy.");
                ValidateDependencies(outputPath);
                return prefab;
            }
            catch
            {
                if (ownsOutput) DeleteGeneratedAsset(outputPath);
                throw;
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        static void ValidateSource(GameObject source)
        {
            if (source == null || !EditorUtility.IsPersistent(source) || !AssetDatabase.Contains(source))
                throw new ArgumentException("Select a model or prefab asset from the Project window.", nameof(source));
            string path = AssetDatabase.GetAssetPath(source);
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) &&
                PrefabUtility.GetPrefabAssetType(source) != PrefabAssetType.Model)
                throw new ArgumentException("The source must be an imported model or a prefab asset.", nameof(source));
        }

        static string ValidateOutputPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Choose a new .prefab path beneath Assets.");
            string path = value.Replace('\\', '/');
            string[] parts = path.Split('/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                parts.Any(part => string.IsNullOrEmpty(part) || part == "." || part == ".." ||
                    part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new ArgumentException("Choose a new .prefab path beneath Assets, without relative traversal.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.GetFullPath(Path.Combine(project, path));
            string assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(assets, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The model copy must stay beneath this project's Assets directory.");
            string folder = path.Substring(0, path.LastIndexOf('/'));
            if (!AssetDatabase.IsValidFolder(folder)) throw new DirectoryNotFoundException("Create the destination Assets folder first: " + folder);
            // Refuse links/junctions that redirect an apparently local Assets path elsewhere.
            for (var directory = new DirectoryInfo(Path.GetDirectoryName(full)); directory != null &&
                directory.FullName.StartsWith(assets.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
                directory = directory.Parent)
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("The destination cannot use a linked Assets directory.");
            if (File.Exists(full) || File.Exists(full + ".meta") || Directory.Exists(full) ||
                AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new IOException("A model copy already exists at this path. Choose a new filename: " + path);
            return path;
        }

        static bool IsPortable(Type type) => WorldContentBuilder.IsPortableType(new ScriptRequirement
        {
            assembly = type.Assembly.GetName().Name,
            type = type.FullName
        });

        static void UnpackCompletely(GameObject copy)
        {
            // Object.Instantiate normally drops the root connection. Nested instances may still
            // carry links; unpack each outermost remaining instance without touching source assets.
            foreach (Transform child in copy.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(child.gameObject)) continue;
                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(child.gameObject);
                if (root != null && (root == copy || root.transform.IsChildOf(copy.transform)))
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
        }

        static void RemoveUnsupportedComponents(GameObject copy)
        {
            foreach (Transform transform in copy.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            var remaining = copy.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(component => component != null && !IsPortable(component.GetType())).Cast<Component>().ToList();
            var order = new List<Component>();
            while (remaining.Count > 0)
            {
                Component removable = remaining.FirstOrDefault(candidate => !candidate.gameObject.GetComponents<Component>()
                    .Any(dependent => dependent != null && dependent != candidate && !order.Contains(dependent) && Requires(dependent.GetType(), candidate.GetType())));
                if (removable == null)
                    throw new InvalidOperationException("Cannot remove these model scripts without breaking a retained component's RequireComponent dependency or a dependency cycle: " +
                        string.Join(", ", remaining.Select(component => component.GetType().FullName).Distinct()));
                order.Add(removable);
                remaining.Remove(removable);
            }
            foreach (Component component in order) Object.DestroyImmediate(component);
        }

        static bool Requires(Type dependent, Type required)
        {
            foreach (RequireComponent attribute in dependent.GetCustomAttributes(typeof(RequireComponent), true))
                foreach (Type type in new[] { attribute.m_Type0, attribute.m_Type1, attribute.m_Type2 })
                    if (type != null && type.IsAssignableFrom(required)) return true;
            return false;
        }

        static void ValidateDependencies(string outputPath)
        {
            var failures = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in AssetDatabase.GetDependencies(outputPath, true))
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is MonoScript script)
                    {
                        Type type = script.GetClass();
                        if (type != null && (typeof(MonoBehaviour).IsAssignableFrom(type) || typeof(ScriptableObject).IsAssignableFrom(type)) && !IsPortable(type))
                            failures.Add(type.FullName + " (" + path + ")");
                    }
                    else if (asset is ScriptableObject && !IsPortable(asset.GetType()))
                        failures.Add(asset.GetType().FullName + " (" + path + ")");
                    else if (asset is GameObject prefab)
                        foreach (MonoBehaviour component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                            if (component == null || !IsPortable(component.GetType()))
                                failures.Add((component == null ? MissingScript : component.GetType().FullName) + " (" + path + ")");
                }
            }
            if (failures.Count > 0)
                throw new InvalidOperationException("The model still references C# code that is not installed in the app, outside removable prefab components. " +
                    "Use a script-free Animator/controller or update the player SDK; the generated copy was not kept.\n" +
                    string.Join("\n", failures.OrderBy(value => value, StringComparer.Ordinal)));
        }

        static void DeleteGeneratedAsset(string path)
        {
            if (AssetDatabase.DeleteAsset(path)) return;
            // SaveAsPrefabAsset can fail before import. These paths were confirmed absent before creation.
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            if (File.Exists(full)) File.Delete(full);
            if (File.Exists(full + ".meta")) File.Delete(full + ".meta");
        }
    }
}
