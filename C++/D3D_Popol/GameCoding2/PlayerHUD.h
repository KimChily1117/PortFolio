#pragma once
#include "MonoBehaviour.h"

class PlayerHUD : public MonoBehaviour
{

public:
	void Awake() override;
	void Update() override;
	void Start() override;
	void LateUpdate() override;
	void FixedUpdate() override;

};

