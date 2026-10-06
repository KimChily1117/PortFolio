using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

[assembly: InternalsVisibleTo("Kimchily.TypeScript.Editor.Tests")]
namespace Kimchily.TypeScript.Editor
{
    [Serializable]
    public sealed class TypeScriptCompilerResult
    {
        public int apiVersion;
        public string entryModule;
        public string className;
        public string sourceHash;
        public string compilerVersion;
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
        public const string InstallMenu = "Kimchily/TypeScript/Install or Repair Compiler";
        static readonly Dictionary<string, string> RuntimeChecks = new Dictionary<string, string>();
        public static string CompilerDirectory
        {
            get
            {
                return Path.Combine(PackageRoot, "Tools~/Compiler");
            }
        }

        public static string ProjectRoot
        {
            get
            {
                return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            }
        }

        public static string PackageRoot
        {
            get
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(PackagePath);
                if (package == null)
                {
                    throw new InvalidOperationException("The com.kimchily.typescript local package is not installed.");
                }

                return Path.GetFullPath(package.resolvedPath);
            }
        }

        public static TypeScriptCompilerResult Compile(string assetPath)
        {
            string output = Path.Combine(ProjectRoot, "Library/KimchilyTypeScript", Guid.NewGuid().ToString("N") + ".json");
            try
            {
                if (TypeScriptCompilerSetup.IsInstalling)
                {
                    throw new InvalidOperationException("TypeScript compiler setup is in progress. Wait for the Kimchily TypeScript background task, then build again.");
                }

                string directory = CompilerDirectory;
                using (AcquireCompilerLock(directory))
                {
                    string compiler = Path.Combine(directory, "compile.cjs");
                    if (!TryGetToolchain(directory, out string node, out string reason))
                    {
                        throw new InvalidOperationException(TypeScriptCompilerSetup.UnavailableMessage(reason));
                    }

                    var start = new ProcessStartInfo(node,
                        Quote(compiler) + " --project-root " + Quote(ProjectRoot) +
                        " --entry " + Quote(assetPath) + " --output " + Quote(output))
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = ProjectRoot
                    };
                    var error = new StringBuilder();
                    using (var process = new Process
                    {
                        StartInfo = start
                    }

                    )
                    {
                        process.OutputDataReceived += (_, __) =>
                        {
                        };
                        process.ErrorDataReceived += (_, e) =>
                        {
                            if (e.Data != null)
                            {
                                lock (error)
                                {
                                    if (error.Length < 8192)
                                    {
                                        error.AppendLine(e.Data);
                                    }
                                }
                            }
                        };
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        if (!process.WaitForExit(30000))
                        {
                            try
                            {
                                process.Kill();
                            }
                            catch (InvalidOperationException)
                            {
                            }

                            throw new TimeoutException("TypeScript compiler exceeded the 30 second timeout.");
                        }

                        process.WaitForExit(); // Drain asynchronous stderr callbacks after termination.
                        if (!File.Exists(output))
                        {
                            throw new InvalidOperationException("TypeScript compiler failed: " + error.ToString().Trim());
                        }

                        var result = JsonUtility.FromJson<TypeScriptCompilerResult>(File.ReadAllText(output));
                        if (result == null)
                        {
                            throw new InvalidOperationException("TypeScript compiler returned invalid JSON.");
                        }

                        if (process.ExitCode != 0)
                        {
                            result.compiledSuccessfully = false;
                        }

                        if (!result.compiledSuccessfully)
                        {
                            result.modules = Array.Empty<TypeScriptModule>();
                            result.fields = Array.Empty<TypeScriptField>();
                        }

                        return result;
                    }
                }
            }
            catch (Exception exception)
            {
                return new TypeScriptCompilerResult
                {
                    diagnostics = new[]
                    {
                        assetPath + ": " + exception.Message
                    }
                };
            }
            finally
            {
                if (File.Exists(output))
                {
                    File.Delete(output);
                }
            }
        }

        public static string[] ToolFiles()
        {
            return new[]
            {
                Path.Combine(PackageRoot, "Tools~/Compiler/compile.cjs"),
                Path.Combine(PackageRoot, "Tools~/Compiler/package.json"),
                Path.Combine(PackageRoot, "Tools~/Compiler/package-lock.json"),
                Path.Combine(PackageRoot, "Tools~/Compiler/install.ps1"),
                // A small, atomically-written marker invalidates failed first imports
                // without reading the multi-megabyte compiler on every editor poll.
                Path.Combine(PackageRoot, "Tools~/Compiler/.tools/ready.json"),
                Path.Combine(PackageRoot, "Typings~/kimchily.d.ts")
            };
        }

        internal static FileStream AcquireCompilerLock(string directory)
        {
            string tools = Path.Combine(directory, ".tools");
            Directory.CreateDirectory(tools);
            try
            {
                // Install takes FileShare.None on the same file. Keep this shared
                // handle until Node exits so node_modules cannot be swapped mid-compile.
                return new FileStream(Path.Combine(tools, "install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            }
            catch (IOException exception)
            {
                throw new InvalidOperationException("TypeScript compiler setup is running in another Editor or terminal. Wait for it to finish, then retry the build.", exception);
            }
        }

        internal static bool TryGetToolchain(string directory, out string node, out string reason)
        {
            node = null;
            if (!File.Exists(Path.Combine(directory, "node_modules/typescript/lib/typescript.js")))
            {
                reason = "TypeScript compiler is not installed.";
                return false;
            }

            string configured = Environment.GetEnvironmentVariable("KIMCHILY_NODE_PATH");
            node = ResolveNode(directory, configured, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetEnvironmentVariable("PATH"));
            string executable = FindExecutable(node, Environment.GetEnvironmentVariable("PATH"));
            if (executable == null)
            {
                reason = string.IsNullOrWhiteSpace(configured)
                    ? "A Node.js runtime is not installed."
                    : "KIMCHILY_NODE_PATH does not point to an available Node.js executable: " + configured +
                        ". Correct or remove this environment variable and restart Unity.";
                return false;
            }

            node = executable;
            reason = null;
            return true;
        }

        internal static string ResolveNode(string directory, string configured, string programFiles, string searchPath)
        {
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            string bundled = ReadBundledNode(directory);
            if (bundled != null)
            {
                return bundled;
            }

            string installed = Path.Combine(programFiles ?? "", "nodejs/node.exe");
            if (File.Exists(installed))
            {
                return Path.GetFullPath(installed);
            }

            return FindExecutable("node", searchPath) ?? "node";
        }

        [Serializable]
        internal sealed class ReadyMarker
        {
            public int schemaVersion;
            public string nodeRelativePath;
            public string nodeVersion;
            public string typescriptVersion;
        }

        internal static string ReadBundledNode(string directory)
        {
            string tools = Path.GetFullPath(Path.Combine(directory, ".tools"));
            string marker = Path.Combine(tools, "ready.json");
            if (!File.Exists(marker))
            {
                return null;
            }

            try
            {
                var ready = JsonUtility.FromJson<ReadyMarker>(File.ReadAllText(marker));
                if (ready == null || ready.schemaVersion != 1 ||
                    string.IsNullOrWhiteSpace(ready.nodeRelativePath) ||
                    ready.nodeVersion != "v24.21.0" || ready.typescriptVersion != "5.9.3" ||
                    Path.IsPathRooted(ready.nodeRelativePath))
                {
                    return null;
                }

                string node = Path.GetFullPath(Path.Combine(tools, ready.nodeRelativePath));
                return node.StartsWith(tools + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(node) ? node : null;
            }
            catch (Exception exception)when (exception is IOException || exception is ArgumentException || exception is UnauthorizedAccessException)
            {
                return null; // Incomplete/corrupt setup is repaired through the installer.
            }
        }

        // Called on a background task by setup; never invokes Unity APIs.
        internal static string CheckNodeRuntime(string executable)
        {
            try
            {
                string key = Path.GetFullPath(executable) + "|" + File.GetLastWriteTimeUtc(executable).Ticks;
                lock (RuntimeChecks)
                {
                    if (RuntimeChecks.TryGetValue(key, out string cached))
                    {
                        return cached;
                    }
                }

                string error;
                using (var process = new Process
                {
                    StartInfo = new ProcessStartInfo(executable, "--version")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                }

                )
                {
                    process.Start();
                    var output = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(3000))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (InvalidOperationException)
                        {
                        }

                        return "Node.js version check timed out: " + executable;
                    }

                    error = process.ExitCode == 0 && IsSupportedNodeVersion(output.GetAwaiter().GetResult())
                        ? null
                        : "Node.js 18 or newer is required. Runtime: " + executable +
                            ". Reported version: " + output.GetAwaiter().GetResult().Trim() +
                            ". " + stderr.GetAwaiter().GetResult().Trim();
                }

                lock (RuntimeChecks)
                {
                    RuntimeChecks[key] = error;
                }

                return error;
            }
            catch (Exception exception)
            {
                return "Node.js runtime could not start: " + executable + ". " + exception.Message;
            }
        }

        internal static bool IsSupportedNodeVersion(string version)
        {
            string value = (version ?? "").Trim();
            return value.StartsWith("v", StringComparison.Ordinal) && Version.TryParse(value.Substring(1), out Version parsed) && parsed.Major >= 18;
        }

        internal static string FindExecutable(string executable, string searchPath)
        {
            if (string.IsNullOrWhiteSpace(executable))
            {
                return null;
            }

            if (File.Exists(executable))
            {
                return executable;
            }

            if (Path.IsPathRooted(executable) || executable.IndexOfAny(new[] { '/', '\\' }) >= 0)
            {
                return null;
            }

            foreach (string entry in (searchPath ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                try
                {
                    string candidate = Path.Combine(entry.Trim().Trim('"'), executable);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }

                    if (Application.platform == RuntimePlatform.WindowsEditor &&
                        !candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        File.Exists(candidate + ".exe"))
                    {
                        return candidate + ".exe";
                    }
                }
                catch (ArgumentException)
                {
                }
            }

            return null;
        }

        public static string ToAssetPath(string physical)
        {
            string full = Path.GetFullPath(physical).Replace('\\', '/');
            string project = ProjectRoot.Replace('\\', '/') + "/";
            string package = PackageRoot.Replace('\\', '/') + "/";
            if (full.StartsWith(package, StringComparison.OrdinalIgnoreCase))
            {
                return PackagePath + "/" + full.Substring(package.Length);
            }

            return full.StartsWith(project, StringComparison.OrdinalIgnoreCase) ? full.Substring(project.Length) : null;
        }

        // Windows CreateProcess quoting; no command shell or profile is involved.
        internal static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\')
                {
                    slashes++;
                    continue;
                }

                if (c == '"')
                {
                    result.Append('\\', slashes * 2 + 1);
                    result.Append(c);
                    slashes = 0;
                    continue;
                }

                result.Append('\\', slashes);
                slashes = 0;
                result.Append(c);
            }

            result.Append('\\', slashes * 2);
            return result.Append('"').ToString();
        }
    }
}
