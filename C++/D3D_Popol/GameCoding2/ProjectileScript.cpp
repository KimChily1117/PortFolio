#include "pch.h"
#include "ProjectileScript.h"
#include "MeshRenderer.h"
#include "BindShaderDesc.h"

ProjectileScript::~ProjectileScript()
{
}

void ProjectileScript::Awake()
{
	_startPos = GetTransform()->GetPosition();
}

void ProjectileScript::SetTarget(const Vec3& targetPos, float speed)
{
	_startPos = GetTransform()->GetPosition();
	_targetPos = targetPos;
	_speed = speed;
	_direction = _targetPos - _startPos;

	if (_direction.Length() > 0)
	{
		_direction.Normalize();
		_isMoving = true;
	}

	// 회전 적용
	Vec3 flatDir = _direction;
	flatDir.y = 0.0f;

	if (flatDir.Length() > 0.01f)
	{
		flatDir.Normalize();
		float angle = atan2f(flatDir.x, flatDir.z) + XM_PI;
		GetTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), angle, 0.0f));
	}

	CreateTrail();
}

void ProjectileScript::Update()
{
	if (!_isMoving)
		return;

	Vec3 currentPos = GetTransform()->GetPosition();
	Vec3 moveDelta = _direction * _speed * TIME->GetDeltaTime();
	Vec3 nextPos = currentPos + moveDelta;

	float totalDistSqr = (_targetPos - _startPos).LengthSquared();
	float currentDistSqr = (nextPos - _startPos).LengthSquared();

	GetTransform()->SetPosition(nextPos);

	UpdateTrail(nextPos);

	// 도착 시 제거 처리
	if (currentDistSqr >= totalDistSqr)
	{
		GetTransform()->SetPosition(_targetPos);
		_isMoving = false;
		_hasHit = true;

		if (_trailObj)
		{
			CUR_SCENE->Remove(_trailObj);
			_trailObj = nullptr;
		}

		CUR_SCENE->Remove(GetGameObject());
	}
}

void ProjectileScript::CreateTrail()
{
	_trailObj = make_shared<GameObject>();
	_trailObj->AddComponent(make_shared<Transform>());
	_trailObj->AddComponent(make_shared<MeshRenderer>());

	_trailObj->GetMeshRenderer()->SetMaterial(RESOURCES->Get<Material>(L"Trail"));
	_trailObj->GetMeshRenderer()->SetMesh(RESOURCES->Get<Mesh>(L"Cube"));
	_trailObj->GetMeshRenderer()->SetPass(13);

	CUR_SCENE->Add(_trailObj);

	_trailObj->GetTransform()->SetPosition(GetTransform()->GetPosition());
	_trailObj->GetTransform()->SetScale(Vec3(0.f, 1.2f, 1.f));
}

void ProjectileScript::UpdateTrail(const Vec3& nextPos)
{
	if (!_trailObj) return;

	Vec3 dir = _direction;
	dir.y = 0.f;

	if (dir.Length() > 0.001f)
	{
		dir.Normalize();

		float length = (_startPos - nextPos).Length();
		float offset = length * 0.4f;

		Vec3 trailPos = nextPos - dir * offset;
		trailPos.y = 1.9f;

		_trailObj->GetTransform()->SetPosition(trailPos);

		float angleY = atan2f(dir.x, dir.z) + XMConvertToRadians(90.f);
		_trailObj->GetTransform()->SetRotation(Vec3(0.f, angleY, 0.f));
		_trailObj->GetTransform()->SetScale(Vec3(length + 0.2f, 0.9f, 1.f));

		float t = length / (_targetPos - _startPos).Length();
		float alpha = (t > 0.3f) ? lerp(1.f, 0.f, (t - 0.3f) / 0.7f) : 1.f;

		auto mat = _trailObj->GetMeshRenderer()->GetMaterial();
		mat->GetMaterialDesc().diffuse = Color(1.f, 0.f, 0.f, 1);
		mat->Update();
	}
}
