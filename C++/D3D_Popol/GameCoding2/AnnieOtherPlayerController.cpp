#include "pch.h"
#include "AnnieOtherPlayerController.h"
#include "ModelAnimator.h"
#include "ISkill.h"
#include "TimeManager.h"
#include "ClientPacketHandler.h"

void AnnieOtherPlayerController::ProcSkill(int32 skillId)
{
	AlignToTarget();

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
		case SkillType::WSpell:
		{
			_currentState = PlayerState::W;
			Vec3 pos = GetTransform()->GetPosition();

			// 이동 중 방향이 유효하면 그걸 사용, 아니라면 기본 forward
			Vec3 dir = (direction.LengthSquared() > 0.001f) ? direction : GetTransform()->GetLook();
			Vec3 rot = CalculateRotationFromDirection(dir);

			PARTICLE->Play(L"AnnieW", pos, rot);
			break;
		}

		case SkillType::ESpell: _currentState = PlayerState::E; break;
		case SkillType::RSpell: _currentState = PlayerState::R; break;
		}
	}


	auto animator = GetGameObject()->GetModelAnimator();
	if (animator)
	{
		_isAttackMode = true;
		animator->SetAnimation((int32)_currentState, false); // ❌ 루프 없음

		float duration = animator->GetAnimationDuration((int32)_currentState);
		if (duration <= 0.01f)
			duration = 1.0f;

		_timeToIdle = TIME->GetGameTime() + duration;

		DEBUG_LOG("[Client] 🎬 AnnieOtherPlayer Skill Played: " << skillId);
	}
}

void AnnieOtherPlayerController::Awake()
{
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

void AnnieOtherPlayerController::Update()
{
	Super::Update();

	if (_isAttackMode)
	{
		if (TIME->GetGameTime() >= _timeToIdle)
		{
			_isAttackMode = false;
			_currentState = PlayerState::IDLE;
			GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true);

			uint64 casterId = _playerInfo->objectid();
			ClientPacketHandler::g_lastPlayedSkill.erase(casterId);
			DEBUG_LOG("[Client] ✅ AnnieOtherPlayer → IDLE (timeout)");
		}
		return;
	}

	// 이동 처리
	if (_hasTargetPosition)
	{
		if (_currentState != PlayerState::RUN)
		{
			_currentState = PlayerState::RUN;
			GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::RUN, true);
		}
	}
	else if (_currentState == PlayerState::RUN)
	{
		_currentState = PlayerState::IDLE;
		GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true);
	}
}

void AnnieOtherPlayerController::AlignToTarget()
{
	if (_target)
	{
		Vec3 myPosition = GetTransform()->GetPosition();
		Vec3 targetPosition = _target->GetTransform()->GetPosition();
		Vec3 direction = targetPosition - myPosition;
		AlignToDirection(direction);
	}

	//else
	//{
	//	// 타겟이 없으면 현재 바라보는 방향 유지
	//	Vec3 forward = GetTransform()->GetLook();
	//	AlignToDirection(forward); // 혹은 그냥 유지
	//}
}

Vec3 AnnieOtherPlayerController::CalculateRotationFromDirection(const Vec3& dir)
{
	Vec3 forward = dir;
	forward.y = 0.f;
	forward.Normalize();

	float angleY = atan2f(forward.x, forward.z); // 🔥 XM_PI 곱하지 말기
	return Vec3((0.f), XMConvertToRadians(angleY), 0.f); // X=90 유지 (파티클이 위를 향하게)
}