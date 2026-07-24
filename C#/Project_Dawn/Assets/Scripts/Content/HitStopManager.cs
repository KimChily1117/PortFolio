using System.Collections;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
    private static HitStopManager s_instance;
    private Coroutine _hitStopCoroutine;

    public static HitStopManager Instance
    {
        get
        {
            if (s_instance == null)
            {
                GameObject go = new GameObject { name = "@HitStopManager" };
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<HitStopManager>();
            }

            return s_instance;
        }
    }

    public void Play(float duration = 0.035f, float timeScale = 0.08f)
    {
        if (duration <= 0f)
            return;

        if (_hitStopCoroutine != null)
            StopCoroutine(_hitStopCoroutine);

        _hitStopCoroutine = StartCoroutine(HitStopRoutine(duration, Mathf.Clamp(timeScale, 0.01f, 1f)));
    }

    private IEnumerator HitStopRoutine(float duration, float timeScale)
    {
        float previousScale = Time.timeScale;
        Time.timeScale = timeScale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = previousScale;
        _hitStopCoroutine = null;
    }
}
