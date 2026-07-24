using Character;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Google.Protobuf.Protocol;


public class CombatSystem : MonoBehaviour
{
    private const float LineTolerance = 0.4f;
    private const float HorizontalRange = 1.8f;
    private const float AttackHitboxHeight = 2.2f;
    private const bool DebugCombatAnchorLog = true;

    public bool canatteck = false;
    public Collider2D inLineCollider;
    public LayerMask enemyLayer;
    public MyPlayer _player;


    private ContactFilter2D contactFilter;
    public List<Collider2D> cols = new List<Collider2D>();
    private readonly HashSet<int> _hitTargetsThisAttack = new HashSet<int>();
    private bool _wasInAttackState;
    private int _currentSkillId;
    private int _currentComboIndex;
    private int _attackSequence;

    public int CurrentSkillId => _currentSkillId;
    public int CurrentComboIndex => _currentComboIndex;
    public int AttackSequence => _attackSequence;

    public void Init(MyPlayer player)
    {
        _player = player;

        enemyLayer = 1 << LayerMask.NameToLayer("Enemy");
        contactFilter.SetLayerMask(enemyLayer);
        contactFilter.useTriggers = true;
    }

    public void OnUpdate()
    {
        if (_player == null)
            return;

        if (_player._state != PlayerState.Atk && _wasInAttackState)
            ClearCurrentAttack();
    }

    public void BeginBasicAttack(int skillId, int comboIndex)
    {
        _currentSkillId = skillId;
        _currentComboIndex = comboIndex;
        _attackSequence++;
        _hitTargetsThisAttack.Clear();
        _wasInAttackState = true;
    }

    public void ClearCurrentAttack()
    {
        _currentSkillId = 0;
        _currentComboIndex = 0;
        _hitTargetsThisAttack.Clear();
        _wasInAttackState = false;
    }

    public void TrySendBasicAttackHitFrameCandidates()
    {
        if (_player == null)
            return;

        MoveDir facing = GetStableHorizontalFacing();
        Vector2 attackerPos = GetCombatPosition(_player);
        Debug.Log($"[CLIENT][ATTACK_HIT_FRAME] SkillId={_currentSkillId}, ComboIndex={_currentComboIndex}, State={_player._state}, Facing={facing}, Pos=({attackerPos.x:0.00},{attackerPos.y:0.00})");

        if (_currentSkillId <= 0)
        {
            Debug.Log("[CLIENT][ATTACK_HIT_FRAME_IGNORED] Reason=NoCurrentSkill");
            return;
        }

        if (_player._state != PlayerState.Atk)
        {
            Debug.Log($"[CLIENT][ATTACK_HIT_FRAME_IGNORED] Reason=NotAtkState, State={_player._state}");
            return;
        }

        if (facing != MoveDir.Left && facing != MoveDir.Right)
        {
            Debug.LogWarning($"[CLIENT][HIT_CANDIDATE_SKIP] Reason=InvalidFacing, Facing={facing}");
            return;
        }

        cols.Clear();
        Vector2 hitboxCenter = GetAttackHitboxCenter(attackerPos, facing);
        Vector2 hitboxSize = new Vector2(HorizontalRange, AttackHitboxHeight);
        Collider2D[] hits = Physics2D.OverlapBoxAll(hitboxCenter, hitboxSize, 0f, enemyLayer);
        Debug.Log($"[CLIENT][ATTACK_HITBOX] Center=({hitboxCenter.x:0.00},{hitboxCenter.y:0.00}), Size=({hitboxSize.x:0.00},{hitboxSize.y:0.00}), Facing={facing}, Hits={hits.Length}");

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null)
                continue;

            BaseCharacter baseCharacter = col.GetComponent<BaseCharacter>();
            if (baseCharacter == null)
                baseCharacter = col.GetComponentInParent<BaseCharacter>();

            if (baseCharacter == null)
                continue;

