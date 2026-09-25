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
#include "Model.h"
#include "AnnieWSpell.h"
#include "SkillIndicatorController.h"
#include "Button.h"

static float GetSkillRange(Protocol::PLAYER_CHAMPION_TYPE champType, int skillId)
{
	switch (champType)
	{
	case Protocol::PLAYER_CHAMPION_TYPE::PLAYER_TYPE_GAREN:
		switch (skillId)
		{
		case 0: return 2; // ?�반 공격
		case 1: return 2; // Q
		case 2: return 2; // W
		case 3: return 5; // E
		case 4: return 4; // R
		default: return 3;
		}

	case Protocol::PLAYER_CHAMPION_TYPE::PLAYER_TYPE_ANNIE:
		switch (skillId)
		{
		case 0: return 6; // ?�반 공격
		case 1: return AnnieSkillTuning::QRange; // Q
		case 2: return AnnieSkillTuning::WRange; // W
		case 3: return 2; // E
		case 4: return 6; // R
		default: return 4;
		}

	default:
		return 3; // 기본�?
	}
}




void PlayerController::Awake()
{
}

void PlayerController::Start()
{
	EnsureClickEffectModel();
}

void PlayerController::EnsureClickEffectModel()
{
	if (_clickEffectModel)
		return;

	_clickEffectModel = make_shared<Model>();
	_clickEffectModel->ReadModel(L"Effect/Cursor");
	_clickEffectModel->ReadMaterial(L"Effect/Cursor");
}

void PlayerController::LateUpdate()
{
}

void PlayerController::FixedUpdate()
{
}

void PlayerController::StopMovementForAction()
{
    BasePlayerController::StopMovementForAction();
    _legacyMoveActive = false;
    _dest = _correctPosition = GetTransform()->GetPosition();
    CancelPendingAnnieW(true);
    if (auto marker = _moveMarker.lock()) CUR_SCENE->Remove(marker);
    _moveMarker.reset();
}

