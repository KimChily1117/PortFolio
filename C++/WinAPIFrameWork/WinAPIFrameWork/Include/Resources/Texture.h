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
	HBITMAP	m_hBitmap; // ��Ʈ�� �׸��� ����
	HBITMAP	m_hOldBitmap; //�ٲ����(��, �׸����� ��Ʈ���� �����س������ؼ� ����)
	// �޸𸮸� �����ϱ����� �ٲ���� ��Ʈ������ �ݵ�� �������� �޸𸮸� �����ؾ��Ѵ�.
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

	//texture�� dc�� �ݵ�� �޾ƿ;���

	HDC GetDC()	const
	{
		return m_hMemDC;

	}
};

