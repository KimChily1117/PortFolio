using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kimchily.Creator.Content;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Kimchily.Creator.Editor
{
    [Serializable]
    public sealed class WorldPublishResponse
    {
        public string worldId;
        public string revisionId;
        public string manifestUrl;
        public string manifestSha256;
        public string publishUrl;
        public string qrUrl;
        public string launchUrl;
    }

    [Serializable]
    sealed class PublisherErrorResponse
    {
        public string code = string.Empty;
        public string message = string.Empty;
    }

    /// <summary>One Editor publication. ZIP/hash work is off-thread; all Unity APIs stay on the Editor thread.</summary>
    public sealed class WorldPublisherClient : IDisposable
    {
        const long MaximumBytes = 256L * 1024 * 1024;
        public bool Busy { get; private set; }
        public float Progress { get; private set; }
        public string Status { get; private set; }
        public string Error { get; private set; }
        public string QrWarning { get; private set; }
        public WorldPublishResponse Result { get; private set; }
        public Texture2D QrTexture { get; private set; }
        public event Action Changed;

        Task<string> packaging;
        CancellationTokenSource cancellation;
        UnityWebRequest request;
        UnityWebRequestAsyncOperation operation;
        string temporaryZip;
        string origin;
        string bearerToken;
        string expectedWorld;
        string expectedRevision;
        string expectedManifestSha;
        string expectedPlatform;
        bool readingQr;
        bool disposed;

        public void Begin(string buildDirectory, string serverUrl, string token)
        {
            if (Busy || disposed) throw new InvalidOperationException("Create a new publisher operation for each upload.");
            origin = ValidateOrigin(serverUrl);
            if (string.IsNullOrWhiteSpace(token) || token.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                throw new ArgumentException("Enter the local publisher token.");
            string directory = Path.GetFullPath(buildDirectory);
            string manifestPath = Path.Combine(directory, "world.json");
            if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 1024 * 1024)
                throw new InvalidDataException("Select a completed build with a world.json of at most 1 MiB.");
            byte[] manifestBytes = File.ReadAllBytes(manifestPath);
            var manifest = JsonUtility.FromJson<WorldContentManifest>(new UTF8Encoding(false, true).GetString(manifestBytes).TrimStart('\ufeff'));
            ValidateForPublishing(manifest);
            expectedWorld = manifest.worldId;
            expectedRevision = manifest.revisionId;
            expectedPlatform = manifest.platform;
            using (var sha = SHA256.Create())
                expectedManifestSha = BitConverter.ToString(sha.ComputeHash(manifestBytes)).Replace("-", "").ToLowerInvariant();
            bearerToken = token.Trim(); // Session memory only; never written into assets, preferences or logs.
            cancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellation.Token;
            Busy = true;
            Status = "Preparing ZIP and checking bundle hashes...";
            packaging = Task.Run(() => CreateArchive(directory, manifest, manifestBytes, cancellationToken), cancellationToken);
            EditorApplication.update += Tick;
            Changed?.Invoke();
        }

        public static void ValidateForPublishing(WorldContentManifest manifest)
        {
            if (manifest == null || manifest.schemaVersion != CreatorSdk.ManifestSchemaVersion)
                throw new InvalidDataException("Unsupported world manifest schema.");
            if (manifest.platform != "Android" && manifest.platform != "WebGL")
                throw new InvalidDataException("Build Android or WebGL content before publishing.");
            if (manifest.renderPipeline != "builtin")
                throw new InvalidDataException("The current world players require the Built-in Render Pipeline.");
            if (manifest.scenes == null || manifest.scenes.Length != 1 || manifest.scenes[0] != manifest.entryScene)
                throw new InvalidDataException("The current player supports exactly one declared entry scene.");
            WorldManifestValidation.Validate(manifest, Application.unityVersion, manifest.platform, manifest.renderPipeline, _ => true);
            WorldContentBuilder.ValidatePortableTypes(manifest.requiredTypes);
            if (manifest.bundles.Length > 64 || manifest.bundles.Any(file => file.fileName.Equals("world.json", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Declare at most 64 bundles, with world.json reserved for the manifest.");
            if (!ValidId(manifest.worldId) || !ValidId(manifest.revisionId))
                throw new InvalidDataException("World/revision IDs must contain 1–80 letters, digits, underscores or hyphens.");
        }

        static bool ValidId(string value) => value != null && value.Length > 0 && value.Length <= 80 &&
            value.All(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '_' || c == '-');

        static string ValidateOrigin(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/")
                throw new ArgumentException("Server URL must be an HTTP(S) origin, e.g. http://127.0.0.1:8788.");
            return value.TrimEnd('/');
        }

        static string CreateArchive(string directory, WorldContentManifest manifest, byte[] manifestBytes, CancellationToken token)
        {
            string output = Path.Combine(Path.GetTempPath(), "kimchily-publish-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                long total = manifestBytes.LongLength;
                foreach (var bundle in manifest.bundles)
                {
                    token.ThrowIfCancellationRequested();
                    string path = Path.Combine(directory, bundle.fileName);
                    if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 ||
                        new FileInfo(path).Length != bundle.sizeBytes)
                        throw new InvalidDataException("Missing, redirected or changed bundle: " + bundle.fileName);
                    total = checked(total + bundle.sizeBytes);
                    if (total > MaximumBytes) throw new InvalidDataException("Expanded publication exceeds 256 MiB.");
                    using (var sha = SHA256.Create())
                    using (var input = File.OpenRead(path))
                    {
                        string digest = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                        if (!string.Equals(digest, bundle.sha256, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Bundle SHA-256 differs from world.json: " + bundle.fileName);
                    }
                }
                using (var destination = new FileStream(output, FileMode.CreateNew, FileAccess.Write))
                using (var archive = new ZipArchive(destination, ZipArchiveMode.Create))
                {
                    foreach (string name in new[] { "world.json" }.Concat(manifest.bundles.Select(file => file.fileName)))
                    {
                        token.ThrowIfCancellationRequested();
                        ZipArchiveEntry entry = archive.CreateEntry(name, System.IO.Compression.CompressionLevel.Fastest);
                        using (Stream source = name == "world.json" ? (Stream)new MemoryStream(manifestBytes, false) : File.OpenRead(Path.Combine(directory, name)))
                        using (var target = entry.Open())
                        {
                            byte[] buffer = new byte[256 * 1024];
                            int count;
                            while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                token.ThrowIfCancellationRequested();
                                target.Write(buffer, 0, count);
                            }
                        }
                    }
                }
                if (new FileInfo(output).Length > MaximumBytes) throw new InvalidDataException("ZIP upload exceeds 256 MiB.");
                return output;
            }
            catch
            {
                TryDelete(output);
                throw;
            }
        }

        void Tick()
        {
            if (!Busy || disposed) return;
            try
            {
                if (packaging != null)
                {
                    if (!packaging.IsCompleted) return;
                    temporaryZip = packaging.GetAwaiter().GetResult();
                    packaging = null;
                    request = new UnityWebRequest(origin + "/api/publish", UnityWebRequest.kHttpVerbPOST)
                    {
                        uploadHandler = new UploadHandlerFile(temporaryZip),
                        downloadHandler = new DownloadHandlerBuffer(), timeout = 120, redirectLimit = 0
                    };
                    request.SetRequestHeader("Content-Type", "application/zip");
                    request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
                    operation = request.SendWebRequest();
                    Status = "Uploading the validated " + expectedPlatform + " world...";
                }
                if (operation == null) return;
                Progress = readingQr ? 0.95f : Mathf.Clamp01(request.uploadProgress) * 0.9f;
                Changed?.Invoke();
                if (!operation.isDone) return;
                if (readingQr)
                {
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        QrTexture = DownloadHandlerTexture.GetContent(request);
                        QrTexture.filterMode = FilterMode.Point;
                    }
                    else QrWarning = "Publication succeeded, but the QR image could not be loaded. Open the published page to retry.";
                    Complete();
                    return;
                }
                if (request.result != UnityWebRequest.Result.Success || request.responseCode != 201)
                {
                    if (request.responseCode == 0)
                        throw new InvalidOperationException("Publish failed: cannot connect to " + origin +
                            ". Check Server URL and start the publisher, then verify " + origin +
                            "/health. Your built world is preserved; use Publish Last Build to retry. (" + request.error + ")");
                    string details = request.error;
                    try
                    {
                        var problem = JsonUtility.FromJson<PublisherErrorResponse>(request.downloadHandler.text);
                        if (!string.IsNullOrEmpty(problem?.message)) details = problem.code + ": " + problem.message;
                    }
                    catch (ArgumentException) { }
                    throw new InvalidOperationException("Publish failed (HTTP " + request.responseCode + "): " + details);
                }
                Result = JsonUtility.FromJson<WorldPublishResponse>(request.downloadHandler.text);
                ValidatePublicationResponse(Result, expectedWorld, expectedRevision, expectedManifestSha, expectedPlatform);
                request.Dispose();
                request = UnityWebRequestTexture.GetTexture(Result.qrUrl, true);
                request.timeout = 30;
                request.redirectLimit = 0;
                operation = request.SendWebRequest();
                readingQr = true;
                Status = "Published. Loading its QR image...";
            }
            catch (Exception error)
            {
                Error = error.GetBaseException().Message;
                Status = "Publication failed.";
                Finish();
            }
        }

        static bool IsWebUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out Uri uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && string.IsNullOrEmpty(uri.UserInfo);

        internal static void ValidatePublicationResponse(WorldPublishResponse result, string world, string revision, string hash, string platform)
        {
            if (result == null || result.worldId != world || result.revisionId != revision ||
                !IsWebUrl(result.manifestUrl) || !IsWebUrl(result.publishUrl) || !IsWebUrl(result.qrUrl) ||
                !string.Equals(result.manifestSha256, hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Publisher returned an invalid publication response.");
            var manifest = new Uri(result.manifestUrl);
            string publicOrigin = manifest.GetLeftPart(UriPartial.Authority);
            if (!MatchesPublishedPath(manifest, publicOrigin, "/worlds/" + world + "/" + revision + "/world.json") ||
                !MatchesPublishedPath(new Uri(result.publishUrl), publicOrigin, "/w/" + world + "/" + revision) ||
                !MatchesPublishedPath(new Uri(result.qrUrl), publicOrigin, "/qr/" + world + "/" + revision + ".png") ||
                !Uri.TryCreate(result.launchUrl, UriKind.Absolute, out Uri launch) ||
                !string.IsNullOrEmpty(launch.UserInfo) || !string.IsNullOrEmpty(launch.Fragment))
                throw new InvalidDataException("Publisher links do not identify this world revision.");
            if (platform == "WebGL")
            {
                if (!IsWebUrl(result.launchUrl) || launch.GetLeftPart(UriPartial.Authority) != publicOrigin || launch.AbsolutePath != "/player/")
                    throw new InvalidDataException("The Web launch URL must use the published world's origin and player path.");
            }
            else if (platform != "Android" || launch.Scheme != "kimchily" || launch.Host != "world" ||
                     (launch.AbsolutePath != "" && launch.AbsolutePath != "/"))
                throw new InvalidDataException("The Android launch URL is invalid.");
            string[] fields = launch.Query.TrimStart('?').Split('&');
            if (fields.Length != 2) throw new InvalidDataException("The launch URL must contain exactly the manifest and SHA-256.");
            string linkedManifest = null, linkedHash = null;
            foreach (string field in fields)
            {
                int separator = field.IndexOf('=');
                if (separator <= 0) throw new InvalidDataException("Invalid launch URL parameters.");
                string key = Uri.UnescapeDataString(field.Substring(0, separator).Replace("+", " "));
                string value = Uri.UnescapeDataString(field.Substring(separator + 1).Replace("+", " "));
                if (key == "manifest" && linkedManifest == null) linkedManifest = value;
                else if (key == "sha256" && linkedHash == null) linkedHash = value;
                else throw new InvalidDataException("Duplicate or unsupported launch URL parameter.");
            }
            if (linkedManifest != result.manifestUrl || !string.Equals(linkedHash, hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The launch URL does not match the validated manifest and SHA-256.");
        }

        static bool MatchesPublishedPath(Uri value, string origin, string path) =>
            value.GetLeftPart(UriPartial.Authority) == origin && value.AbsolutePath == path &&
            string.IsNullOrEmpty(value.Query) && string.IsNullOrEmpty(value.Fragment);

        void Complete()
        {
            Progress = 1;
            Status = expectedPlatform == "WebGL"
                ? "Published successfully. Scan the QR with the phone camera to open the world in your browser."
                : "Published successfully. Scan the QR with the Kimchily Android app installed.";
            Finish();
        }

        void Finish()
        {
            Busy = false;
            EditorApplication.update -= Tick;
            request?.Dispose();
            request = null;
            operation = null;
            bearerToken = null;
            TryDelete(temporaryZip);
            temporaryZip = null;
            Changed?.Invoke();
        }

        static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            cancellation?.Cancel();
            if (packaging != null)
                packaging.ContinueWith(task => { if (task.Status == TaskStatus.RanToCompletion) TryDelete(task.Result); });
            request?.Abort();
            Finish();
            cancellation?.Dispose();
            if (QrTexture != null) UnityEngine.Object.DestroyImmediate(QrTexture);
            QrTexture = null;
        }
    }
}
