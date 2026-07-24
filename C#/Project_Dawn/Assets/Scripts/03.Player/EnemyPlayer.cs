using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Character;
using Google.Protobuf.Protocol;
public class EnemyPlayer : BaseCharacter
{
    private const string HitImpactEffectPath = SpriteHitEffectPlayer.DefaultSpritePath;
    UI_BakalSceneUI _bakalSceneUI;

    [SerializeField]
    Transform[] MeteorAreas;

    public float animTime = 0f;
    private Coroutine _hitFeedbackCoroutine;
    private float _lastHitFeedbackTime;
    private bool _hasRemoteTarget;
    private Vector3 _remoteTargetPosition;
    private SpriteRenderer _spriteRenderer;
    private SpriteRenderer _shadowRenderer;
    private Vector3 _shadowDefaultLocalScale = Vector3.one;
    private Vector3 _shadowDefaultLocalPosition;
    private bool _isFacingLeft;
    [SerializeField] private float remoteMoveApplySpeed = 8f;
    [SerializeField] private float remoteSnapDistance = 6f;
    [SerializeField] private float remoteArriveDistance = 0.02f;
    protected override void Start()
    {
        base.Start();
        MaxHP = 1200f;
        HP = MaxHP;
        ResolveEnemyVisualReferences();

        LogEnemyVisualAnchor("Start");

        Debug.Log($"Enemy Start");


        _bakalSceneUI = GameManager.UI._scene as UI_BakalSceneUI;

        if (_bakalSceneUI == null)
            return;
        _bakalSceneUI.targetChar = this;

        MeteorAreas = new Transform[6];

        GameObject go = GameObject.Find("Map");

        if (go == null)
            return;

        for (int i = 0; i < MeteorAreas.Length; i++)
        {
            MeteorAreas[i] = go.FindChild<Transform>($"Meteor_Area_{i + 1}");
        }

        foreach (Transform t in MeteorAreas)
        {
            t.gameObject.SetActive(false);
        }

    }

    protected override void Update()
    {
        base.Update();
        ApplyRemoteRenderPosition();
        if (_animator.GetCurrentAnimatorStateInfo(0).IsName("Bakal2p_Skill") == true)
        {
            animTime = _animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        }
    }

    public void ApplyRemoteMove(PositionInfo posInfo)
    {
        if (posInfo == null)
            return;

        PositionInfo = posInfo;
        ApplyFacingFromMoveDir(posInfo.MoveDir);
        _remoteTargetPosition = new Vector3(posInfo.PosX, posInfo.PosY, transform.position.z);
        _hasRemoteTarget = true;
    }

    public override void ProcIdlePlayer()
    {
        SetWalkAnimation(false);
        ApplyFacingFromMoveDir(PositionInfo != null ? PositionInfo.MoveDir : MoveDir.None);
    }

    public override void ProcWalkPlayer()
    {
        SetWalkAnimation(true);
        ApplyFacingFromMoveDir(PositionInfo != null ? PositionInfo.MoveDir : MoveDir.None);
    }

    public override void ProcRunPlayer()
    {
        SetWalkAnimation(true);
        ApplyFacingFromMoveDir(PositionInfo != null ? PositionInfo.MoveDir : MoveDir.None);
    }

    public override void ProcAtk()
    {
        SetWalkAnimation(false);
        ApplyFacingFromMoveDir(PositionInfo != null ? PositionInfo.MoveDir : MoveDir.None);
    }

    private void SetWalkAnimation(bool isWalk)
    {
        if (_animator == null)
            return;

        _animator.SetBool("isWalk", isWalk);
    }

    private void ApplyFacingFromMoveDir(MoveDir moveDir)
    {
        MoveDir facing = moveDir;
        if (facing != MoveDir.Left && facing != MoveDir.Right)
            facing = _lastHorizontalDir;

        if (facing == MoveDir.Left)
            SetFacingFlip(true);
        else if (facing == MoveDir.Right)
            SetFacingFlip(false);
    }

    private void ResolveEnemyVisualReferences()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            bool isShadow = (_shadowObject != null && renderer.gameObject == _shadowObject) ||
                            renderer.gameObject.name.IndexOf("Shadow", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isShadow)
            {
                if (_shadowRenderer == null)
                    _shadowRenderer = renderer;
                continue;
            }

