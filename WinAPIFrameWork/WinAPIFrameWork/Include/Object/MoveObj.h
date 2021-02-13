#pragma once
#include "Obj.h"

class CMoveObj : 
	public CObj
{
protected:	
	CMoveObj();
	CMoveObj(const CMoveObj& obj);	
	virtual ~CMoveObj();
protected:
	float		m_fAngle;
	float		m_fSpeed;
public:
	void SetAngle(float fAngle)
	{
		m_fAngle = fAngle;
	}
	void SetSpeed(float fSpeed)
	{
		m_fSpeed = fSpeed;
	}

	float GetAngle()	const
	{
		return m_fAngle;
	}

	float GetSpeed()	const
	{
		return m_fSpeed;
	}

public:
	void Move(float x, float y);
	void Move(float x, float y , float fDeltaTime);
	void Move(const POSITION& tMove);
	void Move(const POSITION& tMove , float fDeltaTime);
	void MoveX(float x);
	void MoveXFromSpeed(float fDeltaTime, MOVE_DIR eDir);
	void MoveX(float x , float fDeltaTime);
	void MoveY(float y);
	void MoveYFromSpeed(float fDeltaTime, MOVE_DIR eDir);
	void MoveY(float y, float fDeltaTime);
	void MoveAngle();
	void MoveAngle(float fDeltaTime);


public:
	virtual bool Init() = 0;
	virtual void Input(float fDeltaTime);
	virtual int Update(float fDeltaTime);
	virtual int LateUpdate(float fDeltaTime);
	virtual void Collision(float fDeltaTime);
	virtual void Render(HDC hDC, float fDeltaTime);
	virtual CMoveObj* Clone() = 0;
};

