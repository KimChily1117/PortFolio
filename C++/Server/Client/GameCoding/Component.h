#pragma once

class Actor;

class Component : public enable_shared_from_this<Component>
{
public:
	Component();
	virtual ~Component();

	virtual void BeginPlay() {}
	virtual void TickComponent() {}
	virtual void Render(HDC hdc) { }

	void SetOwner(Actor* owner) { _owner = owner; }
	Actor* GetOwner() { return _owner; }

protected:
	Actor* _owner;
};

