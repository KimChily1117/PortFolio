#pragma once
#include "../Game.h"


class CResourcesManager
{
private:
	unordered_map<string, class CTexture*> m_mapTexture;
	HINSTANCE		m_hInst;
	HDC				m_hDC;
	class CTexture*	m_pPackBuffer;
	
public:
	class CTexture* GetBackBuffer()	const;
	
public:
	bool Init(HINSTANCE hInst,HDC hDC);

	CTexture* LoadTexture(const string & strKey,
		const wchar_t * pFileName,
		const string & strPathKey = TEXTURE_PATH);

	
	class CTexture* FindTexture(const string& strKey);


	DECLARE_SINGLE(CResourcesManager)

	// ½Ì±ÛÅæ ¼±¾ð 

};

