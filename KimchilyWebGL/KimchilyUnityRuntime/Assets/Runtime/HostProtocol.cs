using System;
using UnityEngine.Scripting;

namespace Kimchily.World
{
    public static class HostProtocol
    {
        public const int Version = 1;
        public const string BridgeObjectName = "KimchilyHostBridge";
        public const string DemoWorldId = "demo";
        public const string DemoRevisionId = "builtin-v1";
        public const string DemoSceneName = "DemoWorld";
    }

    [Serializable]
    [Preserve]
    public sealed class HostCommand
    {
        public int protocolVersion;
        public string type;
        public string requestId;
        public string worldId;
        public string revisionId;
        public string manifestUrl;
        public string manifestSha256;
        public string message;
        public string code;
        public float progress;
    }

    [Serializable]
    [Preserve]
    public sealed class HostEvent
    {
        public int protocolVersion = HostProtocol.Version;
        public string type;
        public string requestId;
        public string worldId;
        public string revisionId;
        public string message;
        public string code;
        public float progress;
    }

    public enum WorldRuntimeState
    {
        Idle,
        Loading,
        Ready,
        Closing,
        Faulted
    }
}
