#include "pch.h"
#include "GarenOtherPlayerController.h"
#include "ModelAnimator.h"
#include "ISkill.h"
#include "TimeManager.h"
#include "ModelRenderer.h"
#include "ClientPacketHandler.h"

void GarenOtherPlayerController::Awake()
{
	Super::Awake();
}

void GarenOtherPlayerController::Start()
{
	Super::Start();
}

void GarenOtherPlayerController::LateUpdate()
{
	Super::LateUpdate();
}

void GarenOtherPlayerController::FixedUpdate()
{
	Super::FixedUpdate();
}

void GarenOtherPlayerController::Update()
{
	Super::Update();

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

	if (_isAttackMode)
	{
		if (TIME->GetGameTime() >= _timeToIdle)
		{
			FinishActionAnimation();
			uint64 casterId = _playerInfo->objectid();
			ClientPacketHandler::g_lastPlayedSkill.erase(casterId);
			if (_eSkillEffect)
			{
				CUR_SCENE->Remove(_eSkillEffect);
				_eSkillEffect = nullptr;
			}
		}
		return;
	}

	if (HasAuthoritativeSnapshotStream())
		return;

	if (_hasTargetPosition)
	{
		if (_currentState != PlayerState::RUN)
		{
			_currentState = PlayerState::RUN;
			GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::RUN, true);
		}
	}
	else if (_currentState == PlayerState::RUN)
	{
		_currentState = PlayerState::IDLE;
		GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true);
	}
}
void GarenOtherPlayerController::ProcSkill(int32 skillId)
{
	if (skillId == (int)SkillType::GeneralAtk)
	{
		_currentState = (_lastAttackAnim == 1) ? PlayerState::ATK2 : PlayerState::ATK1;
		_lastAttackAnim = (_lastAttackAnim == 1) ? 2 : 1;
	}
	else
	{
		switch ((SkillType)skillId)
		{
		case SkillType::QSpell: _currentState = PlayerState::Q; break;
		case SkillType::WSpell: _currentState = PlayerState::W; break;
		case SkillType::ESpell:
			_currentState = PlayerState::E;

			// ✅ E 이펙트 생성
			if (_eSkillEffect == nullptr)
				_eSkillEffect = CreateESkillEffect();
			break;
		case SkillType::RSpell: _currentState = PlayerState::R; break;
		}
	}

	AlignToTarget();

	BeginActionAnimation();
	auto animator = GetGameObject()->GetModelAnimator();
	if (animator)
	{
		_isAttackMode = true;
		animator->SetAnimation((int32)_currentState, false);


		DEBUG_LOG("[Client] 🎬 AnnieOtherPlayer Skill Played: " << skillId);
	}
}

void GarenOtherPlayerController::AlignToTarget()
{
	if (_target)
	{
		Vec3 myPosition = GetTransform()->GetPosition();
		Vec3 targetPosition = _target->GetTransform()->GetPosition();
		Vec3 direction = targetPosition - myPosition;
		AlignToDirection(direction);
	}
}

shared_ptr<GameObject> GarenOtherPlayerController::CreateESkillEffect()
{
	shared_ptr<class Model> m1 = make_shared<Model>();
	m1->ReadModel(L"Garen/Effect/ESpell");
	m1->ReadMaterial(L"Garen/Effect/GarenESpell");

	auto obj = make_shared<GameObject>("GarenEffect_Other");

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
