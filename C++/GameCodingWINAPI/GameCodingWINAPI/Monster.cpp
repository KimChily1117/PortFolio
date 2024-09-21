#include "pch.h"
#include "Monster.h"

Monster::Monster() : Object(ObjectType::Monster)
{

}

Monster::~Monster()
{

}
void Monster::Init()
{
	_stat.hp = 100;
	_stat.maxHp = 100;
	_stat.speed = 10;
}

void Monster::Update()
{

}

void Monster::Render(HDC hdc)
{
	Utils::DrawRect(hdc, _pos, 50, 50);
}

