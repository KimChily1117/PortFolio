#pragma once

class GameObject;

class ObjectManager
{
	DECLARE_SINGLE(ObjectManager)

public:
	template<typename T>
	T* AddObject()
	{
		T* object = new T();

		int64 id = _idGenerator++;
		object->SetObjectID(id);
		_objects[id] = object;

		return object;
	}

	void RemoveObject(int64 id)
	{
		auto findIt = _objects.find(id);
		if (findIt == _objects.end())
			return;

		_objects.erase(id);
		// TODO : Delete?
	}

private:
	int64 _idGenerator = 1;
	unordered_map<int64, GameObject*> _objects;
};

