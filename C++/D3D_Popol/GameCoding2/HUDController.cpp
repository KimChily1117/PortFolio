#include "pch.h"
#include "Material.h"
#include "HUDController.h"
#include "TestScene.h"
#include "PlayerController.h"
#include "MeshRenderer.h"


//test 

// ✅ 스킬별 경과 시간 저장 (초기값 = 최대 쿨타임)
unordered_map<string, float> skillElapsedTime = {
	{"Q", 3.0f},  // Q 스킬 초기 쿨타임 (3초)
	{"W", 5.0f},  // W 스킬 초기 쿨타임 (5초)
	{"E", 7.0f},  // E 스킬 초기 쿨타임 (7초)
	{"R", 10.0f}  // R 스킬 초기 쿨타임 (10초)
};

// ✅ 스킬별 최대 쿨타임 저장
unordered_map<string, float> skillMaxCooldown = {
	{"Q", 3.0f},
	{"W", 5.0f},
	{"E", 7.0f},
	{"R", 10.0f}
};

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


// ✅ 키 입력 시 스킬 쿨타임 리셋 (0초부터 시작)
void HUDController::CheckSkillInput()
{
	if (INPUT->GetButtonDown(KEY_TYPE::Q)) skillElapsedTime["Q"] = 0.f;
	if (INPUT->GetButtonDown(KEY_TYPE::W)) skillElapsedTime["W"] = 0.f;
	if (INPUT->GetButtonDown(KEY_TYPE::E)) skillElapsedTime["E"] = 0.f;
	if (INPUT->GetButtonDown(KEY_TYPE::R)) skillElapsedTime["R"] = 0.f;
}

// ✅ 스킬 쿨타임 UI 갱신
void HUDController::UpdateSkillCooldowns()
{
	CheckSkillInput();
	bool isQActive = INPUT->GetButtonDown(KEY_TYPE::Q);
	bool isWActive = INPUT->GetButtonDown(KEY_TYPE::W);
	bool isEActive = INPUT->GetButtonDown(KEY_TYPE::E);
	bool isRActive = INPUT->GetButtonDown(KEY_TYPE::R);

	// ✅ 스킬 사용 여부를 기반으로 쿨타임 갱신
	UpdateSkillCooldown(QSkillSocket, 7, skillElapsedTime["Q"], 3.0f, "UIFillMount_Q", isQActive);
	UpdateSkillCooldown(WSkillSocket, 8, skillElapsedTime["W"], 5.0f, "UIFillMount_W", isWActive);
	UpdateSkillCooldown(ESkillSocket, 9, skillElapsedTime["E"], 7.0f, "UIFillMount_E", isEActive);
	UpdateSkillCooldown(RSkillSocket, 10, skillElapsedTime["R"], 10.0f, "UIFillMount_R", isRActive);
}

// ✅ 특정 스킬의 쿨타임 UI 갱신 함수
void HUDController::UpdateSkillCooldown(shared_ptr<GameObject> skillButton, int pass, float& elapsedTime, float duration, const string& elementName , bool isSkillActive)
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
