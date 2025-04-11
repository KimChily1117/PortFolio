#include "pch.h"
#include "CursorController.h"

void CursorController::Awake()
{
}

void CursorController::Update()
{
	const float lifeTime = 1.0f; // 이펙트 생명 주기 (초)
	_elapsedTime += DT;

	float t = _elapsedTime / lifeTime;
	t = std::clamp(t, 0.f, 1.f); // 오버슈팅 방지

	// ✅ 1/100 스케일로 보간
	float scale = Lerp(0.01f, 0.0001f, t);
	GetTransform()->SetLocalScale(Vec3(scale));

	if (_elapsedTime >= lifeTime)
	{
		CUR_SCENE->Remove(GetGameObject());
	}
}
