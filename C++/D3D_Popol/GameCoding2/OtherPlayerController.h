#pragma once
#include "MonoBehaviour.h"


enum class OtherPlayerState
{
	IDLE,
	RUN,
	//.....

};

class OtherPlayerController : public MonoBehaviour
{
public:
	void Awake() override;
	void Update() override;

	// 목표 위치 설정
	void SetTargetPosition(const Vec3& position);

	// 이동 속도 설정
	void SetSpeed(float speed) { _speed = speed; }

private:
	Vec3 _targetPosition = Vec3(0, 0, 0);  // 목표 위치
	bool _hasTargetPosition = false;       // 목표 위치 존재 여부
	float _speed = 2.0f;                   // 이동 속도

	OtherPlayerState _currentState = OtherPlayerState::IDLE;

};

