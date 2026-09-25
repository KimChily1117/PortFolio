#include "pch.h"
#include "BasePlayerController.h"
#include "ModelAnimator.h"

bool BasePlayerController::IsActionBusy() const
{
    return _isAttackMode || TIME->GetGameTime() < _pendingActionDeadline;
}

bool BasePlayerController::IsMovementLocked() const
{
    if (TIME->GetGameTime() < _pendingActionDeadline) return true;
    return _isAttackMode && !(_playerInfo &&
        _playerInfo->champtype() == Protocol::PLAYER_TYPE_GAREN && _currentState == PlayerState::E);
}

void BasePlayerController::StopMovementForAction()
{
    _authoritativeFrom = _authoritativeTarget = GetTransform()->GetPosition();
    _authoritativeInterpolationElapsed = _authoritativeInterpolationDuration;
    _authoritativeState = Protocol::MOVEMENT_SNAPSHOT_STATE_CANCELLED;
}

void BasePlayerController::HoldMovementForSkillRequest()
{
    // Annie W waits for server approval. Bound the wait if a request is rejected.
    _pendingActionDeadline = TIME->GetGameTime() + 2.f;
    StopMovementForAction();
}

void BasePlayerController::BeginActionAnimation()
{
    _pendingActionDeadline = 0.f;
    _isAttackMode = true;
    auto animator = GetGameObject()->GetModelAnimator();
    float duration = animator ? animator->GetAnimationDuration((int32)_currentState) : 1.f;
    if (!std::isfinite(duration) || duration <= .01f) duration = 1.f;
    _timeToIdle = TIME->GetGameTime() + duration;
    if (IsMovementLocked()) StopMovementForAction();
}

void BasePlayerController::PlayServerSkillResult(int32 skillId, const Vec3& castOrigin, const Vec3& castDirection)
{
	_serverSkillCastOrigin = castOrigin;
	_serverSkillCastDirection = castDirection;
	if (castDirection.LengthSquared() > 0.0001f)
	{
		direction = castDirection;
		AlignToDirection(castDirection);
	}
	ProcSkill(skillId);
}

void BasePlayerController::Awake()
{
}

void BasePlayerController::Update()
{
	UpdateAuthoritativeInterpolation();
}

bool BasePlayerController::ApplyAuthoritativeSnapshot(uint64 serverMoveId, uint64 serverTick, const Vec3& position, Protocol::MOVEMENT_SNAPSHOT_STATE state)
{
	if (!IsFinitePosition(position) || serverMoveId == 0 || state == Protocol::MOVEMENT_SNAPSHOT_STATE_UNKNOWN)
		return false;
	if (serverMoveId < _latestAuthoritativeMoveId)
		return false;
	if (serverMoveId == _latestAuthoritativeMoveId && _hasAuthoritativeSnapshot)
    {
        // A cast can cancel a path in the same server tick as its last MOVING snapshot.
        const bool terminal = state == Protocol::MOVEMENT_SNAPSHOT_STATE_CANCELLED ||
            state == Protocol::MOVEMENT_SNAPSHOT_STATE_ARRIVED;
        if (serverTick < _latestAuthoritativeServerTick ||
            (serverTick == _latestAuthoritativeServerTick &&
                !(_lastSnapshotState == Protocol::MOVEMENT_SNAPSHOT_STATE_MOVING && terminal)))
            return false;
        if (_lastSnapshotState != Protocol::MOVEMENT_SNAPSHOT_STATE_MOVING &&
            state == Protocol::MOVEMENT_SNAPSHOT_STATE_MOVING)
            return false; // Completed paths cannot be resurrected by a late snapshot.
    }

	if (serverMoveId > _latestAuthoritativeMoveId)
		_latestAuthoritativeServerTick = 0;
	_latestAuthoritativeMoveId = serverMoveId;
	_latestAuthoritativeServerTick = serverTick;
	_hasAuthoritativeSnapshotStream = true;
	_hasAuthoritativeSnapshot = true;
	_lastSnapshotState = state;
    _authoritativeState = state;
    if (IsMovementLocked() && state == Protocol::MOVEMENT_SNAPSHOT_STATE_MOVING)
    {
        StopMovementForAction();
        return true;
    }

    Vec3 current = GetTransform()->GetPosition();
	Vec3 delta = position - current;
	const float distanceSquared = delta.LengthSquared();
	if (state == Protocol::MOVEMENT_SNAPSHOT_STATE_ARRIVED || state == Protocol::MOVEMENT_SNAPSHOT_STATE_CANCELLED || distanceSquared > 9.0f)
	{
		GetTransform()->SetPosition(position);
		_authoritativeFrom = position;
		_authoritativeTarget = position;
		_authoritativeInterpolationElapsed = _authoritativeInterpolationDuration;
	}
	else
	{
		_authoritativeFrom = current;
		_authoritativeTarget = position;
		_authoritativeInterpolationElapsed = 0.f;
	}

	if (!IsMovementLocked() && delta.LengthSquared() > 0.0001f)
		AlignToDirection(delta);
	SetMovementAnimation(state);
	return true;
}

