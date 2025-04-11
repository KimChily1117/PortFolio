#pragma once
#include "OtherPlayerController.h"

class GarenOtherPlayerController : public OtherPlayerController
{
	using Super = OtherPlayerController;

public:
	void Awake() override;
	void Update() override;
	void Start() override;
	void LateUpdate() override;
	void FixedUpdate() override;

protected:
	virtual void ProcSkill(int32 skillId) override;

	void AlignToTarget();
private:
	shared_ptr<GameObject> _eSkillEffect = nullptr;
	shared_ptr<GameObject> CreateESkillEffect();
	float _zAngle = 0.f;

};

