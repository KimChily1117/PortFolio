#pragma once

class Actor;
class UI;

class Scene
{
public:
	Scene()
	{
	}
	virtual ~Scene()
	{

	}
public:
	virtual void Init() abstract;
	virtual void Update() abstract;
	virtual void Render(HDC hdc) abstract;
protected:
	vector<Actor*> _actors;
	vector<UI*> _uis;

};

