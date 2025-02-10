#pragma once
#include "MonoBehaviour.h"

class CameraScript : public MonoBehaviour

{
public:
	void Awake() override;
	void Update() override;
	float _speed = 10.f;

};

