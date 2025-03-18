#pragma once
#include "MonoBehaviour.h"

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

class BasePlayerController : public MonoBehaviour
{
public:
	virtual void Awake() override;
	virtual void Update() override;

	virtual void ProcSkill(int32 skillId) = 0;  // 모든 플레이어 컨트롤러에서 구현해야 함

public:
	PlayerState _currentState = PlayerState::IDLE;
	int32 _lastAttackAnim = 1;  // ATK1 ↔ ATK2 전환을 위한 변수
	bool _isAttackMode = false;
	float _timeToIdle = 0.1f;  // ✅ 애니메이션 종료 후 IDLE로 변경하는 시간
public:
	void SetLastAttackAnim(int32 anim) { _lastAttackAnim = anim; }
	int32 GetLastAttackAnim() { return _lastAttackAnim; }
protected:
	shared_ptr<GameObject> _target;
};
