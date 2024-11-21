#pragma once
class Object
{
public:
	Object();
	Object(ObjectType objType);
	virtual ~Object();

public:
	virtual void Init() abstract;
	virtual void Update() abstract;
	virtual void Render(HDC hdc) abstract;
public:
	ObjectType GetObjType() { return _objType; }
	Pos GetPos() { return _pos; }
	void SetPos(Pos pos) { _pos = pos; }

protected:
	ObjectType _objType = ObjectType::None;
	Pos _pos = {};
};

