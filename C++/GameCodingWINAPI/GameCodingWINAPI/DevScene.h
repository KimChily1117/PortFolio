#pragma once
#include "Scene.h"
class DevScene : public Scene
{
public:
	virtual void Init() override;


	virtual void Update() override;


	virtual void Render(HDC hdc) override;

};

