using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Kimchily.Creator.Content
{
    /// <summary>
    /// Owns bundles for one already-downloaded world revision.
    /// Unload all scenes and destroy instantiated assets before disposing this session.
    /// Transport (HTTP/QR), scripting initialization and scene transition policy are separate.
    /// </summary>
    public sealed class WorldContentSession : IDisposable
    {
        readonly Dictionary<string, AssetBundle> loaded = new Dictionary<string, AssetBundle>(StringComparer.Ordinal);
        readonly List<string> order = new List<string>();
        bool disposed;
        public WorldContentManifest Manifest { get; private set; }

        public static string CurrentPlatform
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.WindowsEditor:
                    case RuntimePlatform.WindowsPlayer: return "StandaloneWindows64";
                    case RuntimePlatform.OSXEditor:
                    case RuntimePlatform.OSXPlayer: return "StandaloneOSX";
                    case RuntimePlatform.LinuxEditor:
                    case RuntimePlatform.LinuxPlayer: return "StandaloneLinux64";
                    case RuntimePlatform.Android: return "Android";
                    case RuntimePlatform.IPhonePlayer: return "iOS";
                    case RuntimePlatform.WebGLPlayer: return "WebGL";
                    default: throw new NotSupportedException("Unsupported world platform: " + Application.platform);
                }
            }
        }

        public static string CurrentRenderPipeline =>
            GraphicsSettings.currentRenderPipeline == null ? "builtin" :
            GraphicsSettings.currentRenderPipeline.GetType().AssemblyQualifiedName;

        public static WorldContentSession OpenLocal(string revisionDirectory)
        {
            var root = Path.GetFullPath(revisionDirectory);
            var manifest = JsonUtility.FromJson<WorldContentManifest>(
                File.ReadAllText(Path.Combine(root, "world.json")));
            ValidateManifest(manifest);
            var files = manifest.bundles.ToDictionary(x => x.name, StringComparer.Ordinal);
            // Validate every file before loading any Unity object.
            foreach (var file in files.Values)
            {
                string path = SafeFilePath(root, file.fileName);
                var info = new FileInfo(path);
                if (!info.Exists || info.Length != file.sizeBytes ||
                    !string.Equals(ComputeSha256(path), file.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Bundle is missing or its size/hash differs: " + file.fileName);
            }
            var result = new WorldContentSession { Manifest = manifest };
            try
            {
                var visiting = new HashSet<string>(StringComparer.Ordinal);
                foreach (var file in files.Values) result.Load(file, files, visiting, root);
                string[] available = result.loaded.Values.Where(x => x.isStreamedSceneAssetBundle)
                    .SelectMany(x => x.GetAllScenePaths()).ToArray();
                if (manifest.scenes.Any(x => !available.Contains(x, StringComparer.OrdinalIgnoreCase)))
                    throw new InvalidDataException("A declared scene is missing from the bundles.");
                return result;
            }
            catch
            {
                result.ReleaseBundles();
                throw;
            }
        }

        public static void ValidateManifest(WorldContentManifest manifest)
        {
            WorldManifestValidation.Validate(manifest, Application.unityVersion, CurrentPlatform, CurrentRenderPipeline,
                type => Type.GetType(type.type + ", " + type.assembly, false) != null);
        }

        void Load(BundleFile file, Dictionary<string, BundleFile> files, HashSet<string> visiting, string root)
        {
            if (loaded.ContainsKey(file.name)) return;
            if (!visiting.Add(file.name)) throw new InvalidDataException("Cyclic bundle dependencies: " + file.name);
            foreach (string dependency in file.dependencies)
            {
                if (!files.TryGetValue(dependency, out var item))
                    throw new InvalidDataException("Missing bundle dependency: " + dependency);
                Load(item, files, visiting, root);
            }
            var bundle = AssetBundle.LoadFromFile(SafeFilePath(root, file.fileName), file.crc);
            if (bundle == null) throw new InvalidDataException("Unity could not load bundle: " + file.name);
            loaded.Add(file.name, bundle);
            order.Add(file.name);
            visiting.Remove(file.name);
        }

        public AsyncOperation LoadEntrySceneAsync(LoadSceneMode mode = LoadSceneMode.Additive)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WorldContentSession));
            return SceneManager.LoadSceneAsync(Manifest.entryScene, mode);
        }

        public void Dispose()
        {
            if (disposed) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && Manifest.scenes.Contains(scene.path, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Unload the world's scenes before disposing its bundle session.");
            }
            ReleaseBundles();
        }

        void ReleaseBundles()
        {
            disposed = true;
            for (int i = order.Count - 1; i >= 0; i--) loaded[order[i]].Unload(true);
            loaded.Clear();
            order.Clear();
        }

        public static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static string SafeFilePath(string root, string relative)
        {
            // Current schema uses flat bundle files. Never resolve external paths from manifest data.
            if (!WorldManifestValidation.IsSimpleFileName(relative))
                throw new InvalidDataException("Bundle file name must not contain a path.");
            return Path.Combine(root, relative);
        }
    }
}
