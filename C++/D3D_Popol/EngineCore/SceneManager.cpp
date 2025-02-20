#include "pch.h"
#include "SceneManager.h"

void SceneManager::Update()
{
	if (_currentScene == nullptr)
		return;

	_currentScene->Update();
	_currentScene->LateUpdate();
}

void SceneManager::GUIRender()
{
	if (_currentScene == nullptr)
		return;

	_currentScene->GUIRender();
}
