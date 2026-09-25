using Kimchily.Creator.Content;
using UnityEngine;

namespace Kimchily.World.Tests
{
    // Created in a sceneLoaded callback so the bridge must observe it before WorldReady.
    public sealed class ScriptStartupProbe : MonoBehaviour, IWorldScriptStatus
    {
        public bool HasStarted { get; set; }
        public bool IsFaulted { get; set; }
        public string LastError { get; set; }
    }
}
