using Character;
using UnityEngine;

public class RemotePlayerLod : MonoBehaviour
{
    const float NearDistance = 8f;
    const float MidDistance = 15f;
    const float RefreshInterval = 0.25f;
    public const float MidVisualUpdateInterval = 0.1f;

    Renderer[] _renderers;
    Canvas[] _canvases;
    Animator _animator;
    float _nextRefreshTime;
    LodLevel _currentLevel = LodLevel.Unknown;

    public bool SkipExpensiveVisualUpdate { get; private set; }
    public bool UseReducedVisualUpdate { get; private set; }

    enum LodLevel
    {
        Unknown,
        Near,
        Mid,
        Far
    }

    void Awake()
    {
        CacheComponents();
    }

    void Start()
    {
        Refresh(force: true);
    }

    void LateUpdate()
    {
        if (Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + RefreshInterval;
        Refresh(force: false);
    }

    void CacheComponents()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _canvases = GetComponentsInChildren<Canvas>(true);
        _animator = GetComponentInChildren<Animator>(true);
    }

    void Refresh(bool force)
    {
        BaseCharacter myPlayer = GameManager.ObjectManager != null ? GameManager.ObjectManager.MyPlayer : null;
        if (myPlayer == null)
        {
            ApplyLevel(LodLevel.Near, force);
            return;
        }

        float distance = Vector2.Distance(transform.position, myPlayer.transform.position);
        LodLevel nextLevel;
        if (distance <= NearDistance)
            nextLevel = LodLevel.Near;
        else if (distance <= MidDistance)
            nextLevel = LodLevel.Mid;
        else
            nextLevel = LodLevel.Far;

        ApplyLevel(nextLevel, force);
    }

    void ApplyLevel(LodLevel level, bool force)
    {
        if (!force && _currentLevel == level)
            return;

        _currentLevel = level;

        bool showRenderers = level != LodLevel.Far;
        bool showNameLabels = level == LodLevel.Near;
        bool enableAnimator = level != LodLevel.Far;
        float animatorSpeed = level == LodLevel.Mid ? 0.35f : 1f;

        SetRenderers(showRenderers);
        SetCanvases(showNameLabels);
        SetAnimator(enableAnimator, animatorSpeed);
    }

    void SetRenderers(bool enabled)
    {
        if (_renderers == null)
            return;

        foreach (Renderer renderer in _renderers)
        {
            if (renderer != null)
                renderer.enabled = enabled;
        }
    }

    void SetCanvases(bool enabled)
    {
        if (_canvases == null)
            return;

        foreach (Canvas canvas in _canvases)
        {
            if (canvas != null)
                canvas.enabled = enabled;
        }
    }

    void SetAnimator(bool enabled, float speed)
    {
        if (_animator == null)
            return;

        _animator.enabled = enabled;
        _animator.speed = speed;
    }
}



