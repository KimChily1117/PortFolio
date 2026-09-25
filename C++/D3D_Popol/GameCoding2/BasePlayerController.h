#pragma once
#include "MonoBehaviour.h"
#include "AnnieSkillTuning.h"


enum class SkillTargetType
{
	SingleTarget,
	Instant,
	Area
};

enum class PlayerState
{
	IDLE,
	RUN,
	ATK1,
	ATK2,
	Q,
	W,
	E,
	R,
	SKILL, // Packet구조 대로 Atk(평)도 SkillId로 관리하는쪽으로 구현
	DIE
	//.....
};

enum class SkillType
{
	GeneralAtk,	
	QSpell,
	WSpell,
	ESpell,
	RSpell
};


struct SkillData
{
	int SkillId;
	SkillTargetType targetType;
	float range;
	float cooldown;
	bool requiresTarget;
};


static unordered_map<Protocol::PLAYER_CHAMPION_TYPE, vector<SkillData>> skillTable =
{
   { Protocol::PLAYER_CHAMPION_TYPE::PLAYER_TYPE_GAREN, {
	   { (int)SkillType::QSpell, SkillTargetType::SingleTarget, 2.0f, 8.0f, true }, // 단일 타겟팅
	   { (int)SkillType::WSpell, SkillTargetType::Instant, 0.0f, 10.0f, false }, // 즉발 방어력 증가 (타겟 없음)
	   { (int)SkillType::ESpell, SkillTargetType::Area, 4.5f, 12.0f, false }, // 범위 스킬 (타겟 없음)
	   { (int)SkillType::RSpell, SkillTargetType::SingleTarget, 5.0f, 100.0f, true } // 단일 타겟팅
   }},

   { Protocol::PLAYER_CHAMPION_TYPE::PLAYER_TYPE_ANNIE, {
	   { (int)SkillType::QSpell, SkillTargetType::SingleTarget, AnnieSkillTuning::QRange, 5.0f, true }, // 단일 타겟팅
	   { (int)SkillType::WSpell, SkillTargetType::Area, AnnieSkillTuning::WRange, 10.0f, false }, // 범위
	   { (int)SkillType::ESpell, SkillTargetType::Instant, 0.0f, 8.0f, false }, // 즉발 방어력 증가
	   { (int)SkillType::RSpell, SkillTargetType::Area, 5.0f, 120.0f, false } // 범위 (티버 소환)
   }}
};

static SkillData GetSkillData(Protocol::PLAYER_CHAMPION_TYPE champ, int skillId)
{

	auto it = skillTable.find(champ);
	if (it != skillTable.end())
	{
		for (const SkillData& skill : it->second)
		{
			if (skill.SkillId == skillId)
				return skill;
		}
	}

	// 기본값 리턴 (잘못된 ID)
	return { 0, SkillTargetType::Instant, 0.0f, 0.0f, false };
}


// ✅ 챔피언의 모든 스킬 반환
static vector<SkillData> GetChampionSkills(Protocol::PLAYER_CHAMPION_TYPE champ)
{
	auto it = skillTable.find(champ);
	if (it != skillTable.end())
		return it->second; // 챔피언 스킬 리스트 반환

	return {}; // 빈 리스트 반환
}



class BasePlayerController : public MonoBehaviour
{
public:
	virtual void Awake() override;
	virtual void Update() override;

	bool ApplyAuthoritativeSnapshot(uint64 serverMoveId, uint64 serverTick, const Vec3& position, Protocol::MOVEMENT_SNAPSHOT_STATE state);
	void SetMinimumAuthoritativeMoveId(uint64 serverMoveId);
	bool IsMovementLocked() const;
    bool IsActionBusy() const;
    void HoldMovementForSkillRequest();
    bool HasAuthoritativeSnapshotStream() const { return _hasAuthoritativeSnapshotStream; }
	uint64 GetLatestAuthoritativeMoveId() const { return _latestAuthoritativeMoveId; }

	virtual void ProcSkill(int32 skillId) = 0;
	virtual void PlayServerSkillResult(int32 skillId, const Vec3& castOrigin, const Vec3& castDirection);  // 모든 플레이어 컨트롤러에서 구현해야 함

protected:
	void AlignToDirection(const Vec3& direction);
	void FinishActionAnimation();
    void BeginActionAnimation();
    void ClearPendingSkillRequest() { _pendingActionDeadline = 0.f; }
    virtual void StopMovementForAction();
	Vec3 _serverSkillCastOrigin = Vec3::Zero;
	Vec3 _serverSkillCastDirection = Vec3::Zero;
	bool IsAuthoritativeMoving() const
	{
		return _hasAuthoritativeSnapshotStream &&
			_authoritativeState == Protocol::MOVEMENT_SNAPSHOT_STATE_MOVING;
	}


	SkillData GetSkillInfo(int32 skillId)
	{
		return GetSkillData(_playerInfo->champtype(), skillId);
	}



public:
	PlayerState _currentState = PlayerState::IDLE;
	int32 _lastAttackAnim = 1;  // ATK1 ↔ ATK2 전환을 위한 변수
	bool _isAttackMode = false;


	float _timeToIdle = 0.1f;  // ✅ 애니메이션 종료 후 IDLE로 변경하는 시간
	shared_ptr<Protocol::ObjectInfo> _playerInfo;
public:
	void SetLastAttackAnim(int32 anim) { _lastAttackAnim = anim; }
	int32 GetLastAttackAnim() { return _lastAttackAnim; }
	
	void SetTarget(shared_ptr<GameObject> t) { _target = t; }

	Vec3 direction;
public:
	shared_ptr<GameObject> _target;

private:
	bool IsFinitePosition(const Vec3& position) const;
	void UpdateAuthoritativeInterpolation();
	void SetMovementAnimation(Protocol::MOVEMENT_SNAPSHOT_STATE state);

	float _pendingActionDeadline = 0.f;
    Protocol::MOVEMENT_SNAPSHOT_STATE _lastSnapshotState = Protocol::MOVEMENT_SNAPSHOT_STATE_UNKNOWN;
    bool _hasAuthoritativeSnapshotStream = false;
	bool _hasAuthoritativeSnapshot = false;
	uint64 _latestAuthoritativeMoveId = 0;
	uint64 _latestAuthoritativeServerTick = 0;
	Vec3 _authoritativeFrom = Vec3(0.f);
	Vec3 _authoritativeTarget = Vec3(0.f);
	float _authoritativeInterpolationElapsed = 0.f;
	float _authoritativeInterpolationDuration = 0.1f;
	Protocol::MOVEMENT_SNAPSHOT_STATE _authoritativeState = Protocol::MOVEMENT_SNAPSHOT_STATE_UNKNOWN;
};
