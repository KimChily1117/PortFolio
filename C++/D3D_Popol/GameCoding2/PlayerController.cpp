#include "pch.h"
#include "Utils.h"
#include "PlayerController.h"
#include "HUDController.h"
#include "ModelAnimator.h"
#include "ModelRenderer.h"
#include "MeshRenderer.h"
#include "Terrain.h"
#include "OtherPlayerController.h"
#include "CursorController.h"

static int GetSkillRange(Protocol::PLAYER_CHAMPION_TYPE champType, int skillId)
{
	switch (champType)
	{
	case Protocol::PLAYER_CHAMPION_TYPE::PLAYER_TYPE_GAREN:
		switch (skillId)
		{
		case 0: return 2; // 일반 공격
		case 1: return 2; // Q
		case 2: return 2; // W
		case 3: return 5; // E
		case 4: return 4; // R
		default: return 3;
		}

	case Protocol::PLAYER_CHAMPION_TYPE::PLAYER_TYPE_ANNIE:
		switch (skillId)
		{
		case 0: return 6; // 일반 공격
		case 1: return 8; // Q
		case 2: return 5; // W
		case 3: return 2; // E
		case 4: return 6; // R
		default: return 4;
		}

	default:
		return 3; // 기본값
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

	// ✅ 스킬 테이블 초기화
	if (skillDataTable.empty())
	{
		vector<SkillData> skills = GetChampionSkills(_playerInfo->champtype());
		for (const SkillData& skill : skills)
		{
			skillDataTable[skill.SkillId] = skill;
			_skillElapsedTime[skill.SkillId] = skill.cooldown;
		}
	}

	// ✅ 쿨타임 갱신
	for (auto& skill : _skillElapsedTime)
	{
		int id = skill.first;
		_skillElapsedTime[id] += TIME->GetDeltaTime();
		_skillElapsedTime[id] = min(_skillElapsedTime[id], skillDataTable[id].cooldown);
	}

	// ✅ 스킬 키 입력
	if (INPUT->GetButtonDown(KEY_TYPE::Q)) _currentSkillID = (int)SkillType::QSpell;
	if (INPUT->GetButtonDown(KEY_TYPE::W)) _currentSkillID = (int)SkillType::WSpell;
	if (INPUT->GetButtonDown(KEY_TYPE::E)) _currentSkillID = (int)SkillType::ESpell;
	if (INPUT->GetButtonDown(KEY_TYPE::R)) _currentSkillID = (int)SkillType::RSpell;
	if (INPUT->GetButtonDown(KEY_TYPE::ESCAPE)) _currentSkillID = 0;

	// ✅ 스킬 처리
	if (_currentSkillID > 0)
	{

		SkillData skill = GetSkillInfo(_currentSkillID);
		if (_skillElapsedTime[_currentSkillID] < skill.cooldown)
		{
			float remain = skill.cooldown - _skillElapsedTime[_currentSkillID];
			DEBUG_LOG("[Client] ⏳ Skill on Cooldown (" << remain << "s)");
			_currentSkillID = 0;
			return;
		}

		if (skill.requiresTarget)
		{
			if (INPUT->GetButtonDown(KEY_TYPE::LBUTTON))
			{
				auto pickObj = CUR_SCENE->Pick(mouseX, mouseY, _dest);
				if (pickObj && IsEnemy(pickObj))
				{
					_target = pickObj;

					// ✅ 이동 중일 경우 정지 처리
					if (_currentState == PlayerState::RUN)
					{
						_currentState = PlayerState::IDLE;
						GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true);
						_dest = GetTransform()->GetPosition();

						_correctPosition = CUR_SCENE->_terrain->GetTerrain()->GetTileCorrectedPosition(_dest);


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

					// ✅ 사거리 체크
					int skillRange = GetSkillRange(_playerInfo->champtype(),_currentSkillID);
					float distance = (_target->GetTransform()->GetPosition() - GetTransform()->GetPosition()).Length();
					if (distance > skillRange)
					{
						DEBUG_LOG("[Client] ❌ Target out of range");
						_currentSkillID = 0;
						return;
					}

					ProcSkill(_currentSkillID);
					_skillElapsedTime[_currentSkillID] = 0.f;
					UI->GetHUD()->GetScript<HUDController>()->TriggerSkillCoolDown(_currentSkillID);
					_currentSkillID = 0;
				}
			}
		}
		else
		{
			if (_currentState == PlayerState::RUN)
			{
				_currentState = PlayerState::IDLE;
				GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true);
				_dest = GetTransform()->GetPosition();
			}

			ProcSkill(_currentSkillID);
			_skillElapsedTime[_currentSkillID] = 0.f;
			UI->GetHUD()->GetScript<HUDController>()->TriggerSkillCoolDown(_currentSkillID);
			_currentSkillID = 0;
		}
	}

	// ✅ 평타 or 이동 처리
	if (INPUT->GetButtonDown(KEY_TYPE::RBUTTON))
	{
		auto pickObj = CUR_SCENE->Pick(mouseX, mouseY, _dest);
		if (pickObj)
		{
			_correctPosition = CUR_SCENE->_terrain->GetTerrain()->GetTileCorrectedPosition(_dest);

			if (IsEnemy(pickObj))
			{
				_target = pickObj;

				// ✅ 이동 중일 경우 정지 처리
				if (_currentState == PlayerState::RUN)
				{
					_currentState = PlayerState::IDLE;
					GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true);
					_dest = GetTransform()->GetPosition();

					_correctPosition = CUR_SCENE->_terrain->GetTerrain()->GetTileCorrectedPosition(_dest);

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

				int skillRange = GetSkillRange(_playerInfo->champtype(), (int)SkillType::GeneralAtk);

				float distance = (_target->GetTransform()->GetPosition() - GetTransform()->GetPosition()).Length();
				if (distance > skillRange)
				{
					DEBUG_LOG("[Client] ❌ Target out of range");
					return;
				}

				if (currentTime - _lastAttackTime < _attackCooldown)
					return;

				_lastAttackTime = currentTime;
				ProcSkill((int)SkillType::GeneralAtk);
			}
			else
			{
				// ✅ 이동 명령
				if (_currentState != PlayerState::RUN)
				{
					_currentState = PlayerState::RUN;
					GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::RUN, true);
				}

				Protocol::C_Move movePacket;
				movePacket.set_objectid(GAMEMANAGER->_myPlayer->_playerInfo->objectid());
				movePacket.mutable_targetpos()->set_x(_dest.x);
				movePacket.mutable_targetpos()->set_y(_dest.y);
				movePacket.mutable_targetpos()->set_z(_dest.z);
				movePacket.mutable_cellpos()->set_x(_correctPosition.x);
				movePacket.mutable_cellpos()->set_z(_correctPosition.z);

				auto sendBuffer = ClientPacketHandler::MakeSendBuffer(movePacket, C_MOVE);
				NETWORK->SendPacket(sendBuffer); 			

				/// Sound Effect			
				std::string champType =  _playerInfo->champtype() == Protocol::PLAYER_CHAMPION_TYPE::PLAYER_TYPE_GAREN ? "Garen" : "Annie";
				static std::random_device rd;
				static std::mt19937 gen(rd());
				static std::uniform_int_distribution<> dis(1, 4);

				if (currentTime - _lastWalkSoundTime > WALK_SOUND_COOLDOWN)
				{
				
					_lastWalkSoundTime = currentTime;
					int walkIndex = dis(gen);  // 1~4 랜덤				
					SOUND->PlaySound("VO_" + champType + "_walk" + std::to_string(walkIndex));
				}				

				/////////////////////////////////////////////////////////////

				shared_ptr<class Model> m1 = make_shared<Model>();
				m1->ReadModel(L"Effect/Cursor");
				m1->ReadMaterial(L"Effect/Cursor");


				auto effect = make_shared<GameObject>("ClickEffect");
				Vec3 pos = _dest;

				pos.y = 1.95f;
				effect->GetOrAddTransform()->SetPosition(pos);
				effect->GetOrAddTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), 0.f, 0.f));

				effect->GetOrAddTransform()->SetScale(Vec3(0.01f)); // ⬅️ 초기 스케일

				auto controller = make_shared<CursorController>();
				effect->AddComponent(controller);


				effect->AddComponent(make_shared<ModelRenderer>(CUR_SCENE->_shader));
				{
					effect->GetModelRenderer()->SetModel(m1);
					effect->GetModelRenderer()->SetPass(0);
				}

				CUR_SCENE->Add(effect);
			}
		}
	}

	MoveTo(); // ✅ 이동 처리
}



unordered_map<int, float>& PlayerController::GetCooldowns()
{
	return _skillElapsedTime;
}

void PlayerController::MoveTo()
{
	// 공격 중이면 이동 금지
	if (_isAttackMode)
		return;

	// IDLE 상태면 더 이상 이동하지 않음
	if (_currentState == PlayerState::IDLE)
		return;

	Vec3 currentPosition = GetTransform()->GetPosition();
	direction = _dest - currentPosition;
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

bool PlayerController::HasArrivedAtDestination()
{
	Vec3 currentPos = GetTransform()->GetPosition();
	Vec3 direction = _dest - currentPos;
	direction.y = 0.0f;
	return direction.LengthSquared() < 0.05f * 0.05f;
}
