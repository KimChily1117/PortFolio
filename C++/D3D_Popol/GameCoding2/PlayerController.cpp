#include "pch.h"
#include "PlayerController.h"
#include "ModelAnimator.h"

void PlayerController::Awake()
{
}

void PlayerController::Start()
{

}


void PlayerController::Update()
{
	if (INPUT->GetButtonDown(KEY_TYPE::LBUTTON))
	{
		int32 mouseX = INPUT->GetMousePos().x;
		int32 mouseY = INPUT->GetMousePos().y;

		// Picking
		auto pickObj = CUR_SCENE->Pick(mouseX, mouseY,_dest);
		if (pickObj)
		{		
			_currentState = PlayerState::RUN;
			GetGameObject()->GetModelAnimator()->GetTweenDesc().curr.animIndex = (int32)_currentState;
		}
	}
	MoveTo();
}


void PlayerController::LateUpdate()
{
}

void PlayerController::FixedUpdate()
{
}

void PlayerController::MoveTo()
{
	if (_currentState == PlayerState::IDLE)
		return;

	// ���� ��ġ
	Vec3 currentPosition = GetTransform()->GetPosition();
	// Target���� �̵�

	Vec3 direction = _dest - currentPosition;
	direction.y = 0.0f; // y�� ���̸� ����


	float distance = direction.Length();

	// Target�� �����ߴ��� Ȯ��
	if (distance >= 0.6f) // ���� ���� ���
	{
		direction.Normalize();

		// ���� ���͸� �������� Yaw ȸ���� ���
		float angle = atan2f(direction.x, direction.z) + XM_PI; // XM_PI�� 180��(�� ����)  X��� Z�� ���� ���� ���
		GetTransform()->SetRotation(Vec3(0.0f, angle, 0.0f)); // ȸ�� ���� (Yaw ���� ����)


		Vec3 newPosition = currentPosition + direction * _speed * TIME->GetDeltaTime();
		newPosition.y = 2.3f;
		GetTransform()->SetPosition(newPosition);
	}
	else
	{
		_currentState = PlayerState::IDLE;
		GetGameObject()->GetModelAnimator()->GetTweenDesc().curr.animIndex = (int32)_currentState;
	}
}
