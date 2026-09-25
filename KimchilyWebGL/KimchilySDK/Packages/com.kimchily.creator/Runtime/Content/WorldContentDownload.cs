using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Kimchily.Creator.Content
{
    /// <summary>One bounded download into a private temporary directory. Tick on Unity's main thread.</summary>
    public sealed class WorldContentDownload : IDisposable
    {
        public const long MaximumWorldBytes = 256L * 1024 * 1024;
        const long MaximumManifestBytes = 1024 * 1024;
        readonly Uri manifestUri;
        readonly string expectedHash;
        readonly string worldId;
        readonly string revisionId;
        UnityWebRequest request;
        int bundleIndex = -1;
        long finishedBytes;
        long totalBytes;
        long requestLimit;
        string filePath;
        bool disposed;

        public string DirectoryPath { get; }
        public WorldContentManifest Manifest { get; private set; }
        public bool IsComplete { get; private set; }
        public Exception Error { get; private set; }
        public float Progress => Manifest == null ? 0 : Mathf.Clamp01(
            (float)(finishedBytes + (long)(request?.downloadedBytes ?? 0)) / Math.Max(1, totalBytes));

        public WorldContentDownload(string url, string sha256, string world, string revision)
        {
            manifestUri = ValidateAddress(url, sha256, world, revision, Debug.isDebugBuild || Application.isEditor);
            expectedHash = sha256;
            worldId = world;
            revisionId = revision;
            DirectoryPath = Path.Combine(Application.temporaryCachePath, "KimchilyWorldDownloads", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                Begin(manifestUri, "world.json", MaximumManifestBytes);
            }
            catch { Dispose(); throw; }
        }

        public static Uri ValidateAddress(string url, string sha256, string world, string revision, bool allowHttp)
        {
            if (!IsIdentifier(world) || !IsIdentifier(revision)) throw new InvalidDataException("Invalid world/revision ID.");
            if (sha256 == null || sha256.Length != 64 || !sha256.All(IsHex))
                throw new InvalidDataException("The world link must include the manifest SHA-256.");
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                !(uri.Scheme == Uri.UriSchemeHttps || allowHttp && uri.Scheme == Uri.UriSchemeHttp) ||
                string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                uri.AbsolutePath != "/worlds/" + world + "/" + revision + "/world.json")
                throw new InvalidDataException("Invalid manifest address. Release players require HTTPS.");
            return uri;
        }

        public static bool IsIdentifier(string value) => !string.IsNullOrEmpty(value) && value.Length <= 80 &&
            value.All(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_');
        static bool IsHex(char c) => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F';

        void Begin(Uri address, string name, long limit)
        {
            filePath = Path.Combine(DirectoryPath, name);
            requestLimit = limit;
            request = new UnityWebRequest(address.AbsoluteUri, UnityWebRequest.kHttpVerbGET);
            request.downloadHandler = new DownloadHandlerFile(filePath) { removeFileOnAbort = true };
            request.redirectLimit = 0;
            request.timeout = 30;
            request.SendWebRequest();
        }

        public void Tick()
        {
            if (disposed || IsComplete || Error != null || request == null) return;
            try
            {
                if (request.downloadedBytes > (ulong)requestLimit ||
                    long.TryParse(request.GetResponseHeader("Content-Length"), out long announced) && announced > requestLimit)
                    throw new InvalidDataException("Download exceeded its declared size limit.");
                if (!request.isDone) return;
                if (request.result != UnityWebRequest.Result.Success)
                    throw new IOException("World download failed (HTTP " + request.responseCode + "): " + request.error);
                request.Dispose();
                request = null;
                if (bundleIndex < 0)
                {
                    if (new FileInfo(filePath).Length > MaximumManifestBytes ||
                        !string.Equals(WorldContentSession.ComputeSha256(filePath), expectedHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The downloaded manifest does not match this QR link.");
                    Manifest = JsonUtility.FromJson<WorldContentManifest>(File.ReadAllText(filePath));
                    WorldContentSession.ValidateManifest(Manifest);
                    if (Manifest.worldId != worldId || Manifest.revisionId != revisionId)
                        throw new InvalidDataException("Manifest world/revision does not match this QR link.");
                    if (Manifest.bundles.Length > 64 || Manifest.scenes.Length != 1 || Manifest.bundles.Any(
                        b => b.sizeBytes <= 0 || b.sizeBytes > MaximumWorldBytes || b.fileName.Equals("world.json", StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException("Unsupported world size or reserved bundle name.");
                    totalBytes = Manifest.bundles.Sum(b => b.sizeBytes);
                    if (totalBytes > MaximumWorldBytes) throw new InvalidDataException("World exceeds the 256 MiB development limit.");
                    bundleIndex = 0;
                }
                else
                {
                    BundleFile downloaded = Manifest.bundles[bundleIndex];
                    if (new FileInfo(filePath).Length != downloaded.sizeBytes ||
                        !string.Equals(WorldContentSession.ComputeSha256(filePath), downloaded.sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Bundle size or SHA-256 differs: " + downloaded.fileName);
                    finishedBytes += downloaded.sizeBytes;
                    bundleIndex++;
                }
                if (bundleIndex == Manifest.bundles.Length) { IsComplete = true; return; }
                BundleFile next = Manifest.bundles[bundleIndex];
                Begin(new Uri(manifestUri, Uri.EscapeDataString(next.fileName)), next.fileName, next.sizeBytes);
            }
            catch (Exception exception)
            {
                Error = exception;
                request?.Abort();
                request?.Dispose();
                request = null;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            request?.Abort();
            request?.Dispose();
            request = null;
            // This path is generated locally, never taken from a URL or manifest.
            if (Directory.Exists(DirectoryPath))
            {
                try { Directory.Delete(DirectoryPath, true); }
                catch (IOException exception) { Debug.LogWarning("Temporary world cleanup: " + exception.Message); }
                catch (UnauthorizedAccessException exception) { Debug.LogWarning("Temporary world cleanup: " + exception.Message); }
            }
        }
    }
}
