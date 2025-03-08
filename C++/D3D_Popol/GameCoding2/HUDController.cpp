#include "pch.h"
#include "Material.h"
#include "HUDController.h"
#include "TestScene.h"

void HUDController::Awake()
{

	QSkillSocket = make_shared<GameObject>("QSKill");
	QSkillSocket->AddComponent(make_shared<Button>());

	QSkillSocket->GetButton()->Create(Vec2(0, 0), Vec2(1, 1), RESOURCES->Get<Material>(L"annie_q"));

	QSkillSocket->GetButton()->SetOrder(5);
	QSkillSocket->GetTransform()->SetParent(GetGameObject()->GetOrAddTransform());
	QSkillSocket->LoadTrasnformData();

	CUR_SCENE->Add(QSkillSocket);

	WSkillSocket = make_shared<GameObject>("WSKill");
	WSkillSocket->AddComponent(make_shared<Button>());

	WSkillSocket->GetButton()->Create(Vec2(0, 0), Vec2(1, 1), RESOURCES->Get<Material>(L"annie_q"));

	WSkillSocket->GetButton()->SetOrder(5);
	WSkillSocket->GetTransform()->SetParent(GetGameObject()->GetOrAddTransform());
	WSkillSocket->LoadTrasnformData();

	CUR_SCENE->Add(WSkillSocket);

	ESkillSocket = make_shared<GameObject>("ESKill");
	ESkillSocket->AddComponent(make_shared<Button>());

	ESkillSocket->GetButton()->Create(Vec2(0, 0), Vec2(1, 1), RESOURCES->Get<Material>(L"annie_q"));

	ESkillSocket->GetButton()->SetOrder(5);
	ESkillSocket->GetTransform()->SetParent(GetGameObject()->GetOrAddTransform());
	ESkillSocket->LoadTrasnformData();

	CUR_SCENE->Add(ESkillSocket);

	RSkillSocket = make_shared<GameObject>("RSKill");
	RSkillSocket->AddComponent(make_shared<Button>());

	RSkillSocket->GetButton()->Create(Vec2(0, 0), Vec2(1, 1), RESOURCES->Get<Material>(L"annie_q"));

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




}

void HUDController::UpdatePlayerStatus()
{

}
