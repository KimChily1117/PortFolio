using System.Collections;
using Kimchily.Creator;
using UnityEngine;

namespace Kimchily.World
{
    /// <summary>Built-in scene demonstration of the shared SDK coroutine service.</summary>
    [DisallowMultipleComponent]
    public sealed class KimchilyDemoBehaviour : MonoBehaviour
    {
        public Transform rotatingContent;
        public Animator animator;
        public float rotationDegreesPerSecond = 30f;

        CoroutineScheduler scheduler;
        CoroutineHandle rotation;
        CoroutineHandle animationPulse;
        float elapsed;
        GUIStyle titleStyle;
        GUIStyle textStyle;
        GUIStyle buttonStyle;

        void Start()
        {
            if (rotatingContent == null)
                rotatingContent = transform.childCount > 0 ? transform.GetChild(0) : transform;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            scheduler = GetComponent<CoroutineScheduler>();
            if (scheduler == null) scheduler = gameObject.AddComponent<CoroutineScheduler>();
            rotation = scheduler.StartRoutine(RotateContent(), gameObject);
            if (animator != null && animator.runtimeAnimatorController != null)
                animationPulse = scheduler.StartRoutine(PulseAnimation(), gameObject);
        }

        IEnumerator RotateContent()
        {
            Quaternion originalRotation = rotatingContent.localRotation;
            Vector3 originalPosition = rotatingContent.localPosition;
            try
            {
                while (rotatingContent != null)
                {
                    elapsed += Time.unscaledDeltaTime;
                    rotatingContent.localRotation = originalRotation *
                        Quaternion.Euler(0, elapsed * rotationDegreesPerSecond, 0);
                    rotatingContent.localPosition = originalPosition +
                        Vector3.up * (Mathf.Sin(elapsed * 1.4f) * 0.08f);
                    yield return null;
                }
            }
            finally
            {
                if (rotatingContent != null)
                {
                    rotatingContent.localRotation = originalRotation;
                    rotatingContent.localPosition = originalPosition;
                }
            }
        }

        IEnumerator PulseAnimation()
        {
            float originalSpeed = animator.speed;
            try
            {
                while (animator != null)
                {
                    animator.speed = 0.8f;
                    yield return new WaitForSecondsRealtime(1f);
                    if (animator == null) yield break;
                    animator.speed = 1.2f;
                    yield return new WaitForSecondsRealtime(1f);
                }
            }
            finally
            {
                if (animator != null) animator.speed = originalSpeed;
            }
        }

        void OnDisable()
        {
            rotation?.Cancel();
            animationPulse?.Cancel();
        }

        void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold };
                titleStyle.normal.textColor = Color.white;
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
                textStyle.normal.textColor = new Color(0.82f, 0.9f, 0.97f);
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            }

            float scale = Mathf.Max(0.7f, Screen.width / 960f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.Box(new Rect(20, 90, 430, 155), GUIContent.none);
            GUI.Label(new Rect(38, 102, 395, 40), "KIMCHILY / DEMO WORLD", titleStyle);
            GUI.Label(new Rect(38, 148, 390, 60),
                "Unity scene + imported model\nCoroutine runtime · " +
                (scheduler != null ? scheduler.ActiveCount : 0) + " running · " + elapsed.ToString("0.0") + "s", textStyle);
#if !UNITY_ANDROID || UNITY_EDITOR
            if (GUI.Button(new Rect(38, 204, 170, 32), "Return to host", buttonStyle))
                KimchilyHostBridge.Instance?.RequestCloseFromUI();
#endif
            GUI.matrix = previous;
        }
    }
}
