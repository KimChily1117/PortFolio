#include "pch.h"
#include "OtherPlayerController.h"
#include "GameObject.h"
#include "Transform.h"
#include "Time.h"
#include "ModelAnimator.h"

void OtherPlayerController::Awake()
{
}

void OtherPlayerController::Update()
{
	if (!_hasTargetPosition)
		return;

	Vec3 currentPosition = GetTransform()->GetPosition();
	Vec3 direction = _targetPosition - currentPosition;
	direction.y = 0.0f;

	float distance = direction.Length();

	if (distance < 0.05f)
	{
		_hasTargetPosition = false;
		GetTransform()->SetPosition(Vec3(_targetPosition.x, 1.6f, _targetPosition.z));

		_currentState = PlayerState::IDLE;
		GetGameObject()->GetModelAnimator()->GetTweenDesc().curr.animIndex = (int32)_currentState;
	}
	else
	{
		direction.Normalize();
		Vec3 newPosition = currentPosition + direction * _speed * TIME->GetDeltaTime();
		newPosition.y = 1.6f;

		float angle = atan2f(direction.x, direction.z) + XM_PI;
		GetTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), angle, 0.0f));

		GetTransform()->SetPosition(newPosition);

		_currentState = PlayerState::RUN;
		GetGameObject()->GetModelAnimator()->GetTweenDesc().curr.animIndex = (int32)_currentState;
	}
}

void OtherPlayerController::SetTargetPosition(const Vec3& position)
{
	_targetPosition = position;
	_hasTargetPosition = true;
}

void OtherPlayerController::ProcSkill(int32 skillId)
{
}
