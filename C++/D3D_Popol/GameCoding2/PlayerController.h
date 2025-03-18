#pragma once
#include "MonoBehaviour.h"
#include "BasePlayerController.h"

class PlayerController : public BasePlayerController
{
public:
	void Awake() override;
	void Update() override;
	void Start() override;
	void LateUpdate() override;
	void FixedUpdate() override;

	// Player의 Move를 위해서 선언함
	void MoveTo();
	
	bool IsEnemy(shared_ptr<GameObject> obj);

	virtual void ProcSkill(int32 skillId) override;
private:
	
	float _speed = 2.f;
	Vec3 _dest;
	int32 currentSkillID = 0;

private:
	float _lastAttackTime = 0.0f;
	float _attackCooldown = 1.1f; // 1.1초 동안 추가 입력 방지
public:	
	shared_ptr<Protocol::ObjectInfo> _playerInfo;
	Vec3 _correctPosition;
};

