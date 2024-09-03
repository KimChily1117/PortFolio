#pragma once
#include "Object.h"
class Monster :  public Object
{
public:
	Monster();
	virtual ~Monster() override;


public:
	void Init() override;


	void Update() override;


	void Render(HDC hdc) override;

};

