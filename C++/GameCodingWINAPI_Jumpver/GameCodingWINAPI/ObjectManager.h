#pragma once

class Object;

class ObjectManager
{
public:
	DECLARE_SINGLE(ObjectManager);
	~ObjectManager();

	void Add(Object* obj);
	void Remove(Object* obj);
	void Clear();


	const vector<Object*>& GetObjects() { return _objects;  }

	template <typename T>
	T* CreateObject()
	{
		// type trait
		static_assert(std::is_convertible_v<T*, Object*>);


		T* object = new T();
		object->Init();
		return object;
	}


private:
	vector<Object*> _objects;
};

