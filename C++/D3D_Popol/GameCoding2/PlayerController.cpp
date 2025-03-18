#include "pch.h"
#include "Utils.h"
#include "PlayerController.h"
#include "HUDController.h"
#include "ModelAnimator.h"
#include "MeshRenderer.h"
#include "Terrain.h"
#include "OtherPlayerController.h"

static int GetSkillRange(int skillId)
{
	switch (skillId)
	{
	case 1: return 3;
	case 2: return 4;
	case 3: return 2;
	case 4: return 5;
	default: return 3;
	}
}

void PlayerController::Awake()
{
}

void PlayerController::Start()
{
}

void PlayerController::LateUpdate()
{
}

void PlayerController::FixedUpdate()
{
}

void PlayerController::Update()
{
	int32 mouseX = INPUT->GetMousePos().x;
	int32 mouseY = INPUT->GetMousePos().y;
	float currentTime = TIME->GetGameTime();

	// ✅ 공격 중이면 다른 동작 불가 (애니메이션 종료 후 IDLE 전환)
	if (_isAttackMode)
	{
		if (TIME->GetGameTime() >= _timeToIdle)
		{
			_isAttackMode = false;
			_currentState = PlayerState::IDLE;
			GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true);
			DEBUG_LOG("[Client] 🔄 Attack Ended → IDLE");
		}
		return;
	}

	// ✅ 우클릭 처리 (공격 또는 이동)
	if (INPUT->GetButtonDown(KEY_TYPE::RBUTTON))
	{
		auto pickObj = CUR_SCENE->Pick(mouseX, mouseY, _dest);
		if (pickObj)
		{
			_correctPosition = CUR_SCENE->_terrain->GetTerrain()->GetTileCorrectedPosition(_dest);

			if (IsEnemy(pickObj))
			{
				if (currentTime - _lastAttackTime < _attackCooldown)
				{
					DEBUG_LOG("[Client] ❌ Attack input ignored (cooldown active)");
					return;
				}

				_lastAttackTime = currentTime;
				DEBUG_LOG("Deteched Enemy!!! : " << pickObj->_name.c_str());
				_target = pickObj;

				// ✅ 공격 실행 (자식 클래스에서 구현)
				ProcSkill(0);
			}
			else
			{
				// ✅ 이미 이동 중이면 애니메이션 재설정 X, 목표 지점만 변경
				if (_currentState != PlayerState::RUN)
				{
					_currentState = PlayerState::RUN;
					GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::RUN, true);
					DEBUG_LOG("[Client] 🚶 Running Animation Started");
				}
				else
				{
					DEBUG_LOG("[Client] 🏃 Updating Target Position Only");
				}

				// ✅ 서버로 이동 패킷 전송 (목표 위치만 업데이트)
				Protocol::C_Move movePacket;
				movePacket.set_objectid(GAMEMANAGER->_myPlayer->_playerInfo->objectid());
				movePacket.mutable_targetpos()->set_x(_dest.x);
				movePacket.mutable_targetpos()->set_y(_dest.y);
				movePacket.mutable_targetpos()->set_z(_dest.z);

				movePacket.mutable_cellpos()->set_x(_correctPosition.x);
				movePacket.mutable_cellpos()->set_z(_correctPosition.z);

				auto sendBuffer = ClientPacketHandler::MakeSendBuffer(movePacket, C_MOVE);
				NETWORK->SendPacket(sendBuffer);
			}
		}
	}

	MoveTo();
}

// ✅ 이동 로직
void PlayerController::MoveTo()
{
	if (_currentState == PlayerState::IDLE)
		return;

	Vec3 currentPosition = GetTransform()->GetPosition();
	Vec3 direction = _dest - currentPosition;
	direction.y = 0.0f;

	float distance = direction.Length();

	if (distance >= 0.05f)
	{
		direction.Normalize();
		float angle = atan2f(direction.x, direction.z) + XM_PI;
		GetTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), angle, 0.0f));

		Vec3 newPosition = currentPosition + direction * _speed * TIME->GetDeltaTime();
		newPosition.y = 1.6f;
		GetTransform()->SetPosition(newPosition);
	}
	else
	{
		_currentState = PlayerState::IDLE;
		GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true);
		DEBUG_LOG("[Client] 🔄 Stopped Moving → IDLE");
	}
}

bool PlayerController::IsEnemy(shared_ptr<GameObject> obj)
{
	return obj && obj->GetScript<OtherPlayerController>();
}

void PlayerController::ProcSkill(int32 skillId)
{
	// ✅ 자식 클래스에서 구현
}
