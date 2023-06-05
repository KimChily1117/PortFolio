#include "Animation.h"
#include "../Resources/Texture.h"
#include "../Resources/ResourcesManager.h"
#include "../Object/Obj.h"



CAnimation::CAnimation() :
	m_pCurClip(NULL)
	
{
}

CAnimation::CAnimation(const CAnimation & anim)
{
	*this = anim;

	m_mapClip.clear();

	unordered_map<string, PANIMATIONCLIP>::const_iterator	iter;
	unordered_map<string, PANIMATIONCLIP>::const_iterator	iterEnd = anim.m_mapClip.end();

	for (iter = anim.m_mapClip.begin(); iter != iterEnd; ++iter)
	{
		PANIMATIONCLIP	pClip = new ANIMATIONCLIP;

		*pClip = *iter->second;

		for (size_t i = 0; pClip->vecTexture.size(); ++i)
		{
			pClip->vecTexture[i]->AddRef();
		}
	}
	m_pCurClip = NULL;

	m_strCurClip = "";
	SetCurrentClip(anim.m_strCurClip);
}


CAnimation::~CAnimation()
{
	unordered_map<string, PANIMATIONCLIP>::iterator	iter;
	unordered_map<string, PANIMATIONCLIP>::iterator	iterEnd =m_mapClip.end();

	for (iter = m_mapClip.begin(); iter != iterEnd; ++iter)
	{
		for (size_t i = 0; iter->second->vecTexture.size(); ++i)
		{
			SAFE_RELEASE(iter->second->vecTexture[i]);
		}

		SAFE_DELETE(iter->second);
	}

	m_mapClip.clear();
}

bool CAnimation::AddClip(const string &strName, ANIMATION_TYPE eType, 
	ANIMATION_OPTION eOption, float fAnimationLimitTime, int iFrameMaxX,
	int iFrameMaxY, int iStartX, int iStartY, int iLengthX, int iLengthY,
	float fOptionLimitTime, const string & strTexKey, const wchar_t * pFileName,
	const string & strPathKey)
{
	PANIMATIONCLIP pClip = new ANIMATIONCLIP;

	pClip->eType = eType;
	pClip->eOption = eOption;
	pClip->fAnimationLimitTime = fAnimationLimitTime;
	pClip->iFrameMaxX = iFrameMaxX;
	pClip->iFrameMaxY = iFrameMaxY;
	pClip->iStartX = iStartX;
	pClip->iStartY = iStartY;
	pClip->iLengthX = iLengthX;
	pClip->iLengthY = iLengthY;
	pClip->fOptionLimitTime = fOptionLimitTime;
	pClip->fAnimationFrameTime = fAnimationLimitTime / (iLengthX * iLengthY);
	
	CTexture*	pTex = GET_SINGLE(CResourcesManager)->LoadTexture(strTexKey,
		pFileName, strPathKey);

	pClip->vecTexture.push_back(pTex);

	pClip->fAnimationTime = 0.f;
	pClip->iFrameX = iStartX;
	pClip->iFrameY = iStartY;
	pClip->fOptionTime = 0.f;
	
	m_mapClip.insert(make_pair(strName, pClip));

	if (m_strDefalutClip.empty())
		SetDefalutClip(strName);
	
	if (m_strCurClip.empty())
		SetCurrentClip(strName);
	
	return true;
}

void CAnimation::SetClipColorKey(const string& strClip , 
	unsigned char r, unsigned char g, unsigned char b)
{
	PANIMATIONCLIP	pClip = FindClip(strClip);

	if (!pClip)
	{
		return;
	}

	for (size_t i = 0; i < pClip->vecTexture.size(); ++i)
	{
		pClip->vecTexture[i]->SetColorKey(r, g, b);
	}

}

void CAnimation::SetCurrentClip(const string & strCurClip)
{
	ChangeClip(strCurClip);
}

void CAnimation::SetDefalutClip(const string & strDefalutClip)
{
	m_strDefalutClip = strDefalutClip;
}

void CAnimation::ChangeClip(const string & strClip)
{
	if (m_strCurClip == strClip) // 같은 동작이면 바로 종료 
	{
		return;
	}
	
	m_strCurClip = strClip;

	if (m_pCurClip)
	{
		m_pCurClip->iFrameX = m_pCurClip->iStartX;
		m_pCurClip->iFrameY = m_pCurClip->iStartY;
		m_pCurClip->fAnimationTime = 0.f;
		m_pCurClip->fOptionTime = 0.f;
	}

	m_pCurClip = FindClip(strClip);

	if (m_pCurClip->eType == AT_ATLAS)
		m_pObj->SetTexture(m_pCurClip->vecTexture[0]);

	else if (m_pCurClip->eType == AT_FRAME)
		m_pObj->SetTexture(m_pCurClip->vecTexture[m_pCurClip->iFrameX]);
}

PANIMATIONCLIP CAnimation::FindClip(const string & strName)
{
	unordered_map<string, PANIMATIONCLIP>::iterator	iter = m_mapClip.find(strName);

	if (iter == m_mapClip.end())
	{
		return NULL;
	}

	return iter->second;
}


bool CAnimation::Init()
{
	return true;
}

void CAnimation::Update(float fTime)
{
	m_pCurClip->fAnimationTime += fTime;

	while (m_pCurClip->fAnimationTime >= m_pCurClip->fAnimationFrameTime)
		// if문으로 하지않는이유 if문이 되면 모종의 이유로 deltatime갱신이 
		//너무 크게 되버려서애니메이션이 3장이 한번에 호다닥 넘어갈수도있다고함 
		//그래서 while문으로 선언
	{
		m_pCurClip->fAnimationTime -= m_pCurClip->fAnimationFrameTime;
		
		++m_pCurClip->iFrameX;

		if (m_pCurClip->iFrameX - m_pCurClip->iStartX == m_pCurClip->iLengthX)
		{
			m_pCurClip->iFrameX = m_pCurClip->iStartX;
			++m_pCurClip->iFrameY;

			if (m_pCurClip->iFrameY - m_pCurClip->iStartY == m_pCurClip->iLengthY)
				//애니메이션이 1바퀴 돌았다는뜻 
			{
				m_pCurClip->iFrameY = m_pCurClip->iStartY;
			
				switch (m_pCurClip->eOption)
				{
				case AO_ONCE_RETURN:
					ChangeClip(m_strDefalutClip);
					break;
				case AO_ONCE_DESTROY:
					m_pObj->Die();
					break;
				case AO_TIME_RETURN:
					break;
				case AO_TIME_DESTROY:
					break;
				
				}
			}
		}

	}
}

CAnimation * CAnimation::Clone()
{
	return new CAnimation(*this);
}