void BasePlayerController::SetMinimumAuthoritativeMoveId(uint64 serverMoveId)
{
	if (serverMoveId > _latestAuthoritativeMoveId)
	{
		_latestAuthoritativeMoveId = serverMoveId;
		_latestAuthoritativeServerTick = 0;
		_hasAuthoritativeSnapshot = false;
	}
	_hasAuthoritativeSnapshotStream = true;
}

void BasePlayerController::UpdateAuthoritativeInterpolation()
{
	if (IsMovementLocked() || !_hasAuthoritativeSnapshotStream || !_hasAuthoritativeSnapshot)
		return;
	if (_authoritativeInterpolationElapsed >= _authoritativeInterpolationDuration)
		return;

	float deltaTime = TIME->GetDeltaTime();
	if (!std::isfinite(deltaTime) || deltaTime <= 0.f)
		return;
	_authoritativeInterpolationElapsed = min(_authoritativeInterpolationElapsed + deltaTime, _authoritativeInterpolationDuration);
	float t = _authoritativeInterpolationDuration <= 0.f ? 1.f : _authoritativeInterpolationElapsed / _authoritativeInterpolationDuration;
	GetTransform()->SetPosition(Vec3::Lerp(_authoritativeFrom, _authoritativeTarget, t));
}

void BasePlayerController::SetMovementAnimation(Protocol::MOVEMENT_SNAPSHOT_STATE state)
{
	if (_isAttackMode || _currentState == PlayerState::DIE)
		return;
	PlayerState nextState = state == Protocol::MOVEMENT_SNAPSHOT_STATE_MOVING ? PlayerState::RUN : PlayerState::IDLE;
	if (_currentState == nextState)
		return;
	_currentState = nextState;
	if (GetGameObject()->GetModelAnimator())
		GetGameObject()->GetModelAnimator()->SetAnimation((int32)_currentState, true);
}

void BasePlayerController::FinishActionAnimation()
{
    _pendingActionDeadline = 0.f;
    _isAttackMode = false;
    if (_currentState == PlayerState::DIE || (_playerInfo && _playerInfo->hp() <= 0)) return;
	_currentState = IsAuthoritativeMoving() ? PlayerState::RUN : PlayerState::IDLE;
	if (GetGameObject()->GetModelAnimator())
		GetGameObject()->GetModelAnimator()->SetAnimation((int32)_currentState, true);
}

bool BasePlayerController::IsFinitePosition(const Vec3& position) const
{
	return std::isfinite(position.x) && std::isfinite(position.y) && std::isfinite(position.z);
}

void BasePlayerController::AlignToDirection(const Vec3& direction)
{
	if (direction.Length() > 0.01f)
	{
		Vec3 normalizedDir = direction;
		normalizedDir.y = 0.0f;
		normalizedDir.Normalize();

		float angle = atan2f(normalizedDir.x, normalizedDir.z) + XM_PI;
		GetTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), angle, 0.0f));
		DEBUG_LOG("[Client] Rotated to Face Direction");
	}
}
