#pragma once

#include "MonoBehaviour.h"
#include "BasePlayerController.h"

class Model;

class PlayerController : public BasePlayerController
{
public:
	void Awake() override;
	void Update() override;
	void Start() override;
	void LateUpdate() override;
	void FixedUpdate() override;

	void MoveTo();
	void OnMoveAccepted(uint64 serverMoveId, const Vec3& acceptedDestination, bool wasAdjusted);
	void OnMoveRejected(const Vec3& serverPosition, Protocol::MOVE_REJECT_REASON reason);
	void OnMovementSnapshotState(Protocol::MOVEMENT_SNAPSHOT_STATE state);
	void ConfirmSkillCooldown(int32 skillId);
	void SelectSkill(int32 skillId); // Hotkeys and UI share targeting/cancel behavior; 0 cancels.

	bool IsEnemy(shared_ptr<GameObject> obj);
	bool HasArrivedAtDestination();

	unordered_map<int, float>& GetCooldowns();

	virtual void ProcSkill(int32 skillId) override;

protected:
    void StopMovementForAction() override;

private:
	float _speed = 2.f;
    bool _legacyMoveActive = false;
	int32 _currentSkillID = 0;

	float _lastAttackTime = 0.0f;
	float _attackCooldown = 1.1f;

	float _lastWalkSoundTime = -999.f;
	const float WALK_SOUND_COOLDOWN = 1.75f;

public:
	Vec3 _dest;
	Vec3 _correctPosition;

private:
	void EnsureClickEffectModel();
	void EnsureSkillData();
	bool SendAuthoritativeMoveRequest(const Vec3& requestedDestination);
	void BeginAnnieWTargeting();
	bool UpdateAnnieWTargeting(int32 mouseX, int32 mouseY);
	void ConfirmAnnieWTarget(const Vec3& targetPosition);
	void TryCastPendingAnnieW();
	void CancelPendingAnnieW(bool hideIndicator);
	void UpdateAttackCursor(int32 mouseX, int32 mouseY);
	void SetAttackCursor(bool enabled);

	unordered_map<int, float> _skillElapsedTime;
	unordered_map<int, SkillData> skillDataTable;
	shared_ptr<Model> _clickEffectModel;
	weak_ptr<GameObject> _moveMarker;

	bool _annieWTargeting = false;
	bool _annieWPendingMove = false;
	Vec3 _annieWTargetPosition = Vec3::Zero;
	uint64 _annieWMoveId = 0;
	bool _attackCursorEnabled = false;

	static constexpr float AnnieWCastRange = AnnieSkillTuning::WRange;
	static constexpr float AnnieWConeAngleDegrees = AnnieSkillTuning::WConeAngleDegrees;
	static constexpr float AnnieWApproachMargin = 0.25f;
};