void PlayerController::Update()
{
	BasePlayerController::Update();

	// Debug combat setup: either connected client can gather every Room player
	// into adjacent authoritative NavGrid Cells around the requester.
	if (!ImGui::GetIO().WantCaptureKeyboard && INPUT->GetButtonDown(KEY_TYPE::F8))
	{
		Protocol::C_TESTMsg command;
		command.set_message("combat-gather-v1");
		NETWORK->SendPacket(ClientPacketHandler::MakeSendBuffer(command, C_TEST_MSG));
		DEBUG_LOG("[CombatTest] F8 gather request sent.");
	}
	int32 mouseX = INPUT->GetMousePos().x;
	int32 mouseY = INPUT->GetMousePos().y;

	float currentTime = TIME->GetGameTime();

	// ???�킬 ?�이�?초기??
	EnsureSkillData();
	if (_playerInfo->hp() <= 0)
	{
		SelectSkill(0);
		SetAttackCursor(false);
		return;
	}

	// ??쿨�???갱신
	for (auto& skill : _skillElapsedTime)
	{
		int id = skill.first;
		_skillElapsedTime[id] += TIME->GetDeltaTime();
		_skillElapsedTime[id] = min(_skillElapsedTime[id], skillDataTable[id].cooldown);
	}

	// ???�킬 ???�력
    if (IsMovementLocked())
    {
        SelectSkill(0);
        SetAttackCursor(false);
        return;
    }
    if (!CUR_SCENE->IsWorldInputBlocked() && !ImGui::GetIO().WantCaptureKeyboard)
	{
		if (INPUT->GetButtonDown(KEY_TYPE::Q)) SelectSkill((int)SkillType::QSpell);
		if (INPUT->GetButtonDown(KEY_TYPE::W)) SelectSkill((int)SkillType::WSpell);
		if (INPUT->GetButtonDown(KEY_TYPE::E)) SelectSkill((int)SkillType::ESpell);
		if (INPUT->GetButtonDown(KEY_TYPE::R)) SelectSkill((int)SkillType::RSpell);
	}
	if (INPUT->GetButtonDown(KEY_TYPE::ESCAPE))
	{
		SelectSkill(0);
	}

	if (_annieWTargeting && UpdateAnnieWTargeting(mouseX, mouseY))
	{
		UpdateAttackCursor(mouseX, mouseY);
		return;
	}

	UpdateAttackCursor(mouseX, mouseY);

	// ???�킬 처리
	if (_currentSkillID > 0)
	{

		SkillData skill = GetSkillInfo(_currentSkillID);
		if (_skillElapsedTime[_currentSkillID] < skill.cooldown)
		{
			float remain = skill.cooldown - _skillElapsedTime[_currentSkillID];
			DEBUG_LOG("[Client] ??Skill on Cooldown (" << remain << "s)");
			SelectSkill(0);
			return;
		}

		if (skill.requiresTarget)
		{
			if (!CUR_SCENE->IsWorldInputBlocked() && !ImGui::GetIO().WantCaptureMouse &&
				INPUT->GetButtonDown(KEY_TYPE::LBUTTON))
			{
				auto pickObj = CUR_SCENE->Pick(mouseX, mouseY, _dest);
				if (pickObj && IsEnemy(pickObj))
				{
					_target = pickObj;

					// ???�동 중일 경우 ?��? 처리

					// ???�거�?체크
					const float skillRange = GetSkillRange(_playerInfo->champtype(), _currentSkillID);
					Vec3 targetDelta = _target->GetTransform()->GetPosition() - GetTransform()->GetPosition();
					if (_playerInfo->champtype() == Protocol::PLAYER_TYPE_ANNIE && _currentSkillID == (int)SkillType::QSpell)
						targetDelta.y = 0.f; // Match the ground-plane range circle.
					float distance = targetDelta.Length();
					if (distance > skillRange)
					{
						DEBUG_LOG("[Client] ??Target out of range");
						SelectSkill(0);
						return;
					}

					ProcSkill(_currentSkillID);
					_skillElapsedTime[_currentSkillID] = 0.f;
					UI->GetHUD()->GetScript<HUDController>()->TriggerSkillCoolDown(_currentSkillID);
					SelectSkill(0);
				}
			}
		}
		else
		{
			ProcSkill(_currentSkillID);
			_skillElapsedTime[_currentSkillID] = 0.f;
			UI->GetHUD()->GetScript<HUDController>()->TriggerSkillCoolDown(_currentSkillID);
			SelectSkill(0);
		}
	}

	// ???��? or ?�동 처리
	if (!IsMovementLocked() && !CUR_SCENE->IsWorldInputBlocked() && !ImGui::GetIO().WantCaptureMouse && INPUT->GetButtonDown(KEY_TYPE::RBUTTON))
	{
		if (_currentSkillID > 0) SelectSkill(0);
		if (_annieWPendingMove)
			CancelPendingAnnieW(true);
		auto pickObj = CUR_SCENE->Pick(mouseX, mouseY, _dest);
		if (pickObj)
		{
			_correctPosition = _dest;

			if (IsEnemy(pickObj))
			{
                if (IsActionBusy()) return;
				_target = pickObj;

				// ???�동 중일 경우 ?��? 처리

				const float skillRange = GetSkillRange(_playerInfo->champtype(), (int)SkillType::GeneralAtk);

				float distance = (_target->GetTransform()->GetPosition() - GetTransform()->GetPosition()).Length();
				if (distance > skillRange)
				{
					DEBUG_LOG("[Client] ??Target out of range");
					return;
				}

				if (currentTime - _lastAttackTime < _attackCooldown)
					return;

				_lastAttackTime = currentTime;
				ProcSkill((int)SkillType::GeneralAtk);
			}
			else
			{
				// ???�동 명령
				if (NETWORK->IsAuthoritativeMoveRequestsEnabled())
				{
					if (!SendAuthoritativeMoveRequest(_dest))
						return;
				}
				else
				{
                    _legacyMoveActive = true;
					if (!_isAttackMode && _currentState != PlayerState::RUN)
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
				} 			

				/// Sound Effect			
				std::string champType =  _playerInfo->champtype() == Protocol::PLAYER_CHAMPION_TYPE::PLAYER_TYPE_GAREN ? "Garen" : "Annie";
				static std::random_device rd;
				static std::mt19937 gen(rd());
				static std::uniform_int_distribution<> dis(1, 4);

				if (currentTime - _lastWalkSoundTime > WALK_SOUND_COOLDOWN)
				{
				
					_lastWalkSoundTime = currentTime;
					int walkIndex = dis(gen);  // 1~4 ?�덤				
					SOUND->PlaySound("VO_" + champType + "_walk" + std::to_string(walkIndex));
				}				

				/////////////////////////////////////////////////////////////

				EnsureClickEffectModel();

				auto effect = make_shared<GameObject>("ClickEffect");
				Vec3 pos = _dest;

				pos.y = 1.95f;
				effect->GetOrAddTransform()->SetPosition(pos);
				effect->GetOrAddTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), 0.f, 0.f));

				effect->GetOrAddTransform()->SetScale(Vec3(0.01f)); // ⬅️ 초기 ?��???

				auto controller = make_shared<CursorController>();
				effect->AddComponent(controller);


				effect->AddComponent(make_shared<ModelRenderer>(CUR_SCENE->_shader));
				{
					effect->GetModelRenderer()->SetModel(_clickEffectModel);
					effect->GetModelRenderer()->SetPass(0);
				}

				CUR_SCENE->Add(effect);
				_moveMarker = effect;
			}
		}
	}

	if (!NETWORK->IsAuthoritativeMoveRequestsEnabled())
		MoveTo(); // ???�동 처리
}



