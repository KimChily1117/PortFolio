#include "pch.h"
#include "AnnieOtherPlayerController.h"
#include "ModelAnimator.h"
#include "ISkill.h"
#include "TimeManager.h"

void AnnieOtherPlayerController::ProcSkill(int32 skillId)
{
	if (skillId == (int)SkillType::GeneralAtk)
	{
		_currentState = (_lastAttackAnim == 1) ? PlayerState::ATK2 : PlayerState::ATK1;
		_lastAttackAnim = (_lastAttackAnim == 1) ? 2 : 1;
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

	AlignToTarget();

	auto animator = GetGameObject()->GetModelAnimator();
	if (animator)
	{
		_isAttackMode = true;
		animator->SetAnimation((int32)_currentState, false); // ✅ 공격 애니메이션 (루프 X)
		DEBUG_LOG("[Client] 🎬 Attack Animation Started: " << (int32)_currentState);

		float animationDuration = animator->GetAnimationDuration((int32)_currentState);
		_timeToIdle = TIME->GetGameTime() + animationDuration;

		// ✅ 공격 애니메이션 종료 후 IDLE로 전환
		animator->SetAnimationEndCallback([this]()
			{
				_isAttackMode = false;
				_currentState = PlayerState::IDLE;
				GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true); // ✅ IDLE 애니메이션 (루프 O)
				DEBUG_LOG("[Client] 🔄 AnnieOtherPlayer Attack Ended → IDLE");
			});
	}
}


void AnnieOtherPlayerController::Awake()
{
}

void AnnieOtherPlayerController::Update()
{
	Super::Update();

	// ✅ 공격 중에는 다른 동작을 하지 않음
	if (_isAttackMode)
	{
		if (TIME->GetGameTime() >= _timeToIdle)
		{
			_isAttackMode = false;
			_currentState = PlayerState::IDLE;
			GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true); // ✅ IDLE 애니메이션 (루프 O)
			DEBUG_LOG("[Client] 🔄 AnnieOtherPlayer Attack Ended → IDLE");
		}
		return;
	}

	// ✅ 이동 애니메이션 (루프 O)
	if (_hasTargetPosition)
	{
		if (_currentState != PlayerState::RUN)
		{
			_currentState = PlayerState::RUN;
			GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::RUN, true); // ✅ 이동 애니메이션 (루프 O)
			DEBUG_LOG("[Client] 🚶 AnnieOtherPlayer Running Animation");
		}
	}
	// ✅ 이동이 멈춘 경우 IDLE로 변경 (루프 O)
	else if (_currentState == PlayerState::RUN)
	{
		_currentState = PlayerState::IDLE;
		GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true); // ✅ IDLE 애니메이션 (루프 O)
		DEBUG_LOG("[Client] 🔄 AnnieOtherPlayer Stopped Moving → IDLE");
	}
}

void AnnieOtherPlayerController::Start()
{
}

void AnnieOtherPlayerController::LateUpdate()
{
}

void AnnieOtherPlayerController::FixedUpdate()
{
}

void AnnieOtherPlayerController::AlignToTarget()
{
	// ✅ 타겟이 존재하는 경우 방향을 맞춤
	if (!_target) return;

	Vec3 myPosition = GetTransform()->GetPosition();
	Vec3 targetPosition = _target->GetTransform()->GetPosition();
	Vec3 direction = targetPosition - myPosition;
	direction.y = 0.0f; // 🔥 수평 회전만 적용

	if (direction.Length() > 0.01f)
	{
		direction.Normalize();
		float angle = atan2f(direction.x, direction.z) + XM_PI;
		GetTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), angle, 0.0f));
		DEBUG_LOG("[Client] 🔄 AnnieOtherPlayer Rotated to Face Target");
	}
}
