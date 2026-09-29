using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Kimchily.TypeScript.Editor
{
    /// <summary>Only the main editor owns first-use setup; import workers never install tools.</summary>
    [InitializeOnLoad]
    public static class TypeScriptCompilerSetup
    {
        const double TimeoutSeconds = 600;
        static readonly StringBuilder Output = new StringBuilder();
        static Process installer;
        static double startedAt;
        static int progressId = -1;
        static bool reloadLocked;
        static string latestOutput;
        static string displayedOutput;
        static Task<string> runtimeCheck;
        static bool installAfterRuntimeCheck;
        static bool checkingOverride;

        public static bool IsInstalling => installer != null;
        static bool IsBusy => IsInstalling || runtimeCheck != null;
        static string SessionKey => "Kimchily.TypeScript.Setup." + Hash128.Compute(TypeScriptCompiler.CompilerDirectory);

        static TypeScriptCompilerSetup()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            EditorApplication.delayCall += TryAutomaticSetup;
            EditorApplication.update += PollInstallation;
            EditorApplication.quitting += StopForShutdown;
            AssemblyReloadEvents.beforeAssemblyReload += StopForShutdown;
        }

        static void TryAutomaticSetup()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess() || IsBusy) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutomaticSetup;
                return;
            }
            try
            {
                // Retain failures across domain reloads; only a user menu action retries.
                if (SessionState.GetBool(SessionKey, false)) return;
                PrepareInstallation(false);
            }
            catch (Exception exception) { Debug.LogError("Kimchily TypeScript setup: " + exception.Message); }
        }

        [MenuItem(TypeScriptCompiler.InstallMenu)]
        public static void InstallOrRepair()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess() || IsBusy) return;
            PrepareInstallation(true);
        }

        [MenuItem(TypeScriptCompiler.InstallMenu, true)]
        static bool CanInstallOrRepair() => !AssetDatabase.IsAssetImportWorkerProcess() && !IsBusy;

        static void PrepareInstallation(bool repair)
        {
            string directory = TypeScriptCompiler.CompilerDirectory;
            bool available = TypeScriptCompiler.TryGetToolchain(directory, out string node, out _);
            string configured = Environment.GetEnvironmentVariable("KIMCHILY_NODE_PATH");
            checkingOverride = !string.IsNullOrWhiteSpace(configured);
            if (checkingOverride)
            {
                node = TypeScriptCompiler.FindExecutable(configured, Environment.GetEnvironmentVariable("PATH"));
                if (node == null)
                {
                    Fail("KIMCHILY_NODE_PATH points to an unavailable executable. Correct or remove it and restart Unity: " + configured);
                    return;
                }
            }
            if (available || checkingOverride)
            {
                installAfterRuntimeCheck = repair || !available;
                runtimeCheck = Task.Run(() => TypeScriptCompiler.CheckNodeRuntime(node));
                return;
            }
            StartInstallation();
        }

        static void StartInstallation()
        {
            string directory = TypeScriptCompiler.CompilerDirectory;
            SessionState.SetBool(SessionKey, true);
            SessionState.EraseString(SessionKey + ".Error");
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                Fail("Automatic compiler installation currently supports Windows Editor. Install Node.js and run npm ci --ignore-scripts in " + directory + ".");
                return;
            }
            string configured = Environment.GetEnvironmentVariable("KIMCHILY_NODE_PATH");
            if (!string.IsNullOrWhiteSpace(configured) && TypeScriptCompiler.FindExecutable(configured, Environment.GetEnvironmentVariable("PATH")) == null)
            {
                Fail("KIMCHILY_NODE_PATH points to an unavailable executable. Correct or remove it and restart Unity: " + configured);
                return;
            }
            string script = Path.Combine(directory, "install.ps1");
            if (!File.Exists(script))
            {
                Fail("The package installer is missing: " + script + ". Restore or update com.kimchily.typescript.");
                return;
            }
            try
            {
                lock (Output) Output.Clear();
                latestOutput = "Downloading the package-local Node.js runtime and TypeScript compiler...";
                displayedOutput = latestOutput;
                // Use Windows PowerShell by its OS path; no shell/profile/startup scripts.
                string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell/v1.0/powershell.exe");
                installer = new Process
                {
                    StartInfo = new ProcessStartInfo(powershell,
                        "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + TypeScriptCompiler.Quote(script) +
                        " -CompilerDirectory " + TypeScriptCompiler.Quote(directory))
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        WorkingDirectory = directory,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                installer.OutputDataReceived += CaptureOutput;
                installer.ErrorDataReceived += CaptureOutput;
                // Keep asynchronous pipe callbacks alive until completion while the
                // Editor stays responsive. Script changes reload after setup finishes.
                EditorApplication.LockReloadAssemblies();
                reloadLocked = true;
                installer.Start();
                startedAt = EditorApplication.timeSinceStartup;
                installer.BeginOutputReadLine();
                installer.BeginErrorReadLine();
                progressId = Progress.Start("Kimchily TypeScript", latestOutput, Progress.Options.Indefinite);
                Debug.Log("Kimchily TypeScript: preparing the package compiler automatically. Follow its background progress; build again after setup completes.");
            }
            catch (Exception exception)
            {
                StopProcess();
                Fail(exception.Message);
            }
        }

        static void CaptureOutput(object sender, DataReceivedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.Data)) return;
            lock (Output)
            {
                // Bounded diagnostic tail; never call Unity APIs on a process thread.
                Output.AppendLine(args.Data);
                if (Output.Length > 12000) Output.Remove(0, Output.Length - 12000);
                latestOutput = args.Data;
            }
        }

        static void PollInstallation()
        {
            if (runtimeCheck != null)
            {
                if (!runtimeCheck.IsCompleted) return;
                string runtimeError = runtimeCheck.GetAwaiter().GetResult();
                runtimeCheck = null;
                if (runtimeError != null && checkingOverride)
                {
                    Fail("KIMCHILY_NODE_PATH: " + runtimeError + " Correct or remove the override and restart Unity.");
                    return;
                }
                if (installAfterRuntimeCheck || runtimeError != null) StartInstallation();
            }
            if (installer == null) return;
            try
            {
                if (!installer.HasExited)
                {
                    if (EditorApplication.timeSinceStartup - startedAt >= TimeoutSeconds)
                    {
                        StopProcess();
                        Fail("Installation exceeded the 10 minute timeout. Check your network/proxy and package folder write permissions.");
                        return;
                    }
                    string description;
                    lock (Output) description = latestOutput;
                    if (progressId >= 0 && description != displayedOutput)
                    {
                        Progress.Report(progressId, -1f, description);
                        displayedOutput = description;
                    }
                    return;
                }
                installer.WaitForExit(); // HasExited is true; drain asynchronous output.
                int exitCode = installer.ExitCode;
                DisposeProcess();
                if (exitCode != 0)
                {
                    string output;
                    lock (Output) output = Output.ToString().Trim();
                    Fail("Installer exited with code " + exitCode + ". " + output);
                    return;
                }
                string directory = TypeScriptCompiler.CompilerDirectory;
                bool usable = TypeScriptCompiler.TryGetToolchain(directory, out _, out string reason);
                if (TypeScriptCompiler.ReadBundledNode(directory) == null || !usable)
                {
                    Fail("Installation finished without a usable runtime/compiler. " + reason);
                    return;
                }
                FinishProgress(Progress.Status.Succeeded);
                SessionState.EraseString(SessionKey + ".Error");
                Debug.Log("Kimchily TypeScript compiler is ready. Reimporting TypeScript assets; you can retry the build when importing finishes.");
                EditorApplication.delayCall += ReimportScripts;
            }
            catch (Exception exception)
            {
                StopProcess();
                Fail(exception.Message);
            }
        }

        static void ReimportScripts()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ReimportScripts;
                return;
            }
            TypeScriptBuildValidation.UpdateToolDependency();
            foreach (string path in AssetDatabase.GetAllAssetPaths().Where(path => path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        static void Fail(string message)
        {
            DisposeProcess();
            FinishProgress(Progress.Status.Failed);
            SessionState.SetBool(SessionKey, true);
            SessionState.SetString(SessionKey + ".Error", message);
            Debug.LogError("Kimchily TypeScript setup failed: " + message + "\nRetry with " + TypeScriptCompiler.InstallMenu +
                ". You can also run Tools~/Compiler/install.ps1 from the package in PowerShell. Automatic retries are paused for this Editor session.");
        }

        internal static string UnavailableMessage(string reason)
        {
            string instruction = Application.platform == RuntimePlatform.WindowsEditor
                ? "Unity prepares the compiler automatically on first use. Wait for the Kimchily TypeScript background task, then retry. To retry a failed setup, use " + TypeScriptCompiler.InstallMenu + "."
                : "Automatic setup supports Windows Editor. Install Node.js and run npm ci --ignore-scripts in " + TypeScriptCompiler.CompilerDirectory + ".";
            string failure = AssetDatabase.IsAssetImportWorkerProcess() ? null : SessionState.GetString(SessionKey + ".Error", "");
            return reason + " " + instruction + (string.IsNullOrWhiteSpace(failure) ? "" : " Last setup error: " + failure);
        }

        static void FinishProgress(Progress.Status status)
        {
            if (progressId < 0) return;
            Progress.Finish(progressId, status);
            progressId = -1;
        }

        static void StopForShutdown()
        {
            runtimeCheck = null;
            if (installer == null) return;
            StopProcess();
            FinishProgress(Progress.Status.Failed);
        }

        static void StopProcess()
        {
            if (installer == null) return;
            try
            {
                if (!installer.HasExited)
                {
                    // Windows PowerShell 5.1 has no Process.Kill(entireProcessTree).
                    // Stop only the installer we started, including its npm child.
                    string taskkill = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "taskkill.exe");
                    using (var kill = Process.Start(new ProcessStartInfo(taskkill, "/PID " + installer.Id + " /T /F")
                    { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
                        kill?.WaitForExit(3000);
                    if (!installer.HasExited) installer.Kill();
                }
            }
            catch (Exception exception) { Debug.LogWarning("Kimchily TypeScript installer shutdown: " + exception.Message); }
            finally { DisposeProcess(); }
        }

        static void DisposeProcess()
        {
            installer?.Dispose();
            installer = null;
            if (!reloadLocked) return;
            reloadLocked = false;
            EditorApplication.UnlockReloadAssemblies();
        }
    }
}