unordered_map<int, float>& PlayerController::GetCooldowns()
{
	return _skillElapsedTime;
}

void PlayerController::MoveTo()
{
	// 공격 중이�??�동 금�?
	if (IsMovementLocked())
		return;

	// IDLE ?�태�????�상 ?�동?��? ?�음
    if (!_legacyMoveActive) return;
    if (!_isAttackMode && _currentState != PlayerState::RUN)
    {
        _currentState = PlayerState::RUN;
        GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::RUN, true);
    }

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
        _legacyMoveActive = false;
        if (_isAttackMode) return;
		_currentState = PlayerState::IDLE;
		GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true);
		DEBUG_LOG("[Client] Stopped Moving -> IDLE");
	}
}



bool PlayerController::IsEnemy(shared_ptr<GameObject> obj)
{
	return obj && obj->GetScript<OtherPlayerController>();
}

void PlayerController::ProcSkill(int32 skillId)
{
	// ???�식 ?�래?�에??구현
}

bool PlayerController::HasArrivedAtDestination()
{
	Vec3 currentPos = GetTransform()->GetPosition();
	Vec3 direction = _dest - currentPos;
	direction.y = 0.0f;
	return direction.LengthSquared() < 0.05f * 0.05f;
}
bool PlayerController::SendAuthoritativeMoveRequest(const Vec3& requestedDestination)
{
    if (IsMovementLocked() || !_playerInfo || _playerInfo->hp() <= 0) return false;
	if (!NETWORK->HasNavigationInfo())
	{
		DEBUG_LOG("[Movement] MoveRequest blocked: NavigationInfo has not arrived.");
		return false;
	}

	uint32 sequence = 0;
	if (!NETWORK->TryGetNextMoveSequence(sequence))
	{
		DEBUG_LOG("[Movement] MoveRequest blocked: sequence exhausted or NavigationInfo unavailable.");
		return false;
	}

	Protocol::C_MoveRequest request;
	request.set_clientmovesequence(sequence);
	request.set_requesteddestinationx(requestedDestination.x);
	request.set_requesteddestinationz(requestedDestination.z);
	request.set_navigationmapid(NETWORK->GetNavigationMapId());
	request.set_navigationcontenthash(NETWORK->GetNavigationContentHash());
	NETWORK->SendPacket(ClientPacketHandler::MakeSendBuffer(request, C_MOVE_REQUEST));
	DEBUG_LOG("[Movement] MoveRequest sent: sequence=" << sequence << " destination=(" << requestedDestination.x << ", " << requestedDestination.z << ")");
	return true;
}

void PlayerController::OnMoveAccepted(uint64 serverMoveId, const Vec3& acceptedDestination, bool wasAdjusted)
{
    SetMinimumAuthoritativeMoveId(serverMoveId);
    if (IsMovementLocked()) return;
    _dest = acceptedDestination;
    _correctPosition = acceptedDestination;
    if (_annieWPendingMove)
        _annieWMoveId = serverMoveId;
    if (auto marker = _moveMarker.lock())
    {
        Vec3 markerPosition = acceptedDestination;
        markerPosition.y = 1.95f;
        marker->GetTransform()->SetPosition(markerPosition);
    }
    DEBUG_LOG("[Movement] MoveAccepted destination=(" << acceptedDestination.x << ", " << acceptedDestination.z << ") adjusted=" << wasAdjusted);
}

