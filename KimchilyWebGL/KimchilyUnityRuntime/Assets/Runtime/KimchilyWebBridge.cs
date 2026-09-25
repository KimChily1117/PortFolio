using System;
using System.Runtime.InteropServices;
using Kimchily.Creator.Mobile;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace Kimchily.World
{
    /// <summary>Browser adapter for the same protocol used by the native host.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(KimchilyHostBridge)), Preserve]
    public sealed class KimchilyWebBridge : MonoBehaviour
    {
        KimchilyHostBridge bridge;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void KimchilyWeb_OnEvent(string json);
#endif

        void OnEnable()
        {
            bridge = GetComponent<KimchilyHostBridge>();
            bridge.EventRaised += Forward;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Leave HTML controls usable while the canvas is not focused.
            WebGLInput.captureAllKeyboardInput = false;
#endif
        }

        void OnDisable()
        {
            if (bridge != null) bridge.EventRaised -= Forward;
        }

        void Forward(HostEvent payload)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { KimchilyWeb_OnEvent(JsonUtility.ToJson(payload)); }
            catch (Exception exception) { Debug.LogWarning("Kimchily browser callback failed: " + exception.Message); }
#endif
        }

        // Called when the browser hides, rotates, loses focus or cancels a touch.
        [Preserve]
        public void CancelInput(string unused)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (KimchilyMobileControls controls in root.GetComponentsInChildren<KimchilyMobileControls>(true))
                        controls.ClearInput();
            }
        }
    }
}
