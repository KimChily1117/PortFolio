#pragma once
#include "../Game.h"


class CColliderManager
{
private:
		list<class CObj*>	m_CollisionList;
		DECLARE_SINGLE(CColliderManager)

public:
	void AddObject(class CObj* pObj);
	void Collision(float fDeltaTime);
	bool Collision(class CObj* pSrc, class CObj* pDest, float fDeltaTime);
};

