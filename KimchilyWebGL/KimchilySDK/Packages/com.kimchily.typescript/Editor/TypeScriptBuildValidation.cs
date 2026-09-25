using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Kimchily.Creator.Editor;
using UnityEditor;
using UnityEngine;

namespace Kimchily.TypeScript.Editor
{
    [InitializeOnLoad]
    public static class TypeScriptBuildValidation
    {
        static double nextCheck;
        static string lastToolHash;

        static TypeScriptBuildValidation()
        {
            WorldContentBuilder.ValidateAdditionalAssets += ValidateAssets;
            EditorApplication.delayCall += UpdateToolDependency;
            EditorApplication.update += PollToolDependency;
        }

        static void PollToolDependency()
        {
            if (EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextCheck = EditorApplication.timeSinceStartup + 2;
            UpdateToolDependency();
        }

        static void UpdateToolDependency()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            try
            {
                using (var sha = SHA256.Create())
                using (var bytes = new MemoryStream())
                {
                    foreach (string file in TypeScriptCompiler.ToolFiles())
                    {
                        byte[] data = File.Exists(file) ? File.ReadAllBytes(file) : Encoding.UTF8.GetBytes("missing:" + file);
                        bytes.Write(data, 0, data.Length);
                    }
                    string hash = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "");
                    if (hash == lastToolHash) return;
                    lastToolHash = hash;
                    AssetDatabase.RegisterCustomDependency(TypeScriptCompiler.DependencyName, Hash128.Compute(hash));
                }
            }
            catch (Exception exception) { Debug.LogWarning("Kimchily TypeScript dependency check: " + exception.Message); }
        }

        /// <summary>Fresh type checking for every referenced TS asset, including helpers.</summary>
        public static void ValidateAssets(IEnumerable<string> paths, ICollection<string> errors)
        {
            foreach (string path in paths.Where(p => p.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)).Distinct())
            {
                var result = TypeScriptCompiler.Compile(path);
                var asset = AssetDatabase.LoadAssetAtPath<TypeScriptAsset>(path);
                if (asset == null || asset.sourceHash != result.sourceHash || asset.compiledSuccessfully != result.compiledSuccessfully)
                {
                    // A source edited outside Unity cannot publish an older successful import.
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    asset = AssetDatabase.LoadAssetAtPath<TypeScriptAsset>(path);
                }
                if (!result.compiledSuccessfully)
                {
                    foreach (string diagnostic in result.diagnostics ?? Array.Empty<string>()) errors.Add(diagnostic);
                    if (result.diagnostics == null || result.diagnostics.Length == 0) errors.Add("TypeScript compilation failed: " + path);
                }
                else if (asset == null || !asset.compiledSuccessfully || asset.sourceHash != result.sourceHash || asset.apiVersion != 1)
                    errors.Add("TypeScript imported asset is missing or stale. Reimport it before building: " + path);
            }
        }
    }
}
