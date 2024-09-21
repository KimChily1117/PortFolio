#include "pch.h"
#include "Missile.h"
#include "TimeManager.h"
#include "ObjectManager.h"


Missile::Missile() : Object(ObjectType::Projectile)
{

}

Missile::~Missile()
{

}

void Missile::Init()
{
	_stat.hp = 1;
	_stat.maxHp = 1;
	_stat.speed = 600;
}

void Missile::Update()
{
	float deltaTime = GET_SINGLE(TimeManager)->GetDeltaTime();	
	if (_target == nullptr)
	{
		//_pos.y -= deltaTime * _stat.speed;
		_pos.x += _stat.speed * deltaTime * ::cos(_angle);
		_pos.y -= _stat.speed * deltaTime * ::sin(_angle);

		// 0.2ÃÊ ÈÄ¿¡ À¯µµÅº
		_sumTime += deltaTime;
		if (_sumTime >= 0.2f)
		{
			// Ã¹¹øÂ° Å¸°Ù ¼­Ä¡
			const vector<Object*>& objects = GET_SINGLE(ObjectManager)->GetObjects();
			for (Object* object : objects)
			{
				if (object->GetObjType() == ObjectType::Monster)
				{
					_target = object;
					break;
				}
			}
		}
	}
	else
	{
		Vector dir = _target->GetPos() - GetPos();
		dir.Normalize();

		// µÇ³ª?
		_target->SetPos(_target->GetPos());

		_pos += dir * deltaTime * _stat.speed;
	}

	// Ãæµ¹
	const vector<Object*> objects = GET_SINGLE(ObjectManager)->GetObjects();
	for (Object* object : objects)
	{
		if (object == this)
			continue;

		if (object->GetObjType() != ObjectType::Monster)
			continue;

		Pos p1 = GetPos();
		Pos p2 = object->GetPos();

		const float dx = p1.x - p2.x;
		const float dy = p1.y - p2.y;
		float dist = sqrt(dx * dx + dy * dy);

		if (dist < 25)
		{
			GET_SINGLE(ObjectManager)->Remove(object); // ±¦ÂúÀ»..±î?
			GET_SINGLE(ObjectManager)->Remove(this); // ±¦ÂúÀ»..±î?
			return;
		}
	}

	// TODO
	if (_pos.y < -1000)
	{
		GET_SINGLE(ObjectManager)->Remove(this);
		return;
	}
}

void Missile::Render(HDC hdc)
{
	Utils::DrawCircle(hdc, _pos, 25);

}
