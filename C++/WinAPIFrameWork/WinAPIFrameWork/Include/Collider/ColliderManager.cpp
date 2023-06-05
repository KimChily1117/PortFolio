#include "ColliderManager.h"
#include "../Object/Obj.h"
#include "Collider.h"

DEFINITION_SINGLE(CColliderManager)

CColliderManager::CColliderManager()
{
}


CColliderManager::~CColliderManager()
{
}

void CColliderManager::AddObject(CObj * pObj)
{
	if (pObj->CheckCollider()) //충돌체(콜라이더)를 가지고있을때
	{
		m_CollisionList.push_back(pObj); //리스트에 넣어줌
	}	
}

void CColliderManager::Collision(float fDeltaTime)
{
	if (m_CollisionList.size() < 2) // 충돌할 오브젝트가 2개밑 == 아무것도 없다
		// 그럼 계속 clear해주고 갱신 시켜줘야한다 (매프레임마다)
	{
		m_CollisionList.clear();
		return;
	}

	//오브젝트간 충돌 처리를 여기다가 해준다. 
	list<CObj*>::iterator	iter;
	list<CObj*>::iterator	iterEnd = m_CollisionList.end();

	--iterEnd;

	for (iter = m_CollisionList.begin(); iter!= iterEnd; ++iter)
	{
		list<CObj*>::iterator	iter1 = iter;
		++iter1;
		list<CObj*>::iterator	iter1End = m_CollisionList.end();
		for (; iter1!= iter1End; ++iter1)
		{
			Collision(*iter,*iter1, fDeltaTime);
		}
	}	

	m_CollisionList.clear();
}

bool CColliderManager::Collision(CObj * pSrc, CObj * pDest,
	float fDeltaTime)

{
	const list<class CCollider*>* pSrcList = pSrc->GetColliderList();
	const list<class CCollider*>* pDestList = pDest->GetColliderList();

	list<CCollider*>::const_iterator	iterSrc;
	list<CCollider*>::const_iterator	iterSrcEnd = pSrcList->end();


	list<CCollider*>::const_iterator	iterDest;
	list<CCollider*>::const_iterator	iterDestEnd = pDestList->end();

	bool	bCollision = false;

 	for (iterSrc = pSrcList->begin(); iterSrc != iterSrcEnd; ++iterSrc)
	{

		for (iterDest = pDestList->begin(); iterDest != iterDestEnd; ++iterDest)
		{
			if ((*iterSrc)->Collision(*iterDest))
			{
				bCollision = true;
				
				//층돌목록에서 이전에 충돌된적이 없다면
				//처음 막 충돌되었다는 의미이다. 
				if(!(*iterSrc)->CheckCollisionList(*iterDest))
				{
					//서로 상대방을 충돌 목록으로 추가한다.
					(*iterSrc)->AddCollider(*iterDest);
					(*iterDest)->AddCollider(*iterSrc);				
				
					(*iterSrc)->CallFunction(CS_ENTER,*iterDest,fDeltaTime);
					(*iterDest)->CallFunction(CS_ENTER,*iterSrc,fDeltaTime);
				}
				
				// 기존 충돌된적이 있다면 계속 충돌 상태임.
				else
				{
					(*iterSrc)->CallFunction(CS_STAY,*iterDest,fDeltaTime);
					(*iterDest)->CallFunction(CS_STAY,*iterSrc,fDeltaTime);
				}
			}



			// 현재 충돌이 안된 상태에서 이전에 충돌이 되고있었다면
			// 이제 막 충돌상태에서 떨어졌다는 의미이다
			else if ((*iterSrc)->CheckCollisionList(*iterDest)) //여긴 충돌이 안되었을때
			{
				//서로 충돌이 안되므로 충돌목록에서 지워준다.
				(*iterSrc)->EraseCollisionList(*iterDest);
				(*iterDest)->EraseCollisionList(*iterSrc);

				(*iterSrc)->CallFunction(CS_LEAVE,*iterDest, fDeltaTime);
				(*iterDest)->CallFunction(CS_LEAVE,*iterSrc, fDeltaTime);
			}
		}
	} 
	return bCollision;
}
