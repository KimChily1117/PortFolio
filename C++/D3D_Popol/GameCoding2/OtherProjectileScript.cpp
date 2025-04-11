#include "pch.h"
#include "OtherProjectileScript.h"
#include "MeshRenderer.h"

void OtherProjectileScript::Awake()
{
	_startPos = GetTransform()->GetPosition();
}

void OtherProjectileScript::SetTarget(const Vec3& targetPos, float speed)
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

	CreateTrail(); // 트레일 생성
}

void OtherProjectileScript::Update()
{
	if (!_isMoving)
		return;

	Vec3 currentPos = GetTransform()->GetPosition();
	Vec3 moveDelta = _direction * _speed * TIME->GetDeltaTime();
	Vec3 nextPos = currentPos + moveDelta;

	float totalDistSqr = (_targetPos - _startPos).LengthSquared();
	float currentDistSqr = (nextPos - _startPos).LengthSquared();

	GetTransform()->SetPosition(nextPos);

	UpdateTrail(nextPos); // 트레일 위치/길이 보정

	// 도착 판정
	if (currentDistSqr >= totalDistSqr * 0.61f)
	{
		GetTransform()->SetPosition(_targetPos);
		_isMoving = false;

		if (_trailObj)
		{
			CUR_SCENE->Remove(_trailObj);
			_trailObj = nullptr;
		}
	}
}

void OtherProjectileScript::CreateTrail()
{
	_trailObj = make_shared<GameObject>("Trail_Other");
	_trailObj->AddComponent(make_shared<Transform>());
	_trailObj->AddComponent(make_shared<MeshRenderer>());

	_trailObj->GetMeshRenderer()->SetMaterial(RESOURCES->Get<Material>(L"Trail"));
	_trailObj->GetMeshRenderer()->SetMesh(RESOURCES->Get<Mesh>(L"Cube"));
	_trailObj->GetMeshRenderer()->SetPass(13); // 👉 ProjectileScript와 동일한 패스 번호 사용

	CUR_SCENE->Add(_trailObj);

	_trailObj->GetTransform()->SetPosition(GetTransform()->GetPosition());
	_trailObj->GetTransform()->SetScale(Vec3(0.f, 1.2f, 1.f));
}

void OtherProjectileScript::UpdateTrail(const Vec3& nextPos)
{
	if (!_trailObj) return;

	Vec3 dir = _direction;
	dir.y = 0.f;

	if (dir.Length() > 0.001f)
	{
		dir.Normalize();

		// ✅ 길이 + 보정 오프셋
		float length = (_startPos - nextPos).Length();
		float offset = length * 0.4f;

		Vec3 trailPos = nextPos - dir * offset;
		trailPos.y = 1.9f;

		_trailObj->GetTransform()->SetPosition(trailPos);
		_trailObj->GetTransform()->SetRotation(Vec3(0.f, atan2f(dir.x, dir.z) + XMConvertToRadians(90.f), 0.f));
		_trailObj->GetTransform()->SetScale(Vec3(length + 0.2f, 0.9f, 1.f));

		// ✅ 알파 보간
		float t = length / (_targetPos - _startPos).Length();
		float alpha = (t > 0.3f) ? lerp(1.f, 0.f, (t - 0.3f) / 0.7f) : 1.f;
		auto mat = _trailObj->GetMeshRenderer()->GetMaterial();
		mat->GetMaterialDesc().diffuse = Color(1.f, 0.f, 0.f, 1);
		mat->Update();
	
		
	}
}
