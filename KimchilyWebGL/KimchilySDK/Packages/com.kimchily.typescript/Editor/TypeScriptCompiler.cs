using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Kimchily.TypeScript.Editor
{
    [Serializable]
    public sealed class TypeScriptCompilerResult
    {
        public int apiVersion;
        public string entryModule, className, sourceHash, compilerVersion;
        public bool compiledSuccessfully;
        public string[] diagnostics = Array.Empty<string>();
        public string[] warnings = Array.Empty<string>();
        public string[] dependencies = Array.Empty<string>();
        public TypeScriptModule[] modules = Array.Empty<TypeScriptModule>();
        public TypeScriptField[] fields = Array.Empty<TypeScriptField>();
    }

    public static class TypeScriptCompiler
    {
        public const string PackagePath = "Packages/com.kimchily.typescript";
        public const string DependencyName = "Kimchily.TypeScript.CompilerAndTypings";
        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string PackageRoot
        {
            get
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(PackagePath);
                if (package == null) throw new InvalidOperationException("The com.kimchily.typescript local package is not installed.");
                return Path.GetFullPath(package.resolvedPath);
            }
        }

        public static TypeScriptCompilerResult Compile(string assetPath)
        {
            string output = Path.Combine(ProjectRoot, "Library/KimchilyTypeScript", Guid.NewGuid().ToString("N") + ".json");
            try
            {
                string compiler = Path.Combine(PackageRoot, "Tools~/Compiler/compile.cjs");
                if (!File.Exists(Path.Combine(PackageRoot, "Tools~/Compiler/node_modules/typescript/lib/typescript.js")))
                    throw new InvalidOperationException("TypeScript compiler is not installed. Run npm ci --ignore-scripts in " + Path.GetDirectoryName(compiler));
                string node = Environment.GetEnvironmentVariable("KIMCHILY_NODE_PATH");
                if (string.IsNullOrWhiteSpace(node))
                {
                    string installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs/node.exe");
                    node = File.Exists(installed) ? installed : "node";
                }
                var start = new ProcessStartInfo(node,
                    Quote(compiler) + " --project-root " + Quote(ProjectRoot) + " --entry " + Quote(assetPath) + " --output " + Quote(output))
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    WorkingDirectory = ProjectRoot
                };
                var error = new StringBuilder();
                using (var process = new Process { StartInfo = start })
                {
                    process.OutputDataReceived += (_, __) => { };
                    process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (error) { if (error.Length < 8192) error.AppendLine(e.Data); } };
                    process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                    if (!process.WaitForExit(30000))
                    {
                        try { process.Kill(); } catch (InvalidOperationException) { }
                        throw new TimeoutException("TypeScript compiler exceeded the 30 second timeout.");
                    }
                    process.WaitForExit(); // Drain asynchronous stderr callbacks after termination.
                    if (!File.Exists(output)) throw new InvalidOperationException("TypeScript compiler failed: " + error.ToString().Trim());
                    var result = JsonUtility.FromJson<TypeScriptCompilerResult>(File.ReadAllText(output));
                    if (result == null) throw new InvalidOperationException("TypeScript compiler returned invalid JSON.");
                    if (process.ExitCode != 0) result.compiledSuccessfully = false;
                    if (!result.compiledSuccessfully) { result.modules = Array.Empty<TypeScriptModule>(); result.fields = Array.Empty<TypeScriptField>(); }
                    return result;
                }
            }
            catch (Exception exception)
            {
                return new TypeScriptCompilerResult { diagnostics = new[] { assetPath + ": " + exception.Message } };
            }
            finally { if (File.Exists(output)) File.Delete(output); }
        }

        public static string[] ToolFiles() => new[]
        {
            Path.Combine(PackageRoot, "Tools~/Compiler/compile.cjs"),
            Path.Combine(PackageRoot, "Tools~/Compiler/package.json"),
            Path.Combine(PackageRoot, "Tools~/Compiler/package-lock.json"),
            Path.Combine(PackageRoot, "Typings~/kimchily.d.ts")
        };

        public static string ToAssetPath(string physical)
        {
            string full = Path.GetFullPath(physical).Replace('\\', '/');
            string project = ProjectRoot.Replace('\\', '/') + "/";
            string package = PackageRoot.Replace('\\', '/') + "/";
            if (full.StartsWith(package, StringComparison.OrdinalIgnoreCase)) return PackagePath + "/" + full.Substring(package.Length);
            return full.StartsWith(project, StringComparison.OrdinalIgnoreCase) ? full.Substring(project.Length) : null;
        }

        // Windows CreateProcess quoting; no command shell or profile is involved.
        static string Quote(string value)
        {
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { result.Append('\\', slashes * 2 + 1); result.Append(c); slashes = 0; continue; }
                result.Append('\\', slashes); slashes = 0; result.Append(c);
            }
            result.Append('\\', slashes * 2); return result.Append('"').ToString();
        }
    }
}
