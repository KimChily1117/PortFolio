#include "pch.h"
#include "GeneralAttack.h"
#include "ModelAnimator.h"
#include "ClientPacketHandler.h"

void GeneralAttack::Use(shared_ptr<GameObject> caster, shared_ptr<GameObject> target)
{
	auto playerController = caster->GetScript<PlayerController>();
	if (!playerController || !target) return;

	//// ✅ ATK1 ↔ ATK2 번갈아 실행
	//int prevAttackAnim = playerController->GetLastAttackAnim();
	//int currentAttackAnim = (prevAttackAnim == (int)PlayerState::ATK1) ? (int)PlayerState::ATK2 : (int)PlayerState::ATK1;

	//playerController->SetLastAttackAnim(currentAttackAnim);
	//playerController->_currentState = (PlayerState)currentAttackAnim;

	//// ✅ 애니메이션 실행
	//auto animator = caster->GetModelAnimator();
	//if (animator)
	//{
	//	animator->SetAnimation(currentAttackAnim); // 🔥 즉시 애니메이션 적용
	//	DEBUG_LOG("[Client] 🎬 General Attack Animation Set: " << prevAttackAnim << " → " << currentAttackAnim);
	//}

	// ✅ 서버에 공격 패킷 전송
	Protocol::C_SkillCast skillPacket;
	skillPacket.set_casterid(GAMEMANAGER->_myPlayer->_playerInfo->objectid());
	skillPacket.set_skillid(0); // 기본 공격 ID
	skillPacket.mutable_targetpos()->set_x(target->GetTransform()->GetPosition().x);
	skillPacket.mutable_targetpos()->set_y(target->GetTransform()->GetPosition().y);
	skillPacket.mutable_targetpos()->set_z(target->GetTransform()->GetPosition().z);

	auto sendBuffer = ClientPacketHandler::MakeSendBuffer(skillPacket, C_SKILL_CAST);
	NETWORK->SendPacket(sendBuffer);
}
