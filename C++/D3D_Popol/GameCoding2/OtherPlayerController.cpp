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

	// ✅ 현재 위치
	Vec3 currentPosition = GetTransform()->GetPosition();
	Vec3 direction = _targetPosition - currentPosition;
	direction.y = 0.0f; // 🔥 Y축 차이 제거하여 수평 이동

	float distance = direction.Length();

	// ✅ 목표 위치 근처까지 이동하면 도착 처리
	if (distance < 0.05f)
	{
		_hasTargetPosition = false;
		GetTransform()->SetPosition(Vec3(_targetPosition.x, 1.6f, _targetPosition.z));

		// ✅ 도착 시 애니메이션 변경 (Idle)
		_currentState = OtherPlayerState::IDLE;
		GetGameObject()->GetModelAnimator()->GetTweenDesc().curr.animIndex = (int32)_currentState;
	}
	else
	{
		// ✅ 방향 벡터 정규화
		direction.Normalize();
		// ✅ 이동 (속도 적용)
		Vec3 newPosition = currentPosition + direction * _speed * TIME->GetDeltaTime();
		newPosition.y = 1.6f; // 🔥 Y축 고정

		// ✅ Yaw 회전값 계산 및 적용 (회전 방향)
		float angle = atan2f(direction.x, direction.z) + XM_PI;
		GetTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), angle, 0.0f));

		// ✅ 최종 위치 적용
		GetTransform()->SetPosition(newPosition);

		// ✅ 이동 중 애니메이션 변경 (Run)
		_currentState = OtherPlayerState::RUN;
		GetGameObject()->GetModelAnimator()->GetTweenDesc().curr.animIndex = (int32)_currentState;
	}
}

void OtherPlayerController::SetTargetPosition(const Vec3& position)
{
	_targetPosition = position;
	_hasTargetPosition = true;
}
