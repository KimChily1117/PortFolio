	#include "pch.h"
	#include "AnniePlayerController.h"
	#include "ModelAnimator.h"
	#include "TimeManager.h"
	#include "ClientPacketHandler.h"
	#include "AnnieQSpell.h"
	#include "AnnieWSpell.h"
	#include "AnnieESpell.h"
	#include "AnnieRSpell.h"
	#include "GeneralAttack.h"

	void AnniePlayerController::ProcSkill(int32 skillId)
	{
		if (_isAttackMode) return;



		AlignToTarget();
		shared_ptr<ISkill> skillInstance = nullptr;

		if (skillId == (int)SkillType::GeneralAtk)
		{
			skillInstance = make_shared<GeneralAttack>();

			_currentState = (_lastAttackAnim == 1) ? PlayerState::ATK2 : PlayerState::ATK1;
			_lastAttackAnim = (_lastAttackAnim == 1) ? 2 : 1;

			SOUND->PlaySound("SFX_Annie_GenATK");


		}
		else
		{
			switch ((SkillType)skillId)
			{
			case SkillType::QSpell:
			{
				_currentState = PlayerState::Q; 
				SOUND->PlaySound("SFX_Annie_QSpell");

				break;
			}
			case SkillType::WSpell: 
			{
				_currentState = PlayerState::W;
				Vec3 pos = GetTransform()->GetPosition();

				// 이동 중 방향이 유효하면 그걸 사용, 아니라면 기본 forward
				Vec3 dir = (direction.LengthSquared() > 0.001f) ? direction : GetTransform()->GetLook();
				Vec3 rot = CalculateRotationFromDirection(dir);


				SOUND->PlaySound("SFX_Annie_WSpell");
				PARTICLE->Play(L"AnnieW", pos, rot);
				break;
			}
			case SkillType::ESpell: _currentState = PlayerState::E; break;
			case SkillType::RSpell: _currentState = PlayerState::R; break;
			}
			skillInstance = CreateSkillInstance(skillId);
		}


		auto animator = GetGameObject()->GetModelAnimator();
		if (animator)
		{
			_isAttackMode = true;
			animator->SetAnimation((int32)_currentState, false);

			_timeToIdle = TIME->GetGameTime() + animator->GetAnimationDuration((int32)_currentState);
			DEBUG_LOG("[Client] ▶ AnniePlayer Skill Started: " << skillId);
		}

		if (skillInstance)
		{
			skillInstance->Use(GetGameObject(), _target);
		}
	}

	void AnniePlayerController::Update()
	{
		Super::Update();

		if (_isAttackMode && TIME->GetGameTime() >= _timeToIdle)
		{
			_isAttackMode = false;
			_currentState = PlayerState::IDLE;
			GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true);

			uint64 casterId = _playerInfo->objectid();
			ClientPacketHandler::g_lastPlayedSkill.erase(casterId);
			DEBUG_LOG("[Client] ✅ AnniePlayer Skill Timeout → IDLE + Reset");
		}
	}

	shared_ptr<ISkill> AnniePlayerController::CreateSkillInstance(int32 skillId)
	{
		switch ((SkillType)skillId)
		{
		case SkillType::QSpell: return make_shared<AnnieQSpell>();
		case SkillType::WSpell: return make_shared<AnnieWSpell>();
		case SkillType::ESpell: return make_shared<AnnieESpell>();
		case SkillType::RSpell: return make_shared<AnnieRSpell>();
		default: return nullptr;
		}
	}

	void AnniePlayerController::Awake()
	{

	}

	//void AnniePlayerController::Update()
	//{
	//	Super::Update();
	//
	//	if (_isAttackMode)
	//	{
	//		if (TIME->GetGameTime() >= _timeToIdle)
	//		{
	//			_isAttackMode = false;
	//			_currentState = PlayerState::IDLE;
	//			GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true);
	//
	//			uint64 casterId = _playerInfo->objectid();
	//			ClientPacketHandler::g_lastPlayedSkill.erase(casterId);
	//		}
	//		return;
	//	}
	//}


	void AnniePlayerController::Start()
	{

	}

	void AnniePlayerController::LateUpdate()
	{

	}

	void AnniePlayerController::FixedUpdate()
	{

	}

	void AnniePlayerController::AlignToTarget()
	{
		if (_target)
		{
			Vec3 dir = _target->GetTransform()->GetPosition() - GetTransform()->GetPosition();
			AlignToDirection(dir);
		}


		else
		{
			// 타겟이 없으면 현재 바라보는 방향 유지
			Vec3 dir = GetTransform()->GetLook();
			CalculateRotationFromDirection(dir); // 혹은 그냥 유지
		}
	}

	Vec3 AnniePlayerController::CalculateRotationFromDirection(const Vec3& dir)
	{
		Vec3 forward = dir;
		forward.y = 0.f;
		forward.Normalize();

		float angleY = atan2f(forward.x, forward.z); // 🔥 XM_PI 곱하지 말기
		return Vec3((0.f), XMConvertToRadians(angleY), 0.f); // X=90 유지 (파티클이 위를 향하게)
	}