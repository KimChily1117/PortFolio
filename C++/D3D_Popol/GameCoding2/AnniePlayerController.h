﻿#pragma once
#include "PlayerController.h"

class AnniePlayerController : public PlayerController
{
	using Super = PlayerController;

public:
	void Awake() override;
	void Update() override;
	void Start() override;
	void LateUpdate() override;
	void FixedUpdate() override;
	void AlignToTarget();

protected:
	virtual void ProcSkill(int32 skillId) override;

};