void PlayerController::OnMoveRejected(const Vec3& serverPosition, Protocol::MOVE_REJECT_REASON reason)
{
    if (_annieWPendingMove)
        CancelPendingAnnieW(true);
    _dest = serverPosition;
    _correctPosition = serverPosition;
    if (auto marker = _moveMarker.lock())
        CUR_SCENE->Remove(marker);
    _moveMarker.reset();
    if (!_isAttackMode)
    {
        _currentState = PlayerState::IDLE;
        GetGameObject()->GetModelAnimator()->SetAnimation((int32)PlayerState::IDLE, true);
    }
    DEBUG_LOG("[Movement] MoveRejected reason=" << static_cast<int>(reason) << " serverPosition=(" << serverPosition.x << ", " << serverPosition.z << ")");
}

void PlayerController::OnMovementSnapshotState(Protocol::MOVEMENT_SNAPSHOT_STATE state)
{
    if (_annieWPendingMove)
    {
        if (state == Protocol::MOVEMENT_SNAPSHOT_STATE_ARRIVED && GetLatestAuthoritativeMoveId() == _annieWMoveId)
            TryCastPendingAnnieW();
        else if (state == Protocol::MOVEMENT_SNAPSHOT_STATE_CANCELLED && GetLatestAuthoritativeMoveId() == _annieWMoveId)
            CancelPendingAnnieW(true);
    }

    if (state != Protocol::MOVEMENT_SNAPSHOT_STATE_ARRIVED && state != Protocol::MOVEMENT_SNAPSHOT_STATE_CANCELLED)
        return;
    if (auto marker = _moveMarker.lock())
        CUR_SCENE->Remove(marker);
    _moveMarker.reset();
}

void PlayerController::ConfirmSkillCooldown(int32 skillId)
{
    auto elapsed = _skillElapsedTime.find(skillId);
    auto data = skillDataTable.find(skillId);
    if (elapsed == _skillElapsedTime.end() || data == skillDataTable.end())
        return;
    elapsed->second = 0.f;
    if (UI->GetHUD() && UI->GetHUD()->GetScript<HUDController>())
        UI->GetHUD()->GetScript<HUDController>()->TriggerSkillCoolDown(skillId);
}

void PlayerController::EnsureSkillData()
{
    if (!_playerInfo || !skillDataTable.empty()) return;
    for (const SkillData& skill : GetChampionSkills(_playerInfo->champtype()))
    {
        skillDataTable[skill.SkillId] = skill;
        _skillElapsedTime[skill.SkillId] = skill.cooldown;
    }
}

void PlayerController::SelectSkill(int32 skillId)
{
    if (!_playerInfo || skillId < 0 || skillId > (int32)SkillType::RSpell) return;
    EnsureSkillData();
    CancelPendingAnnieW(true);
    _currentSkillID = 0;
    if (skillId == 0 || IsActionBusy()) return;
    const auto data = skillDataTable.find(skillId);
    if (data == skillDataTable.end() || _skillElapsedTime[skillId] < data->second.cooldown)
        return;
    const bool annie = _playerInfo->champtype() == Protocol::PLAYER_TYPE_ANNIE;
    if (annie && _playerInfo->hp() <= 0) return;
    if (annie && skillId == (int32)SkillType::WSpell)
    {
        BeginAnnieWTargeting();
        return;
    }
    _currentSkillID = skillId;
    if (annie && skillId == (int32)SkillType::QSpell)
        if (auto object = UI->GetSkillIndicatorController())
            if (auto indicator = object->GetScript<SkillIndicatorController>())
                indicator->ShowCircle(GetGameObject(), data->second.range);
}

void PlayerController::BeginAnnieWTargeting()
{
    const int32 skillId = (int32)SkillType::WSpell;
    auto elapsed = _skillElapsedTime.find(skillId);
    auto data = skillDataTable.find(skillId);
    if (elapsed == _skillElapsedTime.end() || data == skillDataTable.end() || elapsed->second < data->second.cooldown)
    {
        DEBUG_LOG("[AnnieW] Targeting blocked by cooldown or missing skill data.");
        return;
    }

    CancelPendingAnnieW(false);
    _currentSkillID = 0;
    _annieWTargeting = true;
    _annieWTargetPosition = GetTransform()->GetPosition() + GetTransform()->GetLook() * AnnieWCastRange;
    auto indicatorObject = UI->GetSkillIndicatorController();
    if (indicatorObject)
    {
        auto indicator = indicatorObject->GetScript<SkillIndicatorController>();
        if (indicator)
        {
            indicator->SetAimPosition(_annieWTargetPosition);
            indicator->Show(GetGameObject(), AnnieWCastRange, AnnieWConeAngleDegrees);
        }
    }
}

