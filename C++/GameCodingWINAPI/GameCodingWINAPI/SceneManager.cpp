#include "pch.h"
#include "SceneManager.h"
#include "DevScene.h"
#include "GameScene.h"
#include "EditScene.h" 


void SceneManager::Init()
{

}

void SceneManager::Update()
{
	if (_scene)
	{
		_scene->Update();
	}
}

void SceneManager::Render(HDC hdc)
{
	if (_scene)
		_scene->Render(hdc);
}

void SceneManager::Clear()
{
	SAFE_DELETE(_scene);
}

void SceneManager::ChangeScene(SceneType sceneType)
{
	if (_currentSceneType == sceneType)
		return;

	Scene* newScene = nullptr;

	switch (sceneType)
	{
		case SceneType::NONE:
			break;
		case SceneType::DevScene:
			newScene = new DevScene();
			break;
		case SceneType::GameScene:
			//newScene = new GameScene();
			break;
	}

	SAFE_DELETE(_scene);

	_scene = newScene;
	_currentSceneType = sceneType;

	newScene->Init();
}
