using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PatternWarningPresenter2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldState2D _worldState;
    [SerializeField] private GameObject _shockwaveWarningPrefab;
    [SerializeField] private GameObject _lightZoneWarningPrefab;
    [SerializeField] private Transform _warningRoot;

    [Header("Common")]
    [SerializeField] private float _positionScale = 0.01f;
    [SerializeField] private float _yOffset = -0.15f;

    [Header("Shockwave")]
    [SerializeField] private float _shockwaveWarningDuration = 20f / 30f;

    public void ShowShockwaveWarning(int attackerId)
    {
        if (_worldState == null)
        {
            Debug.LogWarning("[Warning] WorldState2D is not assigned.");
            return;
        }

        if (_shockwaveWarningPrefab == null)
        {
            Debug.LogWarning("[Warning] ShockwaveWarningPrefab is not assigned.");
            return;
        }

        if (!_worldState.TryGetActor(attackerId, out ActorStateData attacker))
        {
            Debug.LogWarning($"[Warning] Attacker actor not found. attackerId={attackerId}");
            return;
        }

        Vector3 worldPos = new Vector3(
            attacker.PosX * _positionScale,
            attacker.PosY * _positionScale + _yOffset,
            0f);

        GameObject warning = Instantiate(
            _shockwaveWarningPrefab,
            worldPos,
            Quaternion.identity,
            _warningRoot != null ? _warningRoot : transform);

        StartCoroutine(DestroyAfter(warning, _shockwaveWarningDuration));
    }

    public void ShowLightZoneWarning(List<PatternZoneData> zones, int startServerTick, int durationTick)
    {
        if (_lightZoneWarningPrefab == null)
        {
            Debug.LogWarning("[Warning] LightZoneWarningPrefab is not assigned.");
            return;
        }

        foreach (PatternZoneData zone in zones)
        {
            Vector3 pos = new Vector3(
                zone.PosX * _positionScale,
                zone.PosY * _positionScale,
                0f);

            GameObject warning = Instantiate(
                _lightZoneWarningPrefab,
                pos,
                Quaternion.identity,
                _warningRoot != null ? _warningRoot : transform);

            float diameter = zone.Radius * 2f * _positionScale;
            warning.transform.localScale = new Vector3(diameter, diameter, 1f);

            PatternZoneVisual visual = warning.GetComponent<PatternZoneVisual>();
            if (visual != null)
            {
                visual.WorldState = _worldState;
                visual.EndServerTick = startServerTick + durationTick;
            }
        }
    }

    private IEnumerator DestroyAfter(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (obj != null)
            Destroy(obj);
    }
}