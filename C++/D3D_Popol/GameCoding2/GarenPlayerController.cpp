#include "pch.h"
#include "GarenPlayerController.h"
#include "ISkill.h"
#include "GeneralAttack.h"
#include "ModelAnimator.h"
#include "TimeManager.h"
#include "GarenQSpell.h"
#include "GarenESpell.h"
#include "GarenRSpell.h"
#include "ClientPacketHandler.h"
#include "ModelRenderer.h"

void GarenPlayerController::ProcSkill(int32 skillId)
{
	if (IsActionBusy())
		return;

	shared_ptr<ISkill> skillInstance = nullptr;

	if (skillId == (int)SkillType::GeneralAtk)
	{
		skillInstance = make_shared<GeneralAttack>();
		_currentState = (_lastAttackAnim == 1) ? PlayerState::ATK2 : PlayerState::ATK1;
		_lastAttackAnim = (_lastAttackAnim == 1) ? 2 : 1;
		SOUND->PlaySound("SFX_Garen_GenAtk");

	}
	else
	{
		switch ((SkillType)skillId)
		{
			case SkillType::QSpell:
			{
				_currentState = PlayerState::Q;

				SOUND->PlaySound("VO_Garen_QSpell1");
				SOUND->PlaySound("SFX_Garen_QSpell");
				break;
			}

			case SkillType::WSpell:
			{
				_currentState = PlayerState::W;
				break;
			}
			case SkillType::ESpell:
			{
				_currentState = PlayerState::E;
				// ✅ E 스킬 이펙트 생성
				if (_eSkillEffect == nullptr)
					_eSkillEffect = CreateESkillEffect();

				SOUND->PlaySound("VO_Garen_ESpell2");
				SOUND->PlaySound("SFX_Garen_ESpell");
				break;
			}
			case SkillType::RSpell:
			{
				_currentState = PlayerState::R;
				SOUND->PlaySound("VO_Garen_RSpell1");
				break;
			}
		}
		skillInstance = CreateSkillInstance(skillId);
	}
	
	AlignToTarget();

	BeginActionAnimation();
	auto animator = GetGameObject()->GetModelAnimator();
	if (animator)
	{
			_isAttackMode = true;
			animator->SetAnimation((int32)_currentState, false);

			animator->SetAnimationEndCallback([this]()
				{
					FinishActionAnimation();

					uint64 casterId = _playerInfo->objectid();
					ClientPacketHandler::g_lastPlayedSkill.erase(casterId);

					// ✅ E 이펙트 제거
					if (_eSkillEffect)
					{
						CUR_SCENE->Remove(_eSkillEffect);
						_eSkillEffect = nullptr;
					}
				});
	}

        if (skillInstance)
            skillInstance->Use(GetGameObject(), _target);
        else if (skillId == (int)SkillType::WSpell)
        {
            Protocol::C_SkillCast packet;
            packet.set_casterid(_playerInfo->objectid());
            packet.set_skillid(skillId);
            NETWORK->SendPacket(ClientPacketHandler::MakeSendBuffer(packet, C_SKILL_CAST));
        }
	
}

void GarenPlayerController::Awake() {}
void GarenPlayerController::Start()
{
	Super::Start();
}
void GarenPlayerController::LateUpdate() {}
void GarenPlayerController::FixedUpdate() {}

void GarenPlayerController::Update()
{
    Super::Update();
    if (_isAttackMode && !GetGameObject()->GetModelAnimator() && TIME->GetGameTime() >= _timeToIdle)
        FinishActionAnimation();

    if (_eSkillEffect)
	{
		auto transform = _eSkillEffect->GetTransform();
		Vec3 effectPosition = GetTransform()->GetPosition();
		effectPosition.y = 3.25f;
		transform->SetPosition(effectPosition);

		if (_isAttackMode)
		{
			_zAngle -= XMConvertToRadians(360.f) * DT;
			transform->SetLocalRotation(Vec3(XMConvertToRadians(90.f), 0.f, _zAngle));
		}
	}
}
void GarenPlayerController::AlignToTarget()
{
	if (_target)
	{
		Vec3 myPosition = GetTransform()->GetPosition();
		Vec3 targetPosition = _target->GetTransform()->GetPosition();
		Vec3 direction = targetPosition - myPosition;
		AlignToDirection(direction);
	}
}

shared_ptr<ISkill> GarenPlayerController::CreateSkillInstance(int32 skillId)
{
	switch ((SkillType)skillId)
	{
	case SkillType::QSpell: return make_shared<GarenQSpell>();
	case SkillType::ESpell: return make_shared<GarenESpell>();
	case SkillType::RSpell: return make_shared<GarenRSpell>();
	default: return nullptr;
	}
}

shared_ptr<GameObject> GarenPlayerController::CreateESkillEffect()
{
	shared_ptr<class Model> m1 = make_shared<Model>();
	m1->ReadModel(L"Garen/Effect/ESpell");
	m1->ReadMaterial(L"Garen/Effect/GarenESpell");

	auto obj = make_shared<GameObject>("GarenEffect");

	obj->GetOrAddTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), 0.f, 0.f));
	obj->GetOrAddTransform()->SetScale(Vec3(0.03f));

	Vec3 pos = GetTransform()->GetPosition();

	pos.y = 3.25f;


	obj->GetOrAddTransform()->SetPosition(pos);

	obj->AddComponent(make_shared<ModelRenderer>(CUR_SCENE->_shader));
	{
		obj->GetModelRenderer()->SetModel(m1);
		obj->GetModelRenderer()->SetPass(0);
	}

	CUR_SCENE->Add(obj);

	return obj;
}