bool PlayerController::UpdateAnnieWTargeting(int32 mouseX, int32 mouseY)
{
    if (!_annieWTargeting)
        return false;

    if (INPUT->GetButtonDown(KEY_TYPE::ESCAPE) || INPUT->GetButtonDown(KEY_TYPE::RBUTTON))
    {
        CancelPendingAnnieW(true);
        return true;
    }

    Vec3 pickedPosition = _annieWTargetPosition;
    auto pickedObject = CUR_SCENE->Pick(mouseX, mouseY, pickedPosition);
    if (pickedObject)
    {
        _annieWTargetPosition = pickedPosition;
        auto indicatorObject = UI->GetSkillIndicatorController();
        if (indicatorObject)
        {
            auto indicator = indicatorObject->GetScript<SkillIndicatorController>();
            if (indicator)
                indicator->SetAimPosition(_annieWTargetPosition);
        }
    }

    if (!CUR_SCENE->IsWorldInputBlocked() && !ImGui::GetIO().WantCaptureMouse &&
        pickedObject && INPUT->GetButtonDown(KEY_TYPE::LBUTTON))
        ConfirmAnnieWTarget(_annieWTargetPosition);
    return true;
}

void PlayerController::ConfirmAnnieWTarget(const Vec3& targetPosition)
{
    if (IsActionBusy()) return;
    Vec3 delta = targetPosition - GetTransform()->GetPosition();
    delta.y = 0.f;
    const float distance = delta.Length();
    if (!std::isfinite(distance) || distance <= 0.01f)
    {
        CancelPendingAnnieW(true);
        return;
    }

    _annieWTargeting = false;
    _annieWTargetPosition = targetPosition;
    if (distance <= AnnieWCastRange)
    {
        AnnieWSpell spell;
        spell.UseAt(GetGameObject(), _annieWTargetPosition);
        CancelPendingAnnieW(true);
        return;
    }

    delta /= distance;
    const Vec3 approach = targetPosition - delta * (AnnieWCastRange - AnnieWApproachMargin);
    _annieWPendingMove = true;
    _annieWMoveId = 0;
    if (!SendAuthoritativeMoveRequest(approach))
        CancelPendingAnnieW(true);
}

void PlayerController::TryCastPendingAnnieW()
{
    if (!_annieWPendingMove || IsActionBusy())
        return;

    Vec3 delta = _annieWTargetPosition - GetTransform()->GetPosition();
    delta.y = 0.f;
    const float distance = delta.Length();
    if (!std::isfinite(distance) || distance > AnnieWCastRange + 0.05f || distance <= 0.01f)
    {
        DEBUG_LOG("[AnnieW] Move arrived outside cast range; pending cast cancelled.");
        CancelPendingAnnieW(true);
        return;
    }

    AnnieWSpell spell;
    spell.UseAt(GetGameObject(), _annieWTargetPosition);
    CancelPendingAnnieW(true);
}

void PlayerController::CancelPendingAnnieW(bool hideIndicator)
{
    _annieWTargeting = false;
    _annieWPendingMove = false;
    _annieWMoveId = 0;
    if (!hideIndicator)
        return;
    auto indicatorObject = UI->GetSkillIndicatorController();
    if (!indicatorObject)
        return;
    auto indicator = indicatorObject->GetScript<SkillIndicatorController>();
    if (indicator)
        indicator->Hide();
}

void PlayerController::UpdateAttackCursor(int32 mouseX, int32 mouseY)
{
    if (CUR_SCENE->IsWorldInputBlocked() || ImGui::GetIO().WantCaptureMouse || _annieWTargeting || _annieWPendingMove)
    {
        SetAttackCursor(false);
        return;
    }

    auto hovered = CUR_SCENE->Pick(mouseX, mouseY);
    const bool enemyHovered = IsEnemy(hovered);
    const bool qTargeting = _currentSkillID == (int32)SkillType::QSpell;
    SetAttackCursor(enemyHovered && (qTargeting || _currentSkillID == 0));
}

void PlayerController::SetAttackCursor(bool enabled)
{
    if (_attackCursorEnabled == enabled)
        return;
    auto cursor = UI->GetCursorController();
    if (!cursor || !cursor->GetButton())
        return;
    auto material = RESOURCES->Get<Material>(enabled ? L"singletarget" : L"hover_precise");
    if (!material)
        return;
    cursor->GetButton()->ChangeImageMatrial(material);
    _attackCursorEnabled = enabled;
}
