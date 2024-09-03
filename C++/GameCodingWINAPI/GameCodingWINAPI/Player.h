#pragma once
#include "Object.h"
class Player : public Object
{
public:
	Player();
	virtual ~Player() override;


public:
	void Init() override;


	void Update() override;


	void Render(HDC hdc) override;

};

