#include "pch.h"
#include "Material.h"
#include "HUDController.h"
#include "TestScene.h"
#include "PlayerController.h"
#include "MeshRenderer.h"


////test 
//
//// ✅ 스킬별 경과 시간 저장 (초기값 = 최대 쿨타임)
//unordered_map<string, float> skillElapsedTime = {
//	{"Q", 3.0f},  // Q 스킬 초기 쿨타임 (3초)
//	{"W", 5.0f},  // W 스킬 초기 쿨타임 (5초)
//	{"E", 7.0f},  // E 스킬 초기 쿨타임 (7초)
//	{"R", 10.0f}  // R 스킬 초기 쿨타임 (10초)
//};
//
//// ✅ 스킬별 최대 쿨타임 저장
//unordered_map<string, float> skillMaxCooldown = {
//	{"Q", 3.0f},
//	{"W", 5.0f},
//	{"E", 7.0f},
//	{"R", 10.0f}
//};


// ✅ SkillData 기반 쿨타임 관리
unordered_map<int, float> skillElapsedTime;
unordered_map<int, SkillData> skillDataTable;

void HUDController::InitializeSkillData()
{
	// ✅ 챔피언 타입을 기반으로 스킬 데이터 로드 (GAREN or ANNIE)
	Protocol::PLAYER_CHAMPION_TYPE champType = GAMEMANAGER->_myPlayer->_playerInfo->champtype();
	vector<SkillData> skills = GetChampionSkills(champType);

	for (const SkillData& skill : skills)
	{
		skillElapsedTime[skill.SkillId] = skill.cooldown;  // 최대 쿨타임으로 초기화
		skillDataTable[skill.SkillId] = skill;
	}
}

void HUDController::Awake()
{

	QSkillSocket = make_shared<GameObject>("QSKill");
	QSkillSocket->AddComponent(make_shared<Button>());

	QSkillSocket->GetButton()->Create(Vec2(0, 0), Vec2(1, 1), RESOURCES->Get<Material>(L"annie_q")); // Dummy 

	QSkillSocket->GetButton()->SetOrder(5);
	QSkillSocket->GetTransform()->SetParent(GetGameObject()->GetOrAddTransform());
	QSkillSocket->LoadTrasnformData();

	CUR_SCENE->Add(QSkillSocket);

	WSkillSocket = make_shared<GameObject>("WSKill");
	WSkillSocket->AddComponent(make_shared<Button>());

	WSkillSocket->GetButton()->Create(Vec2(0, 0), Vec2(1, 1), RESOURCES->Get<Material>(L"annie_q")); // Dummy 

	WSkillSocket->GetButton()->SetOrder(5);
	WSkillSocket->GetTransform()->SetParent(GetGameObject()->GetOrAddTransform());
	WSkillSocket->LoadTrasnformData();

	CUR_SCENE->Add(WSkillSocket);

	ESkillSocket = make_shared<GameObject>("ESKill");
	ESkillSocket->AddComponent(make_shared<Button>());

	ESkillSocket->GetButton()->Create(Vec2(0, 0), Vec2(1, 1), RESOURCES->Get<Material>(L"annie_q")); // Dummy 

	ESkillSocket->GetButton()->SetOrder(5);
	ESkillSocket->GetTransform()->SetParent(GetGameObject()->GetOrAddTransform());
	ESkillSocket->LoadTrasnformData();

	CUR_SCENE->Add(ESkillSocket);

	RSkillSocket = make_shared<GameObject>("RSKill");
	RSkillSocket->AddComponent(make_shared<Button>());

	RSkillSocket->GetButton()->Create(Vec2(0, 0), Vec2(1, 1), RESOURCES->Get<Material>(L"annie_q")); // Dummy 

	RSkillSocket->GetButton()->SetOrder(5);
	RSkillSocket->GetTransform()->SetParent(GetGameObject()->GetOrAddTransform());
	RSkillSocket->LoadTrasnformData();

	CUR_SCENE->Add(RSkillSocket);


	HpBar = make_shared<GameObject>("PlayerHUD_Hp");
	HpBar->AddComponent(make_shared<Button>());

	HpBar->GetButton()->Create(Vec2(0, 0), Vec2(1, 1), RESOURCES->Get<Material>(L"PlayerHUD_Hp"));

	HpBar->GetButton()->SetOrder(5);
	HpBar->GetTransform()->SetParent(GetGameObject()->GetOrAddTransform());

	HpBar->LoadTrasnformData();
	CUR_SCENE->Add(HpBar);


	MpBar = make_shared<GameObject>("PlayerHUD_Mp");
	MpBar->AddComponent(make_shared<Button>());

	MpBar->GetButton()->Create(Vec2(0, 0), Vec2(1, 1), RESOURCES->Get<Material>(L"PlayerHUD_Mp"));

	MpBar->GetButton()->SetOrder(5);
	MpBar->GetTransform()->SetParent(GetGameObject()->GetOrAddTransform());
	MpBar->LoadTrasnformData();
	CUR_SCENE->Add(MpBar);
}

