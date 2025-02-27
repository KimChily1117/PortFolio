#include "pch.h"
#include "CameraController.h"

void CameraController::Awake()
{
	//_target = GAMEMANAGER->_myPlayer;
	//_targetPos = GetTransform()->GetPosition();
	//_offset = GetTransform()->GetPosition() - _target->GetTransform()->GetPosition(); // ✅ 초기 오프셋 설정
}

void CameraController::Update()
{
	if (!_target)
	{
		_target = GAMEMANAGER->_myPlayer;
		_offset = GetTransform()->GetPosition() - _target->GetTransform()->GetPosition(); // ✅ 초기 오프셋 설정
		return;
	}

	// ✅ 카메라는 플레이어를 따라다녀야 하므로 `_targetPos`을 매 프레임 갱신
	_targetPos = _target->GetTransform()->GetPosition() + _offset;

	// ✅ `Lerp()`를 사용하여 부드럽게 이동
	Vec3 currentPos = GetTransform()->GetPosition();
	Vec3 newPos = Vec3::Lerp(currentPos, _targetPos, _speed * TIME->GetDeltaTime());
	GetTransform()->SetPosition(newPos);
}