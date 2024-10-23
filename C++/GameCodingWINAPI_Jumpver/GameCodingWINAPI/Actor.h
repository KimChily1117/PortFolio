#pragma once

class Component;
class Collider;
class BoxCollider;


class Actor
{
public:
	Actor();
	virtual ~Actor();

public:

	virtual	void BeginPlay(); 
	virtual void Tick();
	virtual void Render(HDC hdc);


	void SetPos(Vec2 pos) { _pos = pos; }
	Vec2 GetPos() { return _pos; }

	void AddComponent(Component* component);
	void RemoveComponent(Component* component);

	void SetLayer(LAYER_TYPE layerType) { _layerType = layerType; }
	LAYER_TYPE GetLayer() { return _layerType; }


	// OnCollisionEnter2D / OnCollisionExit2D
	virtual void OnComponentBeginOverlap(Collider* collider, Collider* other);
	virtual void OnComponentEndOverlap(Collider* collider, Collider* other);

private:
	void AdjustCollisionPos(BoxCollider* b1 , BoxCollider* b2);



protected:
	Vec2 _pos = { 0, 0 };
	vector<Component*> _components;
	LAYER_TYPE _layerType = LAYER_MAXCOUNT;
};

