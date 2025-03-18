#pragma once
#include "MonoBehaviour.h"
#include "BasePlayerController.h"

class OtherPlayerController : public BasePlayerController
{
public:
	void Awake() override;
	void Update() override;

	// 목표 위치 설정
	void SetTargetPosition(const Vec3& position);

	// 이동 속도 설정
	void SetSpeed(float speed) { _speed = speed; }


protected:
	virtual void ProcSkill(int32 skillId) override;
	bool _hasTargetPosition = false;       // 목표 위치 존재 여부


private:
	Vec3 _targetPosition = Vec3(0, 0, 0);  // 목표 위치
	float _speed = 2.0f;                   // 이동 속도

public:

	shared_ptr<Protocol::ObjectInfo> _playerInfo;
	float _timeToIdle = 0.3f;
};

