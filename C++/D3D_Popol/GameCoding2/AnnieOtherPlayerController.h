#pragma once
#include "PlayerController.h"
class AnnieOtherPlayerController : public PlayerController
{
	using Super = PlayerController;

public:
	void Awake() override;
	void Update() override;
	void Start() override;
	void LateUpdate() override;
	void FixedUpdate() override;
};


