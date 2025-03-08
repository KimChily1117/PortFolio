#include "pch.h"
#include "CameraController.h"

void CameraController::Awake()
{

}

void CameraController::Update()
{
	if (!_target)
	{
		return;
	}

	//// ✅ 카메라는 플레이어를 따라다녀야 하므로 `_targetPos`을 매 프레임 갱신
	//_targetPos = _target->GetTransform()->GetPosition() + _offset;

	//// ✅ `Lerp()`를 사용하여 부드럽게 이동
	//Vec3 currentPos = GetTransform()->GetPosition();
	//Vec3 newPos = Vec3::Lerp(currentPos, _targetPos, _speed * TIME->GetDeltaTime());
	//GetTransform()->SetPosition(newPos);

	// ✅ 카메라는 플레이어를 따라다녀야 하므로 `_targetPos`을 매 프레임 갱신
	_targetPos = _target->GetTransform()->GetPosition();

	// ✅ Y축은 일정한 높이를 유지하여 "위에서 내려다보는" 시점을 유지
	_targetPos.y += 13.5f; // 🔥 카메라의 고정된 높이 (원하는 값으로 조정)

	// ✅ 카메라는 플레이어를 중심으로 바라보도록 오프셋 설정
	Vec3 cameraLookAt = _target->GetTransform()->GetPosition();
	Vec3 cameraPosition = _targetPos + Vec3(0, 0, -10); // 🔥 뒤쪽으로 약간 이동

	// ✅ `Lerp()`를 사용하여 부드럽게 이동
	Vec3 currentPos = GetTransform()->GetPosition();
	Vec3 newPos = Vec3::Lerp(currentPos, cameraPosition, _speed * TIME->GetDeltaTime());

	// ✅ 새로운 카메라 위치 적용
	GetTransform()->SetPosition(newPos);

	// ✅ 카메라가 플레이어를 "바라보도록" 회전 적용
	//GetTransform()->LookAt(cameraLookAt);

}