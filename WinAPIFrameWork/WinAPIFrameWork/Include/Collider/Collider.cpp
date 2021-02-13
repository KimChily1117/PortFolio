#include "Collider.h"



CCollider::CCollider()
{
}

CCollider::CCollider(const CCollider & coll) :
	CRef(coll)
{
	*this = coll;
}


 CCollider::~CCollider()
{
		list<CCollider*>::iterator	iter;
		list<CCollider*>::iterator	iterEnd = m_CollisionList.end();

		for(iter = m_CollisionList.begin(); iter!=iterEnd; ++iter)
		{
				(*iter)->EraseCollisionList(this);
		}
}


 void CCollider::Input(float fDeltaTime)
 {

 }

 int CCollider::Update(float fDeltaTime)
 {
	 return 0;
 }

 int CCollider::LateUpdate(float fDeltaTime)
 {		
	 return 0;
 }

 bool CCollider::Collision(CCollider* pDest)
 {
	 return false;
 }

 void CCollider::Render(HDC hDC, float fDeltaTime)
 {
 }

 bool CCollider::CollisionRectToRect(const RECTANGLE & src, const RECTANGLE & dest)
 {
	 if (src.l > dest.r)
	 	 return false;
	 
	 else if (src.r < dest.l)
	 	 return false;
	
	 else if (src.t > dest.b)
	 {
		 return false;
	 }
	 else if (src.b < dest.t)
	 {
		 return false;
	 }

	 return true;
 }
