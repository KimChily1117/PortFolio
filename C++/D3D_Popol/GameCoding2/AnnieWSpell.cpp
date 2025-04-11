#include "pch.h"
#include "AnnieWSpell.h"

void AnnieWSpell::Use(shared_ptr<GameObject> caster, shared_ptr<GameObject> target)
{
	auto playerController = caster->GetScript<PlayerController>();
	if (!playerController) return;

	Protocol::C_SkillCast skillPacket;
	skillPacket.set_casterid(GAMEMANAGER->_myPlayer->_playerInfo->objectid());
	skillPacket.set_skillid((int32)SkillType::WSpell); // ✅ 올바른 W 스킬 ID

	// ❌ 타겟 관련 정보는 필요 없음

	auto sendBuffer = ClientPacketHandler::MakeSendBuffer(skillPacket, C_SKILL_CAST);
	NETWORK->SendPacket(sendBuffer);
}

