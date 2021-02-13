#pragma once
#include "../Core/Ref.h"
class CTexture :
	public CRef
{
private:
	CTexture();
	~CTexture();
private:
	friend class CResourcesManager;
private:
	HDC		m_hMemDC;
	HBITMAP	m_hBitmap; // 비트맵 그리기 도구
	HBITMAP	m_hOldBitmap; //바뀌기전(즉, 그리기전 비트맵을 저장해놓기위해서 선언)
	// 메모리를 해제하기전에 바뀌기전 비트맵으로 반드시 돌려놓고 메모리를 해제해야한다.
	BITMAP	m_tInfo;
	COLORREF	m_ColorKey;
	bool	m_bColorKeyEnable;



public:
	void	SetColorKey(unsigned char r, unsigned char g,
		unsigned char b);
	void SetColorKey(COLORREF colorKey);

	COLORREF GetColorKey()	const
	{
		return m_ColorKey;
	}

	bool GetColorKeyEnable()	const
	{
		return m_bColorKeyEnable;
	}


public:
	bool LoadTexture(HINSTANCE hInst, HDC hDC,
		const string& strKey, const wchar_t* pFileName,
		const string& strPathKey = TEXTURE_PATH);

	//texture의 dc를 반드시 받아와야함

	HDC GetDC()	const
	{
		return m_hMemDC;

	}
};

