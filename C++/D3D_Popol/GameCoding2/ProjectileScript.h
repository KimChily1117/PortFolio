#pragma once
#include "MonoBehaviour.h"

class ProjectileScript : public MonoBehaviour
{
public:
	~ProjectileScript();
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

private:
	float _trailElapsed = 0.f;
private:
	float _trailTime = 0.f;
	float _lifetime = 0.6f;
public:
	shared_ptr<GameObject> _trailObj; // 꼬리 오브젝트
};

