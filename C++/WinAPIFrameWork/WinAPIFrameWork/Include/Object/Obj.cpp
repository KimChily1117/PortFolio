#include "Obj.h"
#include "../Scene/Layer.h"
#include "../Scene/SceneManager.h"
#include "../Scene/Scene.h"
#include "../Core/Camera.h"



list<CObj*> CObj::m_ObjList;

CObj::CObj():
	m_pTexture(NULL),
	m_pAnimation(NULL)
{
}


CObj::CObj(const CObj& obj)
{
	*this = obj;

	if (obj.m_pAnimation)
	{
		m_pAnimation = obj.m_pAnimation;
	}

	if (m_pTexture)
	{
		m_pTexture->AddRef();
	}

	m_ColliderList.clear();

	list<CCollider*>::const_iterator	iter;
	list<CCollider*>::const_iterator	iterEnd = obj.m_ColliderList.end();

	for (iter = obj.m_ColliderList.begin(); iter !=iterEnd; ++iter)
	{
		CCollider* pColl = (*iter)->Clone();

		pColl->SetObj(this);

		m_ColliderList.push_back(pColl);
	}

}

CObj::~CObj()       
{
	Safe_Release_VecList(m_ColliderList);
	SAFE_RELEASE(m_pTexture);
	SAFE_RELEASE(m_pAnimation);
}

void CObj::AddObj(CObj * pObj)
{
	pObj->AddRef();
	m_ObjList.push_back(pObj);
}

CObj * CObj::FindObject(const string & strTag)
{
	list<CObj*>::iterator	iter;
	list<CObj*>::iterator	iterEnd = m_ObjList.end();

	for (iter = m_ObjList.begin(); iter != iterEnd; ++iter)
	{
		if ((*iter)->GetTag() == strTag)
		{
			(*iter)->AddRef(); // 참조 횟수 1개 올려줌 
			return *iter;
		}
	}

	return NULL;
}

void CObj::EraseObj(CObj * pObj)
{
	list<CObj*>::iterator	iter;
	list<CObj*>::iterator	iterEnd = m_ObjList.end();

	for (iter = m_ObjList.begin(); iter != iterEnd; ++iter)
	{
		if (*iter == pObj)
		{
			SAFE_RELEASE((*iter));
			iter = m_ObjList.erase(iter);
			return;
		}
	}
}

void CObj::EraseObj(const string & strTag)
{

	list<CObj*>::iterator	iter;
	list<CObj*>::iterator	iterEnd = m_ObjList.end();

	for (iter = m_ObjList.begin(); iter != iterEnd; ++iter)
	{
		if ((*iter)->GetTag() == strTag)
		{
			SAFE_RELEASE((*iter));
			iter = m_ObjList.erase(iter);
			return;
		}
	}
}

void CObj::EraseObj()
{
	Safe_Release_VecList(m_ObjList);
}

CAnimation * CObj::CreateAnimation(const string & strTag)
{
	SAFE_RELEASE(m_pAnimation);

	m_pAnimation = new CAnimation;

	m_pAnimation->SetTag(strTag);
	m_pAnimation->SetObj(this);


	if (!m_pAnimation->Init())
	{
		SAFE_RELEASE(m_pAnimation);
		return NULL;
	}

	m_pAnimation->AddRef();

	return m_pAnimation;
}

bool CObj::AddAnimationClip(const string & strName, ANIMATION_TYPE eType, 
	ANIMATION_OPTION eOption, float fAnimationLimitTime, int iFrameMaxX, 
	int iFrameMaxY, int iStartX, int iStartY, int iLengthX, int iLengthY, 
	float fOptionLimitTime, const string & strTexKey, const wchar_t * pFileName,
	const string & strPathKey)
{
	if (!m_pAnimation)
	{
		return false;
	}

	m_pAnimation->AddClip(strName, eType, eOption, fAnimationLimitTime,
		iFrameMaxX, iFrameMaxY, iStartX, iStartY, iLengthX, iLengthY,
		fOptionLimitTime, strTexKey, pFileName, strPathKey);

	return true;
}

void CObj::SetAnimationClipColorKey(const string & strClip, unsigned char r, unsigned char g, unsigned char b)
{

	if (m_pAnimation)
	{
		m_pAnimation->SetClipColorKey(strClip,r, g, b);
	}
}




void CObj::SetTexture(CTexture * pTexture)
{
	SAFE_RELEASE(m_pTexture);
	m_pTexture = pTexture;

	if (pTexture)
	{
		pTexture->AddRef();
	}
}

