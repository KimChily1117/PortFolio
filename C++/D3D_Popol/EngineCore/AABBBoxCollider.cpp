#include "pch.h"
#include "AABBBoxCollider.h"
#include "SphereCollider.h"
#include "OBBBoxCollider.h"

AABBBoxCollider::AABBBoxCollider() : BaseCollider(ColliderType::AABB)
{

}

AABBBoxCollider::~AABBBoxCollider()
{

}

void AABBBoxCollider::Update()
{
	// Todo Box의 사이즈를 검출해서 박스끼리 부딫히게 만들어야하는거아니야?
}

bool AABBBoxCollider::Intersects(Ray& ray, OUT float& distance)
{
	return _boundingBox.Intersects(ray.position, ray.direction, OUT distance);

}

bool AABBBoxCollider::Intersects(shared_ptr<BaseCollider>& other)
{
	ColliderType type = other->GetColliderType();

	switch (type)
	{
	case ColliderType::Sphere:
		return _boundingBox.Intersects(dynamic_pointer_cast<SphereCollider>(other)->GetBoundingSphere());
	case ColliderType::AABB:
		return _boundingBox.Intersects(dynamic_pointer_cast<AABBBoxCollider>(other)->GetBoundingBox());
	case ColliderType::OBB:
		return _boundingBox.Intersects(dynamic_pointer_cast<OBBBoxCollider>(other)->GetBoundingBox());
	}
}
