#include "pch.h"
#include "DevScene.h"
#include "Utils.h"
#include "InputManager.h"
#include "TimeManager.h"
#include "ResourceManager.h"
#include "CollisionManager.h"
#include "Texture.h"
#include "SpriteActor.h"
#include "Sprite.h"
#include "Flipbook.h"
#include "Player.h"
#include "BoxCollider.h"
#include "SphereCollider.h"
#include "UI.h"
#include "Button.h"
#include "TestPanel.h"



DevScene::DevScene()
{

}

DevScene::~DevScene()
{
}

void DevScene::Init()
{
	GET_SINGLE(ResourceManager)->LoadTexture(L"Stage01", L"Sprite\\Map\\Stage01.bmp");
	GET_SINGLE(ResourceManager)->LoadTexture(L"Potion", L"Sprite\\UI\\Mp.bmp");
	GET_SINGLE(ResourceManager)->LoadTexture(L"PlayerDown", L"Sprite\\Player\\PlayerDown.bmp", RGB(128, 128, 128));
	GET_SINGLE(ResourceManager)->LoadTexture(L"PlayerUp", L"Sprite\\Player\\PlayerUp.bmp", RGB(128, 128, 128));
	GET_SINGLE(ResourceManager)->LoadTexture(L"PlayerLeft", L"Sprite\\Player\\PlayerLeft.bmp", RGB(128, 128, 128));
	GET_SINGLE(ResourceManager)->LoadTexture(L"PlayerRight", L"Sprite\\Player\\PlayerRight.bmp", RGB(128, 128, 128));
	GET_SINGLE(ResourceManager)->LoadTexture(L"Start", L"Sprite\\UI\\Start.bmp",RGB(255,255,255));
	GET_SINGLE(ResourceManager)->LoadTexture(L"Edit", L"Sprite\\UI\\Edit.bmp",RGB(255,255, 255));
	GET_SINGLE(ResourceManager)->LoadTexture(L"Exit", L"Sprite\\UI\\Exit.bmp", RGB(255,255, 255));


	GET_SINGLE(ResourceManager)->CreateSprite(L"Stage01", GET_SINGLE(ResourceManager)->GetTexture(L"Stage01"));
	GET_SINGLE(ResourceManager)->CreateSprite(L"Start_Off", GET_SINGLE(ResourceManager)->GetTexture(L"Start"), 0, 0, 150, 150);
	GET_SINGLE(ResourceManager)->CreateSprite(L"Start_On", GET_SINGLE(ResourceManager)->GetTexture(L"Start"), 150, 0, 150, 150);
	GET_SINGLE(ResourceManager)->CreateSprite(L"Edit_Off", GET_SINGLE(ResourceManager)->GetTexture(L"Edit"), 0, 0, 150, 150);
	GET_SINGLE(ResourceManager)->CreateSprite(L"Edit_On", GET_SINGLE(ResourceManager)->GetTexture(L"Edit"), 150, 0, 150, 150);
	GET_SINGLE(ResourceManager)->CreateSprite(L"Exit_Off", GET_SINGLE(ResourceManager)->GetTexture(L"Exit"), 0, 0, 150, 150);
	GET_SINGLE(ResourceManager)->CreateSprite(L"Exit_On", GET_SINGLE(ResourceManager)->GetTexture(L"Exit"), 150, 0, 150, 150);

	{
		Texture* texture = GET_SINGLE(ResourceManager)->GetTexture(L"PlayerUp");
		Flipbook* fb = GET_SINGLE(ResourceManager)->CreateFlipbook(L"FB_MoveUp");
		fb->SetInfo({ texture, L"FB_MoveUp", {200, 200}, 0, 9, 1, 0.5f });
	}
	{
		Texture* texture = GET_SINGLE(ResourceManager)->GetTexture(L"PlayerDown");
		Flipbook* fb = GET_SINGLE(ResourceManager)->CreateFlipbook(L"FB_MoveDown");
		fb->SetInfo({ texture, L"FB_MoveDown", {200, 200}, 0, 9, 1, 0.5f });
	}
	{
		Texture* texture = GET_SINGLE(ResourceManager)->GetTexture(L"PlayerLeft");
		Flipbook* fb = GET_SINGLE(ResourceManager)->CreateFlipbook(L"FB_MoveLeft");
		fb->SetInfo({ texture, L"FB_MoveLeft", {200, 200}, 0, 9, 1, 0.5f });
	}
	{
		Texture* texture = GET_SINGLE(ResourceManager)->GetTexture(L"PlayerRight");
		Flipbook* fb = GET_SINGLE(ResourceManager)->CreateFlipbook(L"FB_MoveRight");
		fb->SetInfo({ texture, L"FB_MoveRight", {200, 200}, 0, 9, 1, 0.5f });
	}


	{
		Sprite* sprite = GET_SINGLE(ResourceManager)->GetSprite(L"Stage01");

		SpriteActor* background = new SpriteActor();

		background->SetSprite(sprite);
		const Vec2Int size = sprite->GetSize();

		background->SetPos(Vec2( size.x / 2 ,size.y	/ 2 ));
		background->SetLayer(LAYER_BACKGROUND);
		AddActors(background);		
	}


	{
		Player* player = new Player();
		{
			SphereCollider* collider = new SphereCollider();
			collider->SetRadius(100);
			player->AddComponent(collider);
			GET_SINGLE(CollisionManager)->AddCollider(collider);
		}
		AddActors(player);
	}

	{
		Actor* player = new Actor();
		{
			SphereCollider* collider = new SphereCollider();
			collider->SetRadius(50);
			player->AddComponent(collider);
			GET_SINGLE(CollisionManager)->AddCollider(collider);
			player->SetPos({ 400, 200 });
		}
		AddActors(player);
	}
	{
		TestPanel* ui = new TestPanel();
		_uis.push_back(ui);
	}

	for (auto actors : _actors)
	{
		actors->BeginPlay();
	}

	for (UI* ui : _uis)
		ui->BeginPlay();
}

void DevScene::Update()
{	
	GET_SINGLE(CollisionManager)->Update();

	for (auto actors : _actors)
	{
		actors->Tick();
	}
	for (UI* ui : _uis)
		ui->Tick();
}

void DevScene::Render(HDC hdc)
{

	for (auto actors : _actors)
	{
		actors->Render(hdc);
	}
	for (UI* ui : _uis)
		ui->Render(hdc);
	/*
	Texture* tex = GET_SINGLE(ResourceManager)->GetTexture(L"Stage01");

	::BitBlt(hdc, 0, 0, GWinSizeX, GWinSizeY, tex->GetDC(), 0, 0, SRCCOPY);*/
}

void DevScene::AddActors(Actor* actor)
{
	if (actor == nullptr)
		return;
	_actors.push_back(actor);
}
