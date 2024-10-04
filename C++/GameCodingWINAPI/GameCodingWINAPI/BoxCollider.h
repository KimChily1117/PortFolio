#pragma once
#include "Collider.h"


class BoxCollider : public Collider
{
	using Super = Collider;

public:
	BoxCollider();
	virtual ~BoxCollider() override;

	virtual void BeginPlay() override;
	virtual void TickComponent() override;
	virtual void Render(HDC hdc) override;

	virtual bool CheckCollision(Collider* other) override;

	Vec2 GetSize() { return _size; }
	void SetSize(Vec2 size) { _size = size; }

private:
	Vec2 _size = {};
};