void CObj::SetTexture(const string & strKey,
	const wchar_t * pFileName, const string & strPathKey)
{
	SAFE_RELEASE(m_pTexture);
	m_pTexture = GET_SINGLE(CResourcesManager)->LoadTexture(
		strKey, pFileName, strPathKey);
}

void CObj::SetColorKey(unsigned char r, unsigned char g, unsigned char b)
{
	m_pTexture->SetColorKey(r, g, b);
}



void CObj::Input(float fDeltaTime)
{
}

int CObj::Update(float fDeltaTime)
{
	list<CCollider*>::iterator iter;
	list<CCollider*>::iterator iterEnd = m_ColliderList.end();

	for (iter = m_ColliderList.begin(); iter != iterEnd;)
	{
		if (!(*iter)->GetEnable())
		{
			++iter;
			continue;
		}
		(*iter)->Update(fDeltaTime);

		if (!(*iter)->GetLife())
		{
			SAFE_RELEASE((*iter));
			iter = m_ColliderList.erase(iter);
			iterEnd = m_ColliderList.end();
		}

		else
		{
			++iter;
		}

	}

	return 0;
}

int CObj::LateUpdate(float fDeltaTime)
{
	list<CCollider*>::iterator iter;
	list<CCollider*>::iterator iterEnd = m_ColliderList.end();

	for (iter = m_ColliderList.begin(); iter != iterEnd;)
	{
		if (!(*iter)->GetEnable())
		{
			++iter;
			continue;
		}
		(*iter)->LateUpdate(fDeltaTime);

		if (!(*iter)->GetLife())
		{
			SAFE_RELEASE((*iter));
			iter = m_ColliderList.erase(iter);
			iterEnd = m_ColliderList.end();
		}

		else
		{
			++iter;
		}

	}
	return 0;
}

void CObj::Collision(float fDeltaTime)
{
}

void CObj::Render(HDC hDC, float fDeltaTime)
{	
	if (m_pTexture) 
	{
		POSITION tPos = m_tPos - m_tSize * m_tPivot;

		tPos -= GET_SINGLE(CCamera)->GetPos();
		// 오브젝트가 옮겨진 만큼 카메라도 움직여야하니깐
		// 상대적인 (상대좌표)를 구한다.

		POSITION	tImagePos;

		if (m_pAnimation)
		{
			PANIMATIONCLIP pClip = m_pAnimation->GetCurrentClip();

			tImagePos.x = pClip->iFrameX * m_tSize.x;
		
			tImagePos.y = pClip->iFrameY * m_tSize.y;

		}


		if (m_pTexture->GetColorKeyEnable())
		{
			TransparentBlt(hDC, tPos.x, tPos.y, m_tSize.x, m_tSize.y,
				m_pTexture->GetDC(),tImagePos.x,tImagePos.y,
				m_tSize.x, m_tSize.y, m_pTexture->GetColorKey());
		}

		else
		{
			BitBlt(hDC, tPos.x, tPos.y, m_tSize.x, m_tSize.y,
				m_pTexture->GetDC(),tImagePos.x, tImagePos.y, SRCCOPY);
			//카피해서 넣어주겠다라는 SRCCOPY의띁
		}	
	}


	list<CCollider*>::iterator iter;
	list<CCollider*>::iterator iterEnd = m_ColliderList.end();

	for (iter = m_ColliderList.begin(); iter != iterEnd;)
	{
		if (!(*iter)->GetEnable())
		{
			++iter;
			continue;
		}
		(*iter)->Render(hDC,fDeltaTime);

		if (!(*iter)->GetLife())
		{
			SAFE_RELEASE((*iter));
			iter = m_ColliderList.erase(iter);
			iterEnd = m_ColliderList.end();
		}

		else
		{
			++iter;
		}

		if (m_pAnimation)
		{
			m_pAnimation->Update(fDeltaTime);
		}


	}
	


}


CObj * CObj::CreateCloneObj(const string & strTagPrototypeKey, 
	const string & strTag,class CLayer* pLayer)
{

	CScene* pScene = GET_SINGLE(CSceneManager)->GetScene();

	CObj*	pProto = CScene::FindPrototype(strTagPrototypeKey);

	if (!pProto)
	{
		return NULL;
	}

	CObj*	pObj = pProto->Clone();
	
	pObj->SetTag(strTag);

	if (pLayer)
	{
		pLayer->AddObject(pObj);
	}
	AddObj(pObj);

	return pObj;

}


