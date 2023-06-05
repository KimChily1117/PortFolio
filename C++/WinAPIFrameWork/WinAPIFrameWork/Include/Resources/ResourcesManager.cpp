#include "ResourcesManager.h"
#include "Texture.h"

DEFINITION_SINGLE(CResourcesManager);

CTexture * CResourcesManager::GetBackBuffer() const
{
	m_pPackBuffer->AddRef();

	return m_pPackBuffer;
}

bool CResourcesManager::Init(HINSTANCE hInst, HDC hDC)
{
	m_hInst = hInst;
	m_hDC = hDC;
	
	m_pPackBuffer = LoadTexture("BackBuffer", L"BackBuffer.bmp");

	return true;
}

CTexture* CResourcesManager::LoadTexture(const string & strKey, 
	const wchar_t * pFileName,
	const string & strPathKey)
{
	CTexture*	pTexture = FindTexture(strKey);

	if (pTexture)
	{
		return pTexture;
	}

	else
	{
		pTexture = new CTexture;
		if (!pTexture->LoadTexture(m_hInst,m_hDC,strKey,pFileName,
			strPathKey)) // ��ο� �������� ������ 
		{
			SAFE_RELEASE(pTexture);
			return NULL;
		}
	}

	pTexture->AddRef();
	m_mapTexture.insert(make_pair(strKey, pTexture));

	return pTexture;
}


CTexture * CResourcesManager::FindTexture(const string & strKey)
{
	unordered_map<string,class CTexture*>::iterator iter = m_mapTexture.find(strKey);

	if (iter == m_mapTexture.end())
	{
		return NULL;
	}

	iter->second->AddRef();
	return iter->second;

	// �̷��� �����ؾ� �޸𸮸� �����Ҽ�����. �м��ʿ�
}

CResourcesManager::CResourcesManager()
{
}


CResourcesManager::~CResourcesManager()
{
	Safe_Release_Map(m_mapTexture);
	SAFE_RELEASE(m_pPackBuffer);
}
