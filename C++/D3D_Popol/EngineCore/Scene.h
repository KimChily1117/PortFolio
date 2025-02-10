#pragma once
class GameObject;

class Scene
{
public:
	virtual void Start();
	virtual void Update();
	virtual void LateUpdate();

	virtual void Add(shared_ptr<GameObject> object);
	virtual void Remove(shared_ptr<GameObject> object);


	unordered_set<shared_ptr<GameObject>> GetObjects() { return _objects; }
	shared_ptr<GameObject> GetCamera() { return _cameras.empty() ? nullptr : *_cameras.begin(); }
	shared_ptr<GameObject> GetLight() { return _lights.empty() ? nullptr : *_lights.begin(); }


	shared_ptr<class GameObject> Pick(int32 screenX, int32 screenY);
	shared_ptr<class GameObject> Pick(int32 screenX, int32 screenY , Vec3& pickPos);
	shared_ptr<class GameObject> PickMesh(int32 screenX, int32 screenY , Vec3& pickPos);


private:
	// unordered_set은 속도와 중복방지를 하기위해 선언함
	unordered_set<shared_ptr<GameObject>> _objects;
	// Cache Camera
	unordered_set<shared_ptr<GameObject>> _cameras;
	// Cache Light
	unordered_set<shared_ptr<GameObject>> _lights;
};
	


