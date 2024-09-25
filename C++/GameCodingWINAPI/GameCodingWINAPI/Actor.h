#pragma once
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


protected:
	Vec2 _pos = { 0,0 };

};

