using Kimchily.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kimchily.Creator.Editor
{
    // Editor-only companion: never added to authored scenes or published bundles.
    public static class CreatorChatPreview
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartChatPreview()
        {
            var chat = InGameChat.Ensure();
            chat.EnterWorld(SceneManager.GetActiveScene(), "editor-preview", "v1");
            chat.SetExpanded("true");
        }
    }
}
