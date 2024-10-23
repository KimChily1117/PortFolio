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
#include "TilemapActor.h"
#include "Tilemap.h"
#include "SoundManager.h"
#include "Sound.h"



DevScene::DevScene()
{

}

DevScene::~DevScene()
{
}

void DevScene::Init()
{
	GET_SINGLE(ResourceManager)->LoadTexture(L"Stage01", L"Sprite\\Map\\Stage01.bmp");
	GET_SINGLE(ResourceManager)->LoadTexture(L"Tile", L"Sprite\\Map\\Tile.bmp", RGB(128, 128, 128));
	GET_SINGLE(ResourceManager)->LoadTexture(L"Potion", L"Sprite\\UI\\Mp.bmp");
	GET_SINGLE(ResourceManager)->LoadTexture(L"PlayerDown", L"Sprite\\Player\\PlayerDown.bmp", RGB(128, 128, 128));
	GET_SINGLE(ResourceManager)->LoadTexture(L"PlayerUp", L"Sprite\\Player\\PlayerUp.bmp", RGB(128, 128, 128));
	GET_SINGLE(ResourceManager)->LoadTexture(L"PlayerLeft", L"Sprite\\Player\\PlayerLeft.bmp", RGB(128, 128, 128));
	GET_SINGLE(ResourceManager)->LoadTexture(L"PlayerRight", L"Sprite\\Player\\PlayerRight.bmp", RGB(128, 128, 128));
	GET_SINGLE(ResourceManager)->LoadTexture(L"Start", L"Sprite\\UI\\Start.bmp", RGB(255, 255, 255));
	GET_SINGLE(ResourceManager)->LoadTexture(L"Edit", L"Sprite\\UI\\Edit.bmp", RGB(255, 255, 255));
	GET_SINGLE(ResourceManager)->LoadTexture(L"Exit", L"Sprite\\UI\\Exit.bmp", RGB(255, 255, 255));


	GET_SINGLE(ResourceManager)->CreateSprite(L"Stage01", GET_SINGLE(ResourceManager)->GetTexture(L"Stage01"));

	GET_SINGLE(ResourceManager)->CreateSprite(L"TileO", GET_SINGLE(ResourceManager)->GetTexture(L"Tile"), 0, 0, 48, 48);
	GET_SINGLE(ResourceManager)->CreateSprite(L"TileX", GET_SINGLE(ResourceManager)->GetTexture(L"Tile"), 48, 0, 48, 48);


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

		background->SetPos(Vec2(size.x / 2, size.y / 2));
		background->SetLayer(LAYER_BACKGROUND);
		AddActors(background);
	}



	// 캐릭터
	{
		Player* player = new Player();
		{
			BoxCollider* collider = new BoxCollider();
			collider->SetSize({ 100,100 });
			
			collider->SetCollisionFlag(COLLISION_LAYER_TYPE::CLT_OBJECT);
			
			collider->ResetCollisionFlag();

			collider->AddCollisionFlagLayer(CLT_GROUND);
			
			
			player->AddComponent(collider);			
			GET_SINGLE(CollisionManager)->AddCollider(collider);
		}
		AddActors(player);
	}


	// 위에 떠있는 Collider
	{
		Actor* player = new Actor();
		{
			BoxCollider* collider = new BoxCollider();
			collider->SetSize({ 100,100 });

			player->AddComponent(collider);
			GET_SINGLE(CollisionManager)->AddCollider(collider);
			player->SetPos({ 400, 200 });
		}
		AddActors(player);
	}


	/*
	// << >>
	// &
	// |
	// ~
	uint32 flag = 0;



	// 특정 비트 켜기
	flag = flag | (1 << CLT_GROUND);


	// 특정 비트 끄기
	//flag = flag & ~(1 << CLT_GROUND);


	// 비트 체크
	//bool ground = flag & (1 << CLT_GROUND);

	// 전체 켜기
	//flag = ~(0);


	*/

	{
		Actor* player = new Actor();
		{
			BoxCollider* collider = new BoxCollider(); 
			collider->SetSize({ 10000,100 });
			collider->SetCollisionLayer(COLLISION_LAYER_TYPE::CLT_GROUND);

			player->AddComponent(collider);
			GET_SINGLE(CollisionManager)->AddCollider(collider);
			player->SetPos({ 200,400 });
		}
		AddActors(player);
	}


	//{
	//	TestPanel* ui = new TestPanel();
	//	_uis.push_back(ui);
	//}

	{
		TilemapActor* actor = new TilemapActor();
		AddActors(actor);

		_tilemapActor = actor;
		{

			auto* tm = GET_SINGLE(ResourceManager)->CreateTilemap(L"Tilemap_01");
			tm->SetTileSize(48);
			tm->SetMapSize({ 64,43 });

			_tilemapActor->SetTilemap(tm);
			_tilemapActor->SetShowDebug(true);
		}

	}
	GET_SINGLE(ResourceManager)->LoadSound(L"BGM", L"Sound\\BGM.wav");
	{
		Sound* sound = GET_SINGLE(ResourceManager)->GetSound(L"BGM");
		sound->Play(true);
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

	if (GET_SINGLE(InputManager)->GetButton(KeyType::Q))
	{
		GET_SINGLE(ResourceManager)->SaveTilemap(L"Tilemap_01", L"Tilemap\\Tilemap_01.txt");
	}
	else if (GET_SINGLE(InputManager)->GetButton(KeyType::E))
	{
		GET_SINGLE(ResourceManager)->LoadTilemap(L"Tilemap_01", L"Tilemap\\Tilemap_01.txt");
	}
	else if (GET_SINGLE(InputManager)->GetButton(KeyType::R))
	{
		_tilemapActor->SetShowDebug(false);
	}

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
