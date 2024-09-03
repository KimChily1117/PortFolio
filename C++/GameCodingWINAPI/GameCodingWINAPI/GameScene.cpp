#include "pch.h"
#include "GameScene.h"
#include "Player.h"

GameScene::GameScene()
{
}

GameScene::~GameScene()
{
}

void GameScene::Init()
{
	_player = new Player();
	_player->Init();
}

void GameScene::Update()
{
	if (_player)
		_player->Update();
}

void GameScene::Render(HDC hdc)
{
	if (_player)
		_player->Render(hdc);
}
