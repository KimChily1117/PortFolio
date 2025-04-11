#pragma once
#include "PlayerController.h"
class GarenPlayerController : public PlayerController
{
	using Super = PlayerController;

public:
	void Awake() override;
	void Update() override;
	void Start() override;
	void LateUpdate() override;
	void FixedUpdate() override;


protected:
	virtual void ProcSkill(int32 skillId) override;
	void AlignToTarget();
	shared_ptr<class ISkill> CreateSkillInstance(int32 skillId);

private:
	shared_ptr<GameObject> _eSkillEffect = nullptr;
	float _zAngle = 0.f;
	shared_ptr<GameObject> CreateESkillEffect();
};

