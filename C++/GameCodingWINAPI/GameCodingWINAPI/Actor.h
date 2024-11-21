#pragma once

class Component;
class Collider;

class Actor
{
public:
	Actor();
	virtual ~Actor();

	virtual void BeginPlay();
	virtual void Tick();
	virtual void Render(HDC hdc);

	void SetPos(Vec2 pos) { _pos = pos; }
	Vec2 GetPos() { return _pos; }

	void SetLayer(LAYER_TYPE layer) { _layer = layer; }
	LAYER_TYPE GetLayer() { return _layer; }

	void AddComponent(Component* component);
	void RemoveComponent(Component* component);

	Component* GetCollider();

	// OnCollisionEnter2D / OnCollisionExit2D
	virtual void OnComponentBeginOverlap(Collider* collider, Collider* other);
	virtual void OnComponentEndOverlap(Collider* collider, Collider* other);

protected:
	Vec2 _pos = { 0, 0 }; // 이건 계산을 위해서 만든 position이야
	Vec2 _destPos = { 0, 0 }; // 도착지점 ( 최종 계산이 마친 위치)
	LAYER_TYPE _layer = LAYER_OBJECT;
	vector<Component*> _components;
};