            if (_spriteRenderer == null)
            {
                _spriteRenderer = renderer;
                _Sprite = renderer.gameObject;
            }
        }

        if (_shadowRenderer == null && _shadowObject != null)
            _shadowRenderer = _shadowObject.GetComponent<SpriteRenderer>();

        Transform shadowTransform = GetShadowTransform();
        if (shadowTransform != null)
        {
            _shadowDefaultLocalScale = shadowTransform.localScale;
            _shadowDefaultLocalScale.x = Mathf.Abs(_shadowDefaultLocalScale.x);
            _shadowDefaultLocalPosition = shadowTransform.localPosition;
            _shadowDefaultLocalPosition.x = Mathf.Abs(_shadowDefaultLocalPosition.x);
        }
    }

    private void SetFacingFlip(bool facingLeft)
    {
        bool changed = _isFacingLeft != facingLeft;
        _isFacingLeft = facingLeft;

        if (_spriteRenderer != null)
            _spriteRenderer.flipX = facingLeft;

        ApplyShadowFacingScale(facingLeft);

        if (transform.localScale.x < 0f)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        if (changed)
            LogEnemyVisualAnchor("FacingChanged");
    }


    private void ApplyShadowFacingScale(bool facingLeft)
    {
        if (_shadowRenderer != null)
            _shadowRenderer.flipX = false;

        Transform shadowTransform = GetShadowTransform();
        if (shadowTransform == null)
            return;

        float sign = facingLeft ? -1f : 1f;
        Vector3 scale = _shadowDefaultLocalScale;
        scale.x = Mathf.Abs(scale.x) * sign;
        shadowTransform.localScale = scale;

        Vector3 localPosition = _shadowDefaultLocalPosition;
        localPosition.x = Mathf.Abs(localPosition.x) * sign;
        shadowTransform.localPosition = localPosition;
    }

    private Transform GetShadowTransform()
    {
        if (_shadowObject != null)
            return _shadowObject.transform;

        return _shadowRenderer != null ? _shadowRenderer.transform : null;
    }
    private void LogEnemyVisualAnchor(string phase)
    {
        Vector3 root = transform.position;
        string shadow = _shadowObject != null ? FormatVector2(_shadowObject.transform.position) : "null";
        string sprite = _Sprite != null ? _Sprite.name : "null";
        string spriteBounds = "null";
        SpriteRenderer spriteRenderer = _spriteRenderer != null ? _spriteRenderer : (_Sprite != null ? _Sprite.GetComponent<SpriteRenderer>() : null);
        if (spriteRenderer != null)
            spriteBounds = FormatBounds(spriteRenderer.bounds);

        Transform shadowTransform = GetShadowTransform();
        Vector3 shadowScale = shadowTransform != null ? shadowTransform.localScale : Vector3.one;
        Vector3 shadowLocalPosition = shadowTransform != null ? shadowTransform.localPosition : Vector3.zero;
        Debug.Log($"[CLIENT][ENEMY_VISUAL_ANCHOR] Phase={phase}, Name={ObjInfo?.Name ?? name}, Root={FormatVector2(root)}, Shadow={shadow}, Sprite={sprite}, SpriteBounds={spriteBounds}, RootScale={transform.localScale}, SpriteFlipX={(_spriteRenderer != null && _spriteRenderer.flipX)}, ShadowFlipX={(_shadowRenderer != null && _shadowRenderer.flipX)}, ShadowScale={shadowScale}, ShadowLocalPos={shadowLocalPosition}");
    }

    private string FormatVector2(Vector3 value)
    {
        return $"({value.x:0.00},{value.y:0.00})";
    }

    private string FormatBounds(Bounds bounds)
    {
        return $"Center={FormatVector2(bounds.center)}, Min={FormatVector2(bounds.min)}, Max={FormatVector2(bounds.max)}";
    }

    private void ApplyRemoteRenderPosition()
    {
        if (!_hasRemoteTarget)
            return;

        float distance = Vector3.Distance(transform.position, _remoteTargetPosition);
        if (distance >= remoteSnapDistance)
        {
            Debug.Log($"[CLIENT][ENEMY_MOVE_SNAP] Name={ObjInfo?.Name ?? name}, Distance={distance:0.00}, From={FormatVector2(transform.position)}, To={FormatVector2(_remoteTargetPosition)}, State={PositionInfo?.State}, Dir={PositionInfo?.MoveDir}");
            transform.position = _remoteTargetPosition;
            return;
        }

        if (distance <= remoteArriveDistance)
        {
            transform.position = _remoteTargetPosition;
            return;
        }

        float maxDistanceDelta = remoteMoveApplySpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, _remoteTargetPosition, maxDistanceDelta);
    }
    public override void TakeDamage(float Damage = 0)
    {
        SetWalkAnimation(false);
        base.TakeDamage(Damage);
        PlayHitFeedback();
        Invoke("triggerOn", 0.2f);
    }

    public void PlayHitFeedback(MoveDir impactDir = MoveDir.None)
    {
        if (Time.time - _lastHitFeedbackTime < 0.08f)
            return;

        _lastHitFeedbackTime = Time.time;

        if (_hitFeedbackCoroutine != null)
            StopCoroutine(_hitFeedbackCoroutine);

        _hitFeedbackCoroutine = StartCoroutine(HitFeedbackRoutine());

        if (CameraShake.Instance != null)
            StartCoroutine(CameraShake.Instance.Shake(0.08f, 0.08f));

        HitStopManager.Instance.Play(0.035f, 0.08f);
        PlayHitImpactEffect(impactDir);
    }


    private void PlayHitImpactEffect(MoveDir impactDir)
    {
        SpriteRenderer baseRenderer = _Sprite != null ? _Sprite.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        Vector3 position;
        if (baseRenderer != null)
        {
            position = baseRenderer.bounds.center;
            position.y += baseRenderer.bounds.extents.y * 0.05f;
        }
        else
        {
            position = _shadowObject != null ? _shadowObject.transform.position : transform.position;
            position.y += 1.2f;
        }

        string sortingLayerName = baseRenderer != null ? baseRenderer.sortingLayerName : "Default";
        int sortingOrder = baseRenderer != null ? baseRenderer.sortingOrder + 5 : 10;
        bool flipX = impactDir == MoveDir.Left;

        SpriteHitEffectPlayer.PlayAt(position, flipX, sortingLayerName, sortingOrder, HitImpactEffectPath, 36f, 1.15f, Vector3.zero);
    }
    private IEnumerator HitFeedbackRoutine()
    {
        SpriteRenderer spriteRenderer = _Sprite != null ? _Sprite.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
            yield break;

        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(0.04f);
        spriteRenderer.color = new Color(1f, 0.25f, 0.25f, 1f);
        yield return new WaitForSeconds(0.06f);
        spriteRenderer.color = originalColor;
        _hitFeedbackCoroutine = null;
    }


    public override void OnDead()
    {
        SetWalkAnimation(false);
        _animator.SetTrigger("DeadTrigger");
        GameManager.Sound.Play($"Sounds/mon/bakal/bakal_dragon_skill_20_2");

        base.OnDead();

    }



    private void triggerOn()
    {
        _bakalSceneUI.isDecrease = true;
    }


    public void UseSkill(int skillId)
    {
        if (skillId == 4)
        {
            SetWalkAnimation(false);
            Debug.Log($"BAKAL SKILL EXPLOSION!!!!");
            StartCoroutine(MeteorPattern());
        }

        if (skillId == 5)
        {
            SetWalkAnimation(false);
            Debug.Log($"BAKAL MELEE ATTACK!!!!");
            // Bakal melee attack animation hook. Add an Animator trigger with this name when the sprite is ready.
            _animator.SetTrigger("MeleeAttack");
        }

        //else if
        //{

        //}
    }




    #region 스킬 연출

    IEnumerator MeteorPattern()
    {
        SetWalkAnimation(false);
        _animator.SetTrigger("SkillTrigger");

        animTime = 0f;

        GameManager.Sound.Play($"Sounds/mon/bakal/bakal_dragon_skill_01_1");

        yield return new WaitUntil(() => animTime >= 0.2f);
        GameManager.Sound.Play($"Sounds/mon/bakal/bakal_dragon_fire_stomp_exp_02");
        StartCoroutine(CameraShake.Instance.Shake(0.3f, 0.4f));
        MeteorAreas[0].gameObject.SetActive(true);
        MeteorAreas[5].gameObject.SetActive(true);


        //foreach (Transform t in MeteorAreas)
        //{
        //    t.gameObject.SetActive(true);
        //}

        yield return new WaitUntil(() => animTime >= 0.59f);
        GameManager.Sound.Play($"Sounds/mon/bakal/bakal_dragon_fire_stomp_exp_02");

        StartCoroutine(CameraShake.Instance.Shake(0.3f, 0.4f));

        MeteorAreas[1].gameObject.SetActive(true);
        MeteorAreas[4].gameObject.SetActive(true);

        yield return new WaitUntil(() => animTime >= 0.95f);
        GameManager.Sound.Play($"Sounds/mon/bakal/bakal_dragon_fire_stomp_exp_03");


        StartCoroutine(CameraShake.Instance.Shake(0.3f, 0.4f));
        MeteorAreas[2].gameObject.SetActive(true);
        MeteorAreas[3].gameObject.SetActive(true);

        yield return new WaitForSeconds(10f);

        foreach (Transform t in MeteorAreas)
        {
            t.gameObject.SetActive(false);
        }
    }
    #endregion

}






























