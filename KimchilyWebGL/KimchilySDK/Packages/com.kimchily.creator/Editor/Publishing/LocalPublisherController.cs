using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Kimchily.Creator.Editor
{
    [Serializable]
    internal sealed class LocalPublisherStatus
    {
        public int schemaVersion;
        public string status;
        public string localUrl;
        public string publicUrl;
        public string stateDirectory;
        public string message;
        public int pid;
        public bool processVerified;
        public bool healthy;
        public bool IsReady => schemaVersion == 1 && status == "running" && processVerified && healthy;
    }

    /// <summary>Runs the local publisher's bounded management commands outside the GUI thread.</summary>
    internal sealed class LocalPublisherController : IDisposable
    {
        sealed class CommandResult { internal string Output, Error; internal int ExitCode; }
        Task<CommandResult> pending;
        bool disposed;
        public bool Busy => pending != null;
        public LocalPublisherStatus Status { get; private set; }
        public string Error { get; private set; }
        public event Action Changed;

        internal static string FindPublisherDirectory(string projectDirectory)
        {
            var directory = new DirectoryInfo(Path.GetFullPath(projectDirectory));
            for (int depth = 0; directory != null && depth < 6; depth++, directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "KimchilyPublish");
                if (File.Exists(Path.Combine(candidate, "tools", "start.ps1")) &&
                    File.Exists(Path.Combine(candidate, "server.py"))) return candidate;
            }
            return string.Empty;
        }

        public void Run(string directory, string command)
        {
            if (disposed || Busy) return;
            if (command != "start" && command != "status" && command != "stop")
                throw new ArgumentException("Unknown local publisher command.");
            if (Application.platform != RuntimePlatform.WindowsEditor)
                throw new NotSupportedException("Local publisher buttons currently require Windows. Use Server URL for another server.");
            string script = Path.Combine(Path.GetFullPath(directory), "tools", command + ".ps1");
            if (!File.Exists(script)) throw new FileNotFoundException("Select the KimchilyPublish folder containing tools/" + command + ".ps1.");
            Error = null;
            pending = Task.Run(() => Execute(script));
            EditorApplication.update += Tick;
            Changed?.Invoke();
        }

        static CommandResult Execute(string script)
        {
            // EncodedCommand preserves spaces and apostrophes in a local path without shell interpolation.
            string program = "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); & '" +
                script.Replace("'", "''") + "' -Json";
            var info = new ProcessStartInfo("powershell.exe", "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " +
                Convert.ToBase64String(Encoding.Unicode.GetBytes(program)))
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            using (var process = Process.Start(info))
            {
                if (process == null) throw new InvalidOperationException("Could not start the publisher management tool.");
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(45000))
                {
                    // Stop only this command wrapper. Never guess which server or port owner to stop.
                    process.Kill();
                    throw new TimeoutException("Publisher management timed out. Refresh its status before retrying.");
                }
                // A descendant must not keep our redirected pipes open indefinitely.
                if (!Task.WaitAll(new Task[] { output, error }, 3000))
                {
                    output.ContinueWith(task => { if (task.IsFaulted) _ = task.Exception; });
                    error.ContinueWith(task => { if (task.IsFaulted) _ = task.Exception; });
                    throw new TimeoutException("The publisher command ended but its output did not close. Refresh server status before retrying.");
                }
                return new CommandResult { Output = output.GetAwaiter().GetResult(), Error = error.GetAwaiter().GetResult(), ExitCode = process.ExitCode };
            }
        }

        void Tick()
        {
            if (pending == null || !pending.IsCompleted) return;
            var finished = pending;
            pending = null;
            EditorApplication.update -= Tick;
            try
            {
                CommandResult result = finished.GetAwaiter().GetResult();
                Status = string.IsNullOrWhiteSpace(result.Output) ? null : JsonUtility.FromJson<LocalPublisherStatus>(result.Output.Trim());
                if (Status == null || Status.schemaVersion != 1)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? "The publisher tool returned no valid status." : result.Error.Trim());
                if (result.ExitCode != 0) Error = Status.message ?? "Local publisher command failed.";
            }
            catch (Exception exception) { Status = null; Error = exception.GetBaseException().Message; }
            if (!disposed) Changed?.Invoke();
        }

        internal static string ReadLocalToken(string publisherDirectory, LocalPublisherStatus status)
        {
            if (status == null || !status.IsReady) throw new InvalidOperationException("Start the local publisher or refresh its status first.");
            if (!Uri.TryCreate(status.localUrl, UriKind.Absolute, out Uri uri) || !uri.IsLoopback ||
                uri.Scheme != Uri.UriSchemeHttp || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidDataException("Local publisher credentials can only be connected to a loopback HTTP origin.");
            string expected = Path.GetFullPath(Path.Combine(publisherDirectory, ".local")).TrimEnd(Path.DirectorySeparatorChar);
            if (string.IsNullOrEmpty(status.stateDirectory) ||
                !string.Equals(expected, Path.GetFullPath(status.stateDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The publisher state directory does not match the selected local publisher.");
            string tokenPath = Path.Combine(expected, "token");
            if (!File.Exists(tokenPath) || new FileInfo(tokenPath).Length > 4096)
                throw new InvalidDataException("The local publisher token file is missing or invalid.");
            string token = File.ReadAllText(tokenPath).Trim();
            if (string.IsNullOrEmpty(token) || token.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                throw new InvalidDataException("The local publisher token file is invalid.");
            return token;
        }

        internal static void ValidateCredentialDestination(string tokenOrigin, string serverUrl)
        {
            if (string.IsNullOrEmpty(tokenOrigin)) return; // A manually entered credential has no automatic binding.
            if (!Uri.TryCreate(tokenOrigin, UriKind.Absolute, out Uri expected) ||
                !Uri.TryCreate(serverUrl, UriKind.Absolute, out Uri actual) || !actual.IsLoopback ||
                !string.Equals(expected.GetLeftPart(UriPartial.Authority), actual.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase) ||
                actual.AbsolutePath != "/" || !string.IsNullOrEmpty(actual.Query) || !string.IsNullOrEmpty(actual.Fragment) || !string.IsNullOrEmpty(actual.UserInfo))
                throw new InvalidOperationException("The server address changed. Reconnect the local publisher or enter that server's own token.");
        }

        public void Dispose()
        {
            disposed = true;
            EditorApplication.update -= Tick;
            // Closing the Editor window must not shut down a server used by phones.
            if (pending != null) pending.ContinueWith(task => { if (task.IsFaulted) _ = task.Exception; });
            pending = null;
            Changed = null;
        }
    }
}
