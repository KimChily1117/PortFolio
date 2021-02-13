#pragma once
#include "MoveObj.h"
class CBullet :
	public CMoveObj
{
private:
	friend class CObj;
	friend class CScene;

private:
	CBullet();
	CBullet(const CBullet& bullet);
	~CBullet();
private:
	// 각도로 방향 결정
	float	m_fLimitDist;
	float	m_fDist;
	

public:
	virtual bool Init();
	virtual int Update(float fDeltaTime);
	virtual int LateUpdate(float fDeltaTime);
	virtual void Collision(float fDeltaTime);
	virtual void Render(HDC hDC, float fDeltaTime);
	virtual CBullet* Clone();

public:
	void SetBulletDistance(float fDist)
	{
		m_fLimitDist = fDist;
	}

public:
	void Hit(class CCollider* pSrc,
		class CCollider* pDest, float fDeltaTime);
};






