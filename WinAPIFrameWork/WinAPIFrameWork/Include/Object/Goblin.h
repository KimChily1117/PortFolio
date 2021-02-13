#pragma once
#include "MoveObj.h"


class CGoblin :
	public CMoveObj
{

private:
	friend class CObj;
private:
	CGoblin();
	CGoblin(const CGoblin& goblin);
	~CGoblin();
private:
	MOVE_DIR	m_eDir;
	float		m_fFireTime;
	float		m_fFireLimitTime;

public:
	virtual bool Init();
	virtual int Update(float fDeltaTime);
	virtual int LateUpdate(float fDeltaTime);
	virtual void Collision(float fDeltaTime);
	virtual void Render(HDC hDC, float fDeltaTime);
	virtual CGoblin* Clone();

public:
	void	CollisionBullet(class CCollider* pSrc,
		class CCollider* pDest, float fDeltaTime);
private:
	void Fire();


};

