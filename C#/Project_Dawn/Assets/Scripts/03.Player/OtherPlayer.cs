using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Character;
using Unity.VisualScripting;
using Google.Protobuf.Protocol;

public class OtherPlayer : BaseCharacter
{
    private static readonly Dictionary<int, int> s_updateLogCounts = new Dictionary<int, int>();
    [SerializeField] private float remoteRunStateGraceTime = 0.15f;
    [SerializeField] private float remoteMoveApplySpeed = 12f;
    [SerializeField] private float remoteInterpolationDelay = 0.1f;
    [SerializeField] private float remoteSnapDistance = 2f;

    private const int MaxRemoteMoveSamples = 8;
    private readonly List<RemoteMoveSample> _remoteMoveSamples = new List<RemoteMoveSample>();
    private bool _hasRemoteTarget;
    private Vector3 _remoteTargetPosition;
    private float _remoteRunStateLockUntil;
    private RemotePlayerLod _lod;
    private float _nextReducedVisualUpdateTime;

    private struct RemoteMoveSample
    {
        public float ReceivedTime;
        public Vector3 Position;
    }

    public override PlayerState _state 
    {
        get
        {
            if (_positionInfo.State == PlayerState.Moving && Time.time < _remoteRunStateLockUntil)
                return PlayerState.Run;

            return PositionInfo.State;
        }
        set 
        {

            // State changes are noisy during remote movement; use PacketHandler/OtherPlayer diagnostics instead when needed.
            if (_positionInfo.State == PlayerState.Moving || _positionInfo.State == PlayerState.Run)
            {
                if (value == PlayerState.Jump)
                {
                    _positionInfo.State = value;
                    ProcJumpPlayer(true);
                    return;
                }
            }
            base._state = value;

            //_updated = true;
        }
    }

    protected override void Start()
    {
        base.Start();
        _lod = GetComponent<RemotePlayerLod>();
    }

    protected override void InitializeStat(Stat stat)
    {
        base.InitializeStat(stat);
        // ?λ퉬???곕씪 ?ш린???ㅽ뀩 媛믪쓣 諛붽퓭以??
    }

   



    #region ProcMethod

    public override void ProcIdlePlayer()
    {
        base.ProcIdlePlayer();

        _animator.SetBool("isWalk", false);
        _animator.SetBool("isRun", false);
        //_HitBox.gameObject.SetActive(false);

    }

    public override void ProcWalkPlayer()
    {
        // Remote players must follow server packet positions, not local dir simulation.
        LogRemoteMovementDiagnostics("WalkProc");
        _animator.SetBool("isRun", false);
        _animator.SetBool("isWalk", true);
        //_HitBox.gameObject.SetActive(false);

        if (_moveDir.x < 0)
        {
            _Sprite.transform.localScale = new Vector3(-1, 1, 1);
            _shadowObject.transform.localScale = new Vector3(-1, 1, 1);
        }
        else if (_moveDir.x > 0)
        {
            _Sprite.transform.localScale = new Vector3(1, 1, 1);
            _shadowObject.transform.localScale = new Vector3(1, 1, 1);
        }
    }
    public override void ProcRunPlayer()
    {
        // Remote players must follow server packet positions, not local dir simulation.
        _animator.SetBool("isWalk", false);
        _animator.SetBool("isRun", true);

        if (_moveDir.x < 0)
        {
            _Sprite.transform.localScale = new Vector3(-1, 1, 1);
            _shadowObject.transform.localScale = new Vector3(-1, 1, 1);
        }
        else if (_moveDir.x > 0)
        {
            _Sprite.transform.localScale = new Vector3(1, 1, 1);
            _shadowObject.transform.localScale = new Vector3(1, 1, 1);
        }

        //_HitBox.gameObject.SetActive(false);
    }
    public override void ProcAtk()
    {
        base.ProcAtk();
        _animator.SetBool("isWalk", false);
        _animator.SetBool("isRun", false);
        _animator.SetBool("isAtkIdle", true);
    }

