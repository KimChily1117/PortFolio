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
	if (INPUT->GetButtonDown(KEY_TYPE::RBUTTON))
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

	else if (INPUT->GetButtonDown(KEY_TYPE::ENTER))
	{	
		DEBUG_LOG("ENTER!!!");	
		Protocol::C_TESTMsg pkt;

		*pkt.mutable_testmessage() = " From Client : Press Enter KEY ";
		auto a = ClientPacketHandler::MakeSendBuffer(pkt, C_TEST_MSG);
		NETWORK->SendPacket(a);
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

	// 현재 위치
	Vec3 currentPosition = GetTransform()->GetPosition();
	// Target으로 이동

	/*DEBUG_LOG("Current Position Before Move: " << currentPosition.x << " " << currentPosition.z);
	DEBUG_LOG("Destination Position: " << _dest.x << " "  << _dest.z);*/




	Vec3 direction = _dest - currentPosition;
	direction.y = 0.0f; // y축 차이를 제거


	float distance = direction.Length();

	// Target에 도달했는지 확인
	if (distance >= 0.6f) // 작은 오차 허용
	{
		direction.Normalize();

		// 방향 벡터를 기준으로 Yaw 회전값 계산
		float angle = atan2f(direction.x, direction.z) + XM_PI; // XM_PI는 180도(π 라디안)  X축과 Z축 기준 각도 계산
		GetTransform()->SetRotation(Vec3(XMConvertToRadians(90.f)
			, angle, 
			0.0f)); // 회전 적용 (Yaw 값만 설정)


		Vec3 newPosition = currentPosition + direction * _speed * TIME->GetDeltaTime();
		newPosition.y = 1.6f;
		GetTransform()->SetPosition(newPosition);
	}
	else
	{
		_currentState = PlayerState::IDLE;
		GetGameObject()->GetModelAnimator()->GetTweenDesc().curr.animIndex = (int32)_currentState;
	}
}

