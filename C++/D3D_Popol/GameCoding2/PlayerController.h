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

	// Player�� Move�� ���ؼ� ������
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
	float _attackCooldown = 1.1f; // 1.1�� ���� �߰� �Է� ����

private:
	float _lastWalkSoundTime = -999.f;
	const float WALK_SOUND_COOLDOWN = 1.75f;



public:	
	Vec3 _dest;
	Vec3 _correctPosition;

private:
	unordered_map<int, float> _skillElapsedTime;  // ��ų�� ��ٿ� Ÿ�̸�

	unordered_map<int, SkillData> skillDataTable;


};

