using UnityEngine;
using Server.Protocol;

public class ActorView2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform _visualRoot;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private SpriteRenderer _shadowRenderer;
    [SerializeField] private TextMesh _hpText;

    public int ActorId { get; private set; }

    private Vector3 _targetPosition;
    private bool _initialized = false;

    private bool _isJumping;
    private bool _prevIsJumping;

    private float _jumpAnimTimer = 0f;
    private const float JumpAnimDuration = 0.85f;
    private const float JumpVisualOffsetY = 2.0f;

    private const float PositionScale = 0.01f;
    private const float FollowSpeed = 20f;

    public void Initialize(int actorId)
    {
        ActorId = actorId;
        name = $"Actor_{actorId}";
    }

    public void Apply(ActorStateData state)
    {
        _targetPosition = new Vector3(
            state.PosX * PositionScale,
            state.PosY * PositionScale,
            0f);

        if (!_initialized)
        {
            transform.position = _targetPosition;
            _initialized = true;
        }

        _isJumping = state.IsJumping;

        if (_hpText != null)
            _hpText.text = $"{state.Hp}/{state.MaxHp}";

        if (_spriteRenderer != null)
        {
            if (state.IsDead)
                _spriteRenderer.color = new Color(0.5f, 0.5f, 0.5f);
            else
                _spriteRenderer.color = Color.white;
        }
    }

    private void Update()
    {
        if (!_initialized)
            return;

        // 루트 이동
        transform.position = Vector3.Lerp(
            transform.position,
            _targetPosition,
            Time.deltaTime * FollowSpeed);

        // 점프 시작 감지 (false -> true)
        if (_isJumping && !_prevIsJumping)
        {
            _jumpAnimTimer = 0f;
        }

        _prevIsJumping = _isJumping;

        // 점프 연출
        if (_visualRoot != null)
        {
            float y = 0f;

            if (_isJumping)
            {
                _jumpAnimTimer += Time.deltaTime;

                float t = Mathf.Clamp01(_jumpAnimTimer / JumpAnimDuration);
                y = Mathf.Sin(t * Mathf.PI) * JumpVisualOffsetY;
            }

            _visualRoot.localPosition = new Vector3(0f, y, 0f);
        }

        // 그림자 연출
        if (_shadowRenderer != null)
        {
            float normalized = 0f;

            if (_isJumping)
                normalized = Mathf.Clamp01(_jumpAnimTimer / JumpAnimDuration);

            float shadowFactor = Mathf.Sin(normalized * Mathf.PI);

            float scale = Mathf.Lerp(1f, 0.65f, shadowFactor);
            _shadowRenderer.transform.localScale = new Vector3(scale, scale, 1f);

            Color c = _shadowRenderer.color;
            c.a = Mathf.Lerp(0.6f, 0.2f, shadowFactor);
            _shadowRenderer.color = c;
        }
    }
}