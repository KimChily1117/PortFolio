#pragma once
class GameObject;

class Scene
{
public:
	virtual void Awake();
	virtual void Start();
	virtual void Update();
	virtual void LateUpdate();


	virtual void Render();
	virtual void GUIRender();


	virtual void Add(shared_ptr<GameObject> object);
	virtual void Remove(shared_ptr<GameObject> object);


	unordered_set<shared_ptr<GameObject>>& GetObjects() { return _objects; }
	//shared_ptr<GameObject> GetCamera() { return _cameras.empty() ? nullptr : *_cameras.begin(); }

	shared_ptr<GameObject> GetMainCamera();
	shared_ptr<GameObject> GetUICamera();
	shared_ptr<GameObject> GetLight() { return _lights.empty() ? nullptr : *_lights.begin(); }

	shared_ptr<GameObject> GetTerrainObj() { return _terrain; }
	void PickUI();

	shared_ptr<class GameObject> Pick(int32 screenX, int32 screenY);
	shared_ptr<class GameObject> Pick(int32 screenX, int32 screenY, Vec3& pickPos);
	//shared_ptr<class GameObject> PickMesh(int32 screenX, int32 screenY , Vec3& pickPos);



	void RegisterObject(uint64 objectId, shared_ptr<GameObject> obj)
	{
		_players[objectId] = obj;
	}

	shared_ptr<GameObject> FindObjectById(uint64 objectId)
	{
		auto it = _players.find(objectId);
		if (it != _players.end())
			return it->second;
		return nullptr;
	}

	void RemoveObject(uint64 objectId)
	{
		auto removeGo = _players[objectId];

		if (removeGo)
		{
			Remove(removeGo);
		}
		_players.erase(objectId);
		
	}

	shared_ptr<Shader> _shader;

	shared_ptr<GameObject> _terrain;

private:
	// unordered_set�� �ӵ��� �ߺ������� �ϱ����� ������
	unordered_set<shared_ptr<GameObject>> _objects;
	// Cache Camera
	unordered_set<shared_ptr<GameObject>> _cameras;
	// Cache Light
	unordered_set<shared_ptr<GameObject>> _lights;



public:
	unordered_map<uint64, shared_ptr<GameObject>> _players; // ObjectId ��� �����

private:
	vector<shared_ptr<GameObject>> _newObjects; // ���� �߰��� ������Ʈ ���
	bool _isStartCalled = false;



};



