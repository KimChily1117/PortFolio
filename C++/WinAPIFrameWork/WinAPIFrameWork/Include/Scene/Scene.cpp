#include "Scene.h"
#include "Layer.h"
#include "../Object/Obj.h"

unordered_map<string, CObj*>CScene::m_mapPrototype;


CScene::CScene()
{
	CLayer * pLayer = CreateLayer("UI", INT_MAX); 
	pLayer = CreateLayer("Default", 2);
	pLayer = CreateLayer("Stage",1);
}

CScene::~CScene()
{
	ErasePrototype();
    Safe_Delete_VecList(m_LayerList);
}

void CScene::ErasePrototype()
{
	Safe_Release_Map(m_mapPrototype);
}

void CScene::ErasePrototype(const string & strTag)
{
	unordered_map<string, CObj*>::iterator	iter = 
		m_mapPrototype.find(strTag);

	if (!iter->second)
	{
		return;
	}

	SAFE_RELEASE(iter->second);
	m_mapPrototype.erase(iter);
}

CLayer* CScene::CreateLayer(const string& strTag, int iZOrder)
{
    CLayer* pLayer = new CLayer;
    pLayer->SetTag(strTag);
    pLayer->SetZOrder(iZOrder);
    pLayer->SetScene(this);
   

    m_LayerList.push_back(pLayer);   

    if (m_LayerList.size() >= 2)
            m_LayerList.sort(CScene::LayerSort);
        //리스트를 정렬할때 밑에 있는 sort함수의 리턴값에따라서
        //값을 바꿔줄지 결정함
    
	return pLayer;
}

CLayer* CScene::FindLayer(const string& strTag)
{
    list<CLayer*>::iterator iter;
    list<CLayer*>::iterator iterEnd = m_LayerList.end();

    for (iter = m_LayerList.begin(); iter != iterEnd; )
    {
        if ((*iter)->GetTag() == strTag)
        {
            return *iter;
        }

		else
		{
			++iter;
		}

    }
    return NULL;
}

bool CScene::Init()
{
    return true;
}

void CScene::Input(float fDeltaTime)
{
    list<CLayer*>::iterator iter;
    list<CLayer*>::iterator iterEnd = m_LayerList.end();

    for (iter = m_LayerList.begin(); iter!= iterEnd;)
    {
		if (!(*iter)->GetEnable())
		{
			++iter;
			continue;
		}

        (*iter)->Input(fDeltaTime);
		
		if (!(*iter)->GetLife())
		{
			SAFE_DELETE((*iter));
			iter = m_LayerList.erase(iter);
			iterEnd = m_LayerList.end();
		}

		else
		{
			++iter;
		}

    }
}

int CScene::Update(float fDeltaTime)
{
    list<CLayer*>::iterator iter;
    list<CLayer*>::iterator iterEnd = m_LayerList.end();

    for (iter = m_LayerList.begin(); iter != iterEnd;)
    {

		if (!(*iter)->GetEnable())
		{
			++iter;
			continue;
		}
		(*iter)->Update(fDeltaTime);

		if (!(*iter)->GetLife())
		{
			SAFE_DELETE((*iter));
			iter = m_LayerList.erase(iter);
			iterEnd = m_LayerList.end();
		}

		else
		{
			++iter;
		}

    }
    return 0;
}

int CScene::LateUpdate(float fDeltaTime)
{
    list<CLayer*>::iterator iter;
    list<CLayer*>::iterator iterEnd = m_LayerList.end();

    for (iter = m_LayerList.begin(); iter != iterEnd;)
    {
		if (!(*iter)->GetEnable())
		{
			++iter;
			continue;
		}
		(*iter)->LateUpdate(fDeltaTime);

		if (!(*iter)->GetLife())
		{
			SAFE_DELETE((*iter));
			iter = m_LayerList.erase(iter);
			iterEnd = m_LayerList.end();
		}

		else
		{
			++iter;
		}

    }
    return 0;
}

void CScene::Collision(float fDeltaTime)
{
    list<CLayer*>::iterator iter;
    list<CLayer*>::iterator iterEnd = m_LayerList.end();

    for (iter = m_LayerList.begin(); iter != iterEnd;)
    {

		if (!(*iter)->GetEnable())
		{
			++iter;
			continue;
		}
		(*iter)->Collision(fDeltaTime);

		if (!(*iter)->GetLife())
		{
			SAFE_DELETE((*iter));
			iter = m_LayerList.erase(iter);
			iterEnd = m_LayerList.end();
		}

		else
		{
			++iter;
		}

    }
}

void CScene::Render(HDC hDC, float fDeltaTime)
{
    list<CLayer*>::iterator iter; 
    list<CLayer*>::iterator iterEnd = m_LayerList.end();

    for (iter = m_LayerList.begin(); iter != iterEnd; )
    {

		if (!(*iter)->GetEnable())
		{
			++iter;
			continue;
		}
		(*iter)->Render(hDC,fDeltaTime);

		if (!(*iter)->GetLife())
		{
			SAFE_DELETE((*iter));
			iter = m_LayerList.erase(iter);
			iterEnd = m_LayerList.end();
		}

		else
		{
			++iter;
		}

    }
}

CObj* CScene::FindPrototype(const string & strKey)
{
	unordered_map<string, CObj*>::iterator	iter = m_mapPrototype.find(strKey);

	if (iter == m_mapPrototype.end())
		return NULL;

	return iter->second;

}

void CScene::SetBulletAnimationClipColorKey()
{
	FindPrototype("Bullet")->SetAnimationClipColorKey("Bullet_Psycho", 255, 255, 255);
}


bool CScene::LayerSort(CLayer* pL1, CLayer* pL2)
{
    return pL1->GetZOrder() < pL2->GetZOrder();
}
