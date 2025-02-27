#pragma once
#include "MonoBehaviour.h"

class CameraController : public MonoBehaviour
{
public:
	void Awake() override;
	void Update() override;


private:
	shared_ptr<GameObject> _target;
	float _speed = 15.f;

	Vec3 _targetPos;
	Vec3 _offset;
};

