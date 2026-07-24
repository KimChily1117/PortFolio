using System.Collections;
using UnityEngine;

public class HitEffectPlayer : MonoBehaviour
{
    private Coroutine _lifeRoutine;

    public void Play(float lifetime = 0.75f)
    {
        if (_lifeRoutine != null)
            StopCoroutine(_lifeRoutine);

        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particle in particles)
        {
            particle.gameObject.SetActive(true);
            particle.Clear(true);
            particle.Play(true);
        }

        _lifeRoutine = StartCoroutine(LifeRoutine(lifetime));
    }

    private IEnumerator LifeRoutine(float lifetime)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, lifetime));
        _lifeRoutine = null;
        GameManager.ObjectPool.Push(gameObject);
    }
}
