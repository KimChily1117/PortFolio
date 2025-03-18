#include "pch.h"
#include "AnniePlayerController.h"
#include "ISkill.h"
#include "GeneralAttack.h"
#include "ModelAnimator.h"
#include "TimeManager.h"

void AnniePlayerController::ProcSkill(int32 skillId)
{
	if (_isAttackMode) return; // ✅ 중복 공격 방지

	shared_ptr<ISkill> skill = nullptr;

	// ✅ Debug: 현재 _lastAttackAnim 값 확인
	DEBUG_LOG("[Client] 🔄 Before Attack: _lastAttackAnim = " << _lastAttackAnim);

	if (skillId == (int)SkillType::GeneralAtk)
	{
		skill = make_shared<GeneralAttack>();

		_currentState = (_lastAttackAnim == 1) ? PlayerState::ATK2 : PlayerState::ATK1;
		_lastAttackAnim = (_lastAttackAnim == 1) ? 2 : 1;

		// ✅ Debug: 변경된 _lastAttackAnim 값 확인
		DEBUG_LOG("[Client] 🔄 After Attack: _lastAttackAnim = " << GetLastAttackAnim());
	}
	else
	{
		switch ((SkillType)skillId)
		{
		case SkillType::QSpell: _currentState = PlayerState::Q; break;
		case SkillType::WSpell: _currentState = PlayerState::W; break;
		case SkillType::ESpell: _currentState = PlayerState::E; break;
		case SkillType::RSpell: _currentState = PlayerState::R; break;
		}
	}

	// ✅ 애니메이션 실행
	auto animator = GetGameObject()->GetModelAnimator();
	if (animator)
	{
		_isAttackMode = true;
		animator->SetAnimation((int32)_currentState, false);
		DEBUG_LOG("[Client] 🎬 AnniePlayer Attack Animation " << (int32)_currentState << " Played");

		// ✅ 공격 실행 (GeneralAttack 등)
		if (skill)
		{
			skill->Use(GetGameObject(), _target);
			DEBUG_LOG("[Client] ⚔️ Skill Used: GeneralAttack");
		}

		// ✅ 애니메이션 종료 후 IDLE로 변경
		_timeToIdle = TIME->GetGameTime() + animator->GetAnimationDuration((int32)_currentState);
		animator->SetAnimationEndCallback([this]()
			{

				DEBUG_LOG("[Client] 🔄 AnniePlayer Attack Ended : " << _lastAttackAnim);
				_isAttackMode = false;
				_currentState = PlayerState::IDLE;
				GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true);
				DEBUG_LOG("[Client] 🔄 AnniePlayer Attack Ended → IDLE");
			});
	}
}


void AnniePlayerController::Awake()
{
}

void AnniePlayerController::Update()
{
	Super::Update();

	// ✅ 공격 중이면 다른 동작을 하지 않음
	if (_isAttackMode)
	{
		if (TIME->GetGameTime() >= _timeToIdle)
		{
			_isAttackMode = false;
			_currentState = PlayerState::IDLE;
			GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true); // ✅ IDLE 애니메이션 (루프 O)
			DEBUG_LOG("[Client] 🔄 AnniePlayer Attack Ended → IDLE");
		}
		return;
	}
}

void AnniePlayerController::Start()
{
}

void AnniePlayerController::LateUpdate()
{
}

void AnniePlayerController::FixedUpdate()
{
}

void AnniePlayerController::AlignToTarget()
{
	// ✅ 현재 타겟이 존재하는 경우 방향을 맞춤
	if (_target == nullptr)
		return;

	Vec3 myPosition = GetTransform()->GetPosition();
	Vec3 targetPosition = _target->GetTransform()->GetPosition();
	Vec3 direction = targetPosition - myPosition;
	direction.y = 0.0f; // 🔥 수평 회전만 적용

	if (direction.Length() > 0.01f)
	{
		direction.Normalize();
		float angle = atan2f(direction.x, direction.z) + XM_PI;
		GetTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), angle, 0.0f));
		DEBUG_LOG("[Client] 🔄 AnniePlayer Rotated to Face Target");
	}
}
