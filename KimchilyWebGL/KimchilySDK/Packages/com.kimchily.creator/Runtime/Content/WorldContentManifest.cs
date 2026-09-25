using System;

namespace Kimchily.Creator.Content
{
    public static class CreatorSdk
    {
        public const string Version = "0.1.0";
        public const int ManifestSchemaVersion = 1;
    }

    [Serializable]
    public sealed class WorldContentManifest
    {
        public int schemaVersion = CreatorSdk.ManifestSchemaVersion;
        public string sdkVersion = CreatorSdk.Version;
        public string unityVersion;
        public string platform;
        public string renderPipeline;
        public string worldId;
        public string revisionId;
        public string entryScene;
        public string[] scenes = Array.Empty<string>();
        public BundleFile[] bundles = Array.Empty<BundleFile>();
        public ScriptRequirement[] requiredTypes = Array.Empty<ScriptRequirement>();
    }

    [Serializable]
    public sealed class BundleFile
    {
        public string name;
        public string fileName;
        public string sha256;
        public long sizeBytes;
        public string unityHash;
        public uint crc;
        public string[] dependencies = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ScriptRequirement
    {
        public string assembly;
        public string type;
    }
}
