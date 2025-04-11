#pragma once
#include "MonoBehaviour.h"

class OtherProjectileScript : public MonoBehaviour
{
public:
	void Awake() override;
	void Update() override;

	void CreateTrail();

	void UpdateTrail(const Vec3& nextPos);

	void SetTarget(const Vec3& targetPos, float speed);

private:

	Vec3 _startPos;
	Vec3 _targetPos;
	Vec3 _direction;
	float _speed = 0.f;
	bool _isMoving = false;
	bool _hasHit = false;
public:
	// Trail
// OtherProjectileScript.h 내부 변수
	float _trailTime = 0.f;
	float _lifetime = 0.6f;
	shared_ptr<GameObject> _trailObj;

};

