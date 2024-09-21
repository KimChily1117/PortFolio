#include "pch.h"
#include "GameScene.h"
#include "ObjectManager.h"
#include "Player.h"

GameScene::GameScene()
{
}

GameScene::~GameScene()
{
}

void GameScene::Init()
{
	Player* player = GET_SINGLE(ObjectManager)->CreateObject<Player>();
	player->SetPos(Pos{ 400, 400 });
	GET_SINGLE(ObjectManager)->Add(player);
}

void GameScene::Update()
{
	const vector<Object*> objects = GET_SINGLE(ObjectManager)->GetObjects();
	for (Object* object : objects)
	{
		object->Update();
	}
}

void GameScene::Render(HDC hdc)
{
	const vector<Object*>& objects = GET_SINGLE(ObjectManager)->GetObjects();
	for (Object* object : objects)
	{
		object->Render(hdc);
	}
}
