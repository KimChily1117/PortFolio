#pragma once
#include "MonoBehaviour.h"

class CameraController : public MonoBehaviour
{
public:
	void Awake() override;
	void Update() override;


	shared_ptr<GameObject> _target;
	Vec3 _offset;
private:
	float _speed = 15.f;

	Vec3 _targetPos;

	float _initialY;
};

