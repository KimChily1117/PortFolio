#include "pch.h"
#include "BaseCollider.h"

BaseCollider::BaseCollider(ColliderType colliderType)
	: Component(ComponentType::Script) ,_colliderType(colliderType)
{

}

BaseCollider::~BaseCollider()
{

}

