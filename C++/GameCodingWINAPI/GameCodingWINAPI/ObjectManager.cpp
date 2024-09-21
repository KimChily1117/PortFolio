#include "pch.h"
#include "ObjectManager.h"
#include "Object.h"

ObjectManager::~ObjectManager()
{
}

void ObjectManager::Add(Object* obj)
{
	if (obj == nullptr)
	{
		return;
	}

	auto findIt = std::find(_objects.begin(), _objects.end(), obj);

	if (findIt != _objects.end())
		return;
	// 이미 존재하니깐 return

	
	_objects.push_back(obj);

}

void ObjectManager::Remove(Object* obj)
{
	if (obj == nullptr)
		return;

	auto it = std::find(_objects.begin(), _objects.end(), obj);

	_objects.erase(it, _objects.end());

	delete obj;

}

void ObjectManager::Clear()
{
	std::for_each(_objects.begin(), _objects.end(), [=](Object* obj) { delete obj; });
	_objects.clear();
}