void HUDController::Update()
{
	UpdatePlayerStatus();
	UpdateSkillCooldowns();
}


void HUDController::UpdatePlayerStatus()
{
	if (GAMEMANAGER->_myPlayer)
	{
		shared_ptr<UIFillMountDesc> Hpdesc = make_shared<UIFillMountDesc>();
		Hpdesc->ratio = (float)GAMEMANAGER->_myPlayer->_playerInfo->hp() / (float)GAMEMANAGER->_myPlayer->_playerInfo->maxhp();
		HpBar->GetButton()->GetMatrial()->SetPass(5);
		HpBar->GetButton()->GetMatrial()->GetShader()->PushUiData(*Hpdesc,"UIFillMount_HP");
	
		
	
		//// ✅ MP 바 FillAmount 업데이트
		//shared_ptr<UIFillMountDesc> Mpdesc = make_shared<UIFillMountDesc>();
		//Mpdesc->ratio = (float)GAMEMANAGER->_myPlayer->_playerInfo->mp() / (float)GAMEMANAGER->_myPlayer->_playerInfo->maxmp();
		//MpBar->GetButton()->GetMatrial()->SetPass(6);
		//MpBar->GetButton()->GetMatrial()->GetShader()->PushUiData(*Mpdesc, "UIFillMount_Mp");	
	
	}
}

void HUDController::TriggerSkillCoolDown(int skillId)
{
	if (skillElapsedTime.find(skillId) != skillElapsedTime.end())
	{
		skillElapsedTime[skillId] = 0.f;  // ✅ 쿨다운 초기화
	}
}

void HUDController::UpdateSkillCooldowns()
{
	auto playerController = GAMEMANAGER->_myPlayer;
	if (!playerController)
		return;

	auto& cooldowns = playerController->GetCooldowns();

	// ✅ 스킬 데이터 테이블 초기화
	if (skillDataTable.empty())
	{
		vector<SkillData> skills = GetChampionSkills(GAMEMANAGER->_myPlayer->_playerInfo->champtype());
		for (const SkillData& skill : skills)
			skillDataTable[skill.SkillId] = skill;
	}

	// ✅ HUD 갱신 - PlayerController에서 직접 쿨다운 값 참조
	UpdateSkillCooldown(QSkillSocket, 7, cooldowns[(int)SkillType::QSpell], skillDataTable[(int)SkillType::QSpell].cooldown, "UIFillMount_Q");
	UpdateSkillCooldown(WSkillSocket, 8, cooldowns[(int)SkillType::WSpell], skillDataTable[(int)SkillType::WSpell].cooldown, "UIFillMount_W");
	UpdateSkillCooldown(ESkillSocket, 9, cooldowns[(int)SkillType::ESpell], skillDataTable[(int)SkillType::ESpell].cooldown, "UIFillMount_E");
	UpdateSkillCooldown(RSkillSocket, 10, cooldowns[(int)SkillType::RSpell], skillDataTable[(int)SkillType::RSpell].cooldown, "UIFillMount_R");
}


// ✅ 특정 스킬의 쿨타임 UI 갱신 함수 (기존 구조 유지)
void HUDController::UpdateSkillCooldown(shared_ptr<GameObject> skillButton, int pass, float& elapsedTime, float duration, const string& elementName)
{

	// ✅ 쿨타임이 끝났다면 ratio = 1.0 유지
	if (elapsedTime >= duration)
	{
		elapsedTime = duration;  // 초과 방지
		
	}
	else
	{
		elapsedTime += TIME->GetDeltaTime();
	}

	// ✅ ratio 값이 항상 1.0 이상으로 유지됨
	float cooldownRatio = 1.0f - (elapsedTime / duration);
	cooldownRatio = max(cooldownRatio, 0.0f); // 최소 0.0 보장
	cooldownRatio = min(cooldownRatio, 1.0f); // 최대 1.0 보장

	shared_ptr<UIFillMountDesc> CooldownDesc = make_shared<UIFillMountDesc>();
	CooldownDesc->ratio = cooldownRatio;

	skillButton->GetButton()->GetMatrial()->SetPass(pass);
	skillButton->GetButton()->GetMatrial()->GetShader()->PushUiData(*CooldownDesc, elementName);
}
