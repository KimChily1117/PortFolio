using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Kimchily.TypeScript.Editor
{
    [ScriptedImporter(1, "ts")]
    public sealed class TypeScriptImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            var result = TypeScriptCompiler.Compile(context.assetPath);
            var asset = ScriptableObject.CreateInstance<TypeScriptAsset>();
            asset.name = Path.GetFileNameWithoutExtension(context.assetPath);
            asset.apiVersion = result.apiVersion;
            asset.entryModule = result.entryModule;
            asset.className = result.className;
            asset.sourceHash = result.sourceHash;
            asset.compilerVersion = result.compilerVersion;
            asset.compiledSuccessfully = result.compiledSuccessfully;
            asset.diagnostics = (result.diagnostics ?? Array.Empty<string>()).Concat(result.warnings ?? Array.Empty<string>()).ToArray();
            asset.modules = result.compiledSuccessfully ? result.modules : Array.Empty<TypeScriptModule>();
            asset.fields = result.compiledSuccessfully ? result.fields : Array.Empty<TypeScriptField>();
            context.AddObjectToAsset("script", asset); context.SetMainObject(asset);
            context.DependsOnCustomDependency(TypeScriptCompiler.DependencyName);
            foreach (string dependency in (result.dependencies ?? Array.Empty<string>()).Concat(TypeScriptCompiler.ToolFiles()).Distinct())
            {
                string path = TypeScriptCompiler.ToAssetPath(dependency);
                if (!string.IsNullOrEmpty(path) && path != context.assetPath) context.DependsOnSourceAsset(path);
            }
            if (!result.compiledSuccessfully)
                foreach (string diagnostic in result.diagnostics ?? Array.Empty<string>()) context.LogImportError(diagnostic);
            // Successful helper modules intentionally have an empty className. They
            // may be imported by a behaviour, but cannot be attached as one.
        }

        [MenuItem("Assets/Create/Kimchily/TypeScript Script", priority = 80)]
        public static void CreateScript()
        {
            const string template = "import { KimchilyScriptBehaviour } from 'Kimchily.Script';\n" +
                "import { Time, Vector3 } from 'UnityEngine';\n\n" +
                "export default class WorldBehaviour extends KimchilyScriptBehaviour {\n" +
                "    public speed: number = 45;\n\n    Update(): void {\n" +
                "        this.transform.Rotate(0, this.speed * Time.deltaTime, 0);\n    }\n}\n";
            ProjectWindowUtil.CreateAssetWithContent("WorldBehaviour.ts", template);
        }

        [MenuItem("Kimchily/TypeScript/Configure Type Completion")]
        public static void EnsureProjectConfiguration()
        {
            string file = Path.Combine(TypeScriptCompiler.ProjectRoot, "tsconfig.json");
            if (File.Exists(file)) { Debug.Log("Keeping existing tsconfig.json: " + file); return; }
            string relative = new Uri(TypeScriptCompiler.ProjectRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar)
                .MakeRelativeUri(new Uri(Path.Combine(TypeScriptCompiler.PackageRoot, "Typings~/kimchily.d.ts"))).ToString();
            File.WriteAllText(file, "{\n  \"compilerOptions\": { \"target\": \"ES2018\", \"module\": \"CommonJS\", \"strict\": true, \"noEmit\": true, \"lib\": [\"ES2018\"], \"types\": [] },\n  \"include\": [\"Assets/**/*.ts\", \"" + relative + "\"]\n}\n");
            Debug.Log("Created TypeScript completion configuration: " + file);
        }
    }
}
