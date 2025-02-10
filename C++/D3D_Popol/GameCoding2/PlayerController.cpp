#include "pch.h"
#include "PlayerController.h"

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
	float distance = direction.Length();

	// Target�� �����ߴ��� Ȯ��
	if (distance > 0.1f) // ���� ���� ���
	{
		direction.Normalize();
		Vec3 newPosition = currentPosition + direction * _speed * TIME->GetDeltaTime();
		newPosition.y = 2.0f;
		GetTransform()->SetPosition(newPosition);
	}
	else
	{
		_currentState = PlayerState::IDLE;
	}
}
