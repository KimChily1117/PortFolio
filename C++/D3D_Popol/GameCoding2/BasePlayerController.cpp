#include "pch.h"
#include "BasePlayerController.h"

void BasePlayerController::Awake()
{
}

void BasePlayerController::Update()
{
}

void BasePlayerController::AlignToDirection(const Vec3& direction)
{
	if (direction.Length() > 0.01f)
	{
		Vec3 normalizedDir = direction;
		normalizedDir.y = 0.0f;
		normalizedDir.Normalize();

		float angle = atan2f(normalizedDir.x, normalizedDir.z) + XM_PI;
		GetTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), angle, 0.0f));
		DEBUG_LOG("[Client] Rotated to Face Direction");
	}
}
