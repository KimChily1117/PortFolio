using Kimchily.Networking;
using UnityEngine;

namespace Kimchily.Creator.Editor
{
    // Editor-only companion: never added to authored scenes or published bundles.
    public static class CreatorChatPreview
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartChatPreview() => InGameChat.Ensure().SetContext("editor-preview", "v1");
    }
}
