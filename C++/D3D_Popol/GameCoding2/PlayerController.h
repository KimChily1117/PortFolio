#pragma once
#include "MonoBehaviour.h"

enum class PlayerState
{
	IDLE,
	RUN,
	//.....

};




class PlayerController : public MonoBehaviour
{
	

public:
	void Awake() override;
	void Update() override;
	void Start() override;
	void LateUpdate() override;
	void FixedUpdate() override;

	
	void MoveTo();

private:
	float _speed = 2.f;
	Vec3 _dest;

	PlayerState _currentState = PlayerState::IDLE;

};