    public override void ProcJumpPlayer(bool isMoving = false)
    {
        Vector2 networkPosition = new Vector2(_positionInfo.PosX, _positionInfo.PosY);
        transform.position = networkPosition;
        _shadowObject.transform.position = networkPosition;
        initialPosition = networkPosition;

        jumpTimer += Time.deltaTime;

        if (jumpTimer <= jumpDuration)
        {
            isJumping = true;

            float jumpProgress = jumpTimer / jumpDuration;
            float yOffset = Mathf.Sin(jumpProgress * Mathf.PI) * jumpHeight;
            _Sprite.transform.localPosition = _spriteDefaultLocalPosition + new Vector3(0, yOffset, 0);
            _animator.SetBool("isJump", true);
        }
        else
        {
            ResetRemoteJumpVisual();
            _positionInfo.State = PlayerState.Idle;
        }
    }

    private void ResetRemoteJumpVisual()
    {
        if (!isJumping && !_animator.GetBool("isJump"))
            return;

        isJumping = false;
        jumpTimer = 0.0f;
        _Sprite.transform.localPosition = _spriteDefaultLocalPosition;
        _animator.SetBool("isJump", false);
    }

    #endregion


    protected override void CheckUpdatedFlag()
    {

    }



    public void ApplyRemoteMove(PositionInfo posInfo)
    {
        if (posInfo == null)
            return;

        PositionInfo = posInfo;
        _remoteTargetPosition = new Vector3(posInfo.PosX, posInfo.PosY, transform.position.z);
        _hasRemoteTarget = true;

        _remoteMoveSamples.Add(new RemoteMoveSample
        {
            ReceivedTime = Time.time,
            Position = _remoteTargetPosition
        });

        while (_remoteMoveSamples.Count > MaxRemoteMoveSamples)
            _remoteMoveSamples.RemoveAt(0);
    }
    public void UseSkill(int skillId)
    {  
        if (skillId == 2) //??
        {
            Debug.Log($"Use Skill!!! 2");
            PositionInfo.State = PlayerState.Atk;
            _animator.SetTrigger($"Attack1");

        }
        if (skillId == 3) //??
        {
            Debug.Log($"Use Skill!!! 3");
            PositionInfo.State = PlayerState.Atk;
            _animator.SetTrigger($"Attack2");


        }
        if (skillId == 4) //??
        {
            Debug.Log($"Use Skill!!! 4");
            PositionInfo.State = PlayerState.Atk;

            _animator.SetTrigger($"Attack3");
        }

    }
    public override void TakeDamage(float Damage = 0)
    {
        base.TakeDamage(Damage);

        //if (HP <= 0)
        //{
        //    _animator.SetTrigger("Die");
        //    _shadowObject.SetActive(false);
        //    GameManager.Sound.Play("Effect/Swordman/sm_die");
        //}
    }

    public override void OnDead()
    {
        _animator.SetTrigger("Die");
        _shadowObject.SetActive(false);
        GameManager.Sound.Play("Effect/Swordman/sm_die");
        base.OnDead();
    }



    private void RefreshRemoteRunStateLock()
    {
        if (_positionInfo.State == PlayerState.Run)
        {
            _remoteRunStateLockUntil = Time.time + Mathf.Max(0f, remoteRunStateGraceTime);
        }
        else if (_positionInfo.State != PlayerState.Moving)
        {
            _remoteRunStateLockUntil = 0f;
        }
    }

