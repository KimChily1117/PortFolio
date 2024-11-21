#include "pch.h"
#include "Actor.h"
#include "Component.h"
#include "BoxCollider.h"

Actor::Actor()
{

}

Actor::~Actor()
{
	for (Component* component : _components)
		SAFE_DELETE(component);
}
void Actor::BeginPlay()
{
	for (Component* component : _components)
	{
		component->BeginPlay();
	}
}

void Actor::Tick()
{
	for (Component* component : _components)
	{
		component->TickComponent();
	}
}

void Actor::Render(HDC hdc)
{
	for (Component* component : _components)
	{
		component->Render(hdc);
	}
}

void Actor::AddComponent(Component* component)
{
	if (component == nullptr)
		return;
	
	component->SetOwner(this);
	_components.push_back(component);
}

void Actor::RemoveComponent(Component* component)
{
	auto findIt = std::find(_components.begin(), _components.end(), component);
	if (findIt == _components.end())
		return;

	_components.erase(findIt);
}

Component* Actor::GetCollider()
{
	return nullptr;
}

void Actor::OnComponentBeginOverlap(Collider* collider, Collider* other)
{
	BoxCollider* b1 = dynamic_cast<BoxCollider*>(collider);
	BoxCollider* b2 = dynamic_cast<BoxCollider*>(other);
	//AdjustCollisionPos(b1, b2);

	
}

void Actor::OnComponentEndOverlap(Collider* collider, Collider* other)
{
}



//void Actor::AdjustCollisionPos(BoxCollider* b1, BoxCollider* b2)
//{
//	RECT r1 = b1->GetRect();
//	RECT r2 = b2->GetRect();
//
//	RECT intersect = {};
//
//	Vec2 pos = GetPos();
//
//	if (::IntersectRect(&intersect, &r1, &r2))
//	{
//		int32 w = intersect.right - intersect.left;
//		int32 h = intersect.bottom - intersect.top;
//
//		if (w > h)
//		{
//			if (intersect.top == r2.top)
//			{
//				pos.y -= h;
//			}
//			else
//			{
//				pos.y += h;
//			}
//		}
//
//		else
//		{
//			if (intersect.left == r2.left)
//			{
//				pos.x -= w;
//			}
//
//			else
//			{
//				pos.x += w;
//			}
//		}
//
//	}
//
//	SetPos(pos);
//}
