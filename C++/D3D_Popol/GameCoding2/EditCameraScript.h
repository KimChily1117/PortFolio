#pragma once
#include "MonoBehaviour.h"

class EditCameraScript : public MonoBehaviour

{
public:
	void Awake() override;
	void Update() override;
	float _speed = 10.f;

};

