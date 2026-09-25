using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kimchily.Creator.Content
{
    /// <summary>Pure managed validation. No Unity/native calls: shared by loader and build verification.</summary>
    public static class WorldManifestValidation
    {
        public static void Validate(WorldContentManifest manifest, string unityVersion, string platform,
            string renderPipeline, Func<ScriptRequirement, bool> typeAvailable)
        {
            if (manifest == null || manifest.schemaVersion != CreatorSdk.ManifestSchemaVersion)
                throw new InvalidDataException("Unsupported world manifest schema.");
            if (manifest.sdkVersion != CreatorSdk.Version || manifest.unityVersion != unityVersion)
                throw new InvalidDataException("SDK or Unity version differs from this player.");
            if (manifest.platform != platform || manifest.renderPipeline != renderPipeline)
                throw new InvalidDataException("Content platform or render pipeline differs from this player.");
            if (string.IsNullOrWhiteSpace(manifest.worldId) || string.IsNullOrWhiteSpace(manifest.revisionId))
                throw new InvalidDataException("Missing world/revision ID.");
            if (manifest.scenes == null || manifest.scenes.Length == 0 ||
                manifest.scenes.Any(string.IsNullOrWhiteSpace) ||
                manifest.scenes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.scenes.Length ||
                !manifest.scenes.Contains(manifest.entryScene, StringComparer.Ordinal))
                throw new InvalidDataException("Entry scene must be explicitly declared; scene paths must be unique.");
            if (manifest.bundles == null || manifest.bundles.Length == 0 ||
                manifest.bundles.Any(x => x == null || string.IsNullOrWhiteSpace(x.name) ||
                    !IsSimpleFileName(x.fileName) || x.sizeBytes < 0 || x.dependencies == null ||
                    x.sha256 == null || x.sha256.Length != 64 || !x.sha256.All(IsHex)))
                throw new InvalidDataException("Invalid bundle records.");
            if (manifest.bundles.Select(x => x.name).Distinct(StringComparer.Ordinal).Count() != manifest.bundles.Length ||
                manifest.bundles.Select(x => x.fileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.bundles.Length)
                throw new InvalidDataException("Duplicate bundle names/files.");
            var files = manifest.bundles.ToDictionary(x => x.name, StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in manifest.bundles) Visit(file, files, visited, visiting);
            if (manifest.requiredTypes == null || typeAvailable == null)
                throw new InvalidDataException("Missing required type list/resolver.");
            foreach (var type in manifest.requiredTypes)
                if (type == null || string.IsNullOrWhiteSpace(type.assembly) ||
                    string.IsNullOrWhiteSpace(type.type) || !typeAvailable(type))
                    throw new InvalidDataException("The player does not contain a required C# type: " + type?.type);
        }

        static void Visit(BundleFile file, Dictionary<string, BundleFile> files,
            HashSet<string> visited, HashSet<string> visiting)
        {
            if (visited.Contains(file.name)) return;
            if (!visiting.Add(file.name)) throw new InvalidDataException("Cyclic bundle dependencies: " + file.name);
            foreach (string dependency in file.dependencies)
            {
                if (string.IsNullOrWhiteSpace(dependency) || !files.TryGetValue(dependency, out var required))
                    throw new InvalidDataException("Missing bundle dependency: " + dependency);
                Visit(required, files, visited, visiting);
            }
            visiting.Remove(file.name);
            visited.Add(file.name);
        }

        public static bool IsSimpleFileName(string name) =>
            !string.IsNullOrWhiteSpace(name) && name != "." && name != ".." &&
            name.All(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' ||
                          c >= '0' && c <= '9' || c == '-' || c == '_' || c == '.');
        static bool IsHex(char c) => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F';
    }
}

