using System.Collections;
using Kimchily.Creator;
using UnityEngine;

[RequireComponent(typeof(CoroutineScheduler))]
public sealed class CoroutineExample : MonoBehaviour
{
    CoroutineHandle handle;
    void Start() => handle = GetComponent<CoroutineScheduler>().StartRoutine(Animate(), gameObject);
    IEnumerator Animate()
    {
        try
        {
            yield return new WaitForSeconds(1);
            for (int i = 0; i < 60; i++)
            {
                transform.Rotate(Vector3.up, 90 * Time.deltaTime);
                yield return null;
            }
        }
        finally { Debug.Log("Coroutine example cleaned up."); }
    }
    void OnDisable() => handle?.Cancel();
}

