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
	bool HasArrivedAtDestination();

	unordered_map<int, float>& GetCooldowns();

	virtual void ProcSkill(int32 skillId) override;
private:
	
	float _speed = 2.f;
	int32 _currentSkillID = 0;

private:
	float _lastAttackTime = 0.0f;
	float _attackCooldown = 1.1f; // 1.1초 동안 추가 입력 방지

private:
	float _lastWalkSoundTime = -999.f;
	const float WALK_SOUND_COOLDOWN = 1.75f;



public:	
	Vec3 _dest;
	Vec3 _correctPosition;

private:
	unordered_map<int, float> _skillElapsedTime;  // 스킬별 쿨다운 타이머

	unordered_map<int, SkillData> skillDataTable;


};

