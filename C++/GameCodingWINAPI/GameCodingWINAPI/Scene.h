#pragma once
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
};

