#pragma once
#include "Scene.h"

class GameScene : public Scene
{
public:
	GameScene();
	virtual ~GameScene() override;

public:
	void Init() override;


	void Update() override;


	void Render(HDC hdc) override;
};