            TrySendHitCandidate(baseCharacter);
        }
    }

    private void TrySendHitCandidate(BaseCharacter collisionChar)
    {
        if (collisionChar == null)
        {
            Debug.Log("[CLIENT][HIT_CANDIDATE_SKIP] Reason=TargetNull");
            return;
        }

        if (collisionChar.ObjInfo == null)
        {
            Debug.Log($"[CLIENT][HIT_CANDIDATE_SKIP] Reason=TargetObjInfoNull, Target={collisionChar.name}");
            return;
        }

        int targetId = collisionChar.ObjInfo.ObjectId;
        if (_hitTargetsThisAttack.Contains(targetId))
        {
            Debug.Log($"[CLIENT][HIT_CANDIDATE_SKIP] Reason=DuplicateLocal, Target={collisionChar.ObjInfo.Name}, TargetId={targetId}");
            return;
        }

        LogCombatAnchor("[CLIENT][COMBAT_ANCHOR]", _player);
        LogCombatAnchor("[CLIENT][ENEMY_ANCHOR]", collisionChar);

        Vector2 attackerPos = GetCombatPosition(_player);
        Vector2 targetPos = GetCombatPosition(collisionChar);
        MoveDir facing = GetStableHorizontalFacing();
        float dx = targetPos.x - attackerPos.x;
        float dy = Mathf.Abs(targetPos.y - attackerPos.y);

        bool localLineMismatch = dy > LineTolerance;
        bool localBehind = IsInFront(dx, facing) == false;
        bool localOutOfRange = Mathf.Abs(dx) > HorizontalRange;
        if (localLineMismatch || localBehind || localOutOfRange)
        {
            Debug.Log($"[CLIENT][HIT_CANDIDATE_LOCAL_DIAG] Target={collisionChar.ObjInfo.Name}, Facing={facing}, Dx={dx:0.00}, Dy={dy:0.00}, LocalLineMismatch={localLineMismatch}, LocalBehind={localBehind}, LocalOutOfRange={localOutOfRange}. SendingToServer=True");
        }

        SendHitCandidate(collisionChar, facing, dx, dy);
        _hitTargetsThisAttack.Add(targetId);
    }

    private void SendHitCandidate(BaseCharacter collisionChar, MoveDir facing, float dx, float dy)
    {
        C_Collision c_Collision = new C_Collision();
        c_Collision.Playerinfo = collisionChar.ObjInfo.Clone();
        c_Collision.Playerinfo.SkillInfo = new SkillInfo { SkillId = _currentSkillId };

        Debug.Log($"[CLIENT][HIT_CANDIDATE_SEND] SkillId={_currentSkillId}, ComboIndex={_currentComboIndex}, AttackSeq={_attackSequence}, Target={collisionChar.ObjInfo.Name}, TargetId={collisionChar.ObjInfo.ObjectId}, Facing={facing}, Dx={dx:0.00}, Dy={dy:0.00}");
        GameManager.Network.Send(c_Collision);

        // 데미지/HP/콤보 반영은 서버 검증 후 S_Collision에서만 처리한다.
    }

    private void LogCombatAnchor(string tag, BaseCharacter character)
    {
        if (DebugCombatAnchorLog == false || character == null)
            return;

        Vector3 root = character.transform.position;
        Vector2 positionInfo = character.PositionInfo != null
            ? new Vector2(character.PositionInfo.PosX, character.PositionInfo.PosY)
            : Vector2.zero;

        string shadowPosition = "null";
        string shadowCollider = "null";
        if (character._shadowObject != null)
        {
            Vector3 shadow = character._shadowObject.transform.position;
            shadowPosition = FormatVector2(shadow);
            Collider2D shadowCol = character._shadowObject.GetComponent<Collider2D>();
            shadowCollider = FormatBounds(shadowCol);
        }

        Collider2D rootCollider = character.GetComponent<Collider2D>();
        SpriteRenderer spriteRenderer = character._Sprite != null ? character._Sprite.GetComponent<SpriteRenderer>() : character.GetComponentInChildren<SpriteRenderer>();

        Debug.Log($"{tag} Name={character.ObjInfo?.Name}, ObjectId={character.Id}, Root={FormatVector2(root)}, Shadow={shadowPosition}, RootCollider={FormatBounds(rootCollider)}, ShadowCollider={shadowCollider}, SpriteBounds={FormatSpriteBounds(spriteRenderer)}, PositionInfo={FormatVector2(positionInfo)}");
    }

    private string FormatVector2(Vector2 value)
    {
        return $"({value.x:0.00},{value.y:0.00})";
    }

    private string FormatBounds(Collider2D col)
    {
        if (col == null)
            return "null";

        Bounds b = col.bounds;
        return $"Center={FormatVector2(b.center)}, Min={FormatVector2(b.min)}, Max={FormatVector2(b.max)}";
    }

    private string FormatSpriteBounds(SpriteRenderer renderer)
    {
        if (renderer == null)
            return "null";

        Bounds b = renderer.bounds;
        return $"Center={FormatVector2(b.center)}, Min={FormatVector2(b.min)}, Max={FormatVector2(b.max)}";
    }
    private Vector2 GetCombatPosition(BaseCharacter character)
    {
        if (character == null)
            return Vector2.zero;

        if (character._shadowObject != null)
            return character._shadowObject.transform.position;

        return character.transform.position;
    }

    private MoveDir GetStableHorizontalFacing()
    {
        MoveDir dir = _player.PositionInfo != null ? _player.PositionInfo.MoveDir : MoveDir.None;
        if (dir == MoveDir.Left || dir == MoveDir.Right)
            return dir;

        if (_player._lastHorizontalDir == MoveDir.Left || _player._lastHorizontalDir == MoveDir.Right)
            return _player._lastHorizontalDir;

        if (_player._lastDir == MoveDir.Left || _player._lastDir == MoveDir.Right)
            return _player._lastDir;

        return MoveDir.None;
    }

    private Vector2 GetAttackHitboxCenter(Vector2 attackerPos, MoveDir facing)
    {
        float sign = facing == MoveDir.Left ? -1f : 1f;
        return new Vector2(attackerPos.x + sign * HorizontalRange * 0.5f, attackerPos.y);
    }
    private bool IsInFront(float dx, MoveDir facing)
    {
        if (facing == MoveDir.Right)
            return dx >= 0f;

        if (facing == MoveDir.Left)
            return dx <= 0f;

        return false;
    }
}