    protected override void Update()
    {
        RefreshRemoteRunStateLock();

        if (_lod != null && _lod.SkipExpensiveVisualUpdate)
        {
            ApplyHiddenRemotePosition();
            return;
        }

        if (_lod != null && _lod.UseReducedVisualUpdate && Time.time < _nextReducedVisualUpdateTime)
        {
            ApplyRemoteRenderPosition();
            return;
        }

        if (_lod != null && _lod.UseReducedVisualUpdate)
            _nextReducedVisualUpdateTime = Time.time + RemotePlayerLod.MidVisualUpdateInterval;

        if (_positionInfo.State != PlayerState.Jump)
            ResetRemoteJumpVisual();

        Vector3 beforeBaseUpdate = transform.position;
        Vector2 beforeCell = CellPos;

        base.Update();

        Vector3 afterBaseUpdate = transform.position;
        ApplyRemoteRenderPosition();

        LogRemoteMovementDiagnostics("Update", beforeBaseUpdate, afterBaseUpdate, transform.position, beforeCell, CellPos);
    }

    private void ApplyHiddenRemotePosition()
    {
        if (!_hasRemoteTarget)
            return;

        transform.position = _remoteTargetPosition;
        _positionInfo.PosX = _remoteTargetPosition.x;
        _positionInfo.PosY = _remoteTargetPosition.y;
    }

    private void ApplyRemoteRenderPosition()
    {
        if (!_hasRemoteTarget)
            return;

        Vector3 renderPosition = GetRemoteRenderPosition();
        transform.position = renderPosition;
        _positionInfo.PosX = renderPosition.x;
        _positionInfo.PosY = renderPosition.y;
    }

    private Vector3 GetRemoteRenderPosition()
    {
        if (_positionInfo.State == PlayerState.Idle || _remoteMoveSamples.Count == 0)
            return _remoteTargetPosition;

        if (Vector3.Distance(transform.position, _remoteTargetPosition) >= remoteSnapDistance)
            return _remoteTargetPosition;

        if (_remoteMoveSamples.Count == 1)
            return Vector3.MoveTowards(transform.position, _remoteTargetPosition, remoteMoveApplySpeed * Time.deltaTime);

        float renderTime = Time.time - Mathf.Max(0f, remoteInterpolationDelay);

        for (int i = 0; i < _remoteMoveSamples.Count - 1; i++)
        {
            RemoteMoveSample from = _remoteMoveSamples[i];
            RemoteMoveSample to = _remoteMoveSamples[i + 1];
            if (renderTime < from.ReceivedTime || renderTime > to.ReceivedTime)
                continue;

            float duration = Mathf.Max(0.0001f, to.ReceivedTime - from.ReceivedTime);
            float t = Mathf.Clamp01((renderTime - from.ReceivedTime) / duration);
            return Vector3.Lerp(from.Position, to.Position, t);
        }

        return Vector3.MoveTowards(transform.position, _remoteTargetPosition, remoteMoveApplySpeed * Time.deltaTime);
    }

    private void LogRemoteMovementDiagnostics(string phase)
    {
        LogRemoteMovementDiagnostics(phase, transform.position, transform.position, transform.position, CellPos, CellPos);
    }

    private void LogRemoteMovementDiagnostics(string phase, Vector3 beforeTransform, Vector3 afterBaseTransform, Vector3 finalTransform, Vector2 beforeCell, Vector2 afterCell)
    {
        if (string.IsNullOrEmpty(name) || name.StartsWith("PD_Dummy", StringComparison.Ordinal) == false)
            return;

        int count = 0;
        s_updateLogCounts.TryGetValue(Id, out count);
        count++;
        s_updateLogCounts[Id] = count;

        if (count > 20 && count % 30 != 0)
            return;

        Debug.Log($"[CLIENT][OTHER_MOVE] Phase={phase}, Id={Id}, Name={name}, State={_positionInfo.State}, Dir={_positionInfo.MoveDir}, BeforeTransform=({beforeTransform.x:0.00},{beforeTransform.y:0.00},{beforeTransform.z:0.00}), AfterBase=({afterBaseTransform.x:0.00},{afterBaseTransform.y:0.00},{afterBaseTransform.z:0.00}), Final=({finalTransform.x:0.00},{finalTransform.y:0.00},{finalTransform.z:0.00}), BeforeCell=({beforeCell.x:0.00},{beforeCell.y:0.00}), AfterCell=({afterCell.x:0.00},{afterCell.y:0.00})");
    }


}








