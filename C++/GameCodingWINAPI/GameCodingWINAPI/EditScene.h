#pragma once
#include "Scene.h"


class EditScene : public Scene	
{
	// Scene��(��) ���� ��ӵ�
	void Init() override;
	void Update() override;
	void Render(HDC hdc) override;
};

