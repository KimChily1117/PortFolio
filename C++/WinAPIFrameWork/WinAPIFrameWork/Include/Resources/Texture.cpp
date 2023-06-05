#include "Texture.h"
#include "../Core/PathManager.h"


CTexture::CTexture() :
	m_hMemDC(NULL),
	m_bColorKeyEnable(false),
	m_ColorKey(RGB(255,0,255)) // ����Ʈ ���� 
{
}


CTexture::~CTexture()
{
	//�޸𸮸� �����Ҷ� ������ �����Ǿ��ִ� ������ �ٽ� �������ش�
	SelectObject(m_hMemDC, m_hOldBitmap);

	// Bitmap�� �����ش�. 

	DeleteObject(m_hBitmap);

	//DC�� �����ش�.
	DeleteDC(m_hMemDC);
}


void CTexture::SetColorKey(unsigned char r, unsigned char g, unsigned char b)
{
	m_ColorKey = RGB(r, g, b);
	m_bColorKeyEnable = true;
}

void CTexture::SetColorKey(COLORREF colorKey)
{
	m_ColorKey = colorKey;
}

bool CTexture::LoadTexture(HINSTANCE hInst, HDC hDC,
	const string & strKey, const wchar_t * pFileName,
	const string & strPathKey)
{
	//�޸� dc�� ����� �ش�. 
	m_hMemDC = CreateCompatibleDC(hDC);
	//�Ű������� �޾ƿ� hDC�� MemDC�� ���� 

	// ��θ� ���������� (pathmanager���� �޾ƿ�)

	const wchar_t* pPath = GET_SINGLE(CPathManager)->FindPath(strPathKey);

	wstring strPath;

	if (pPath)
	{
		strPath = pPath;
	}
	
	strPath += pFileName;

	m_hBitmap = (HBITMAP)LoadImage(hInst, strPath.c_str(),
		IMAGE_BITMAP, 0, 0, LR_LOADFROMFILE);
	// cx,cy�� ���� 0 ���� ������ ���� size�� �о��� 


	// ������ ������� ��Ʈ�� ������ DC�� �����Ѵ�. 
	// �����Ҷ� ��ȯ�Ǵ� ���� DC�� �⺻���� �����Ǿ� �ִ�
	// ������ ��ȯ�ȴ�. 

	m_hOldBitmap = (HBITMAP)SelectObject(m_hMemDC, m_hBitmap);

	//�ε尡 ���̳���?

	GetObject(m_hBitmap, sizeof(m_tInfo), &m_tInfo);
	//�̹��� ������ �޾ƿü��ִ�. (get�ϱ� ���ؼ� ����)

	return true;
}

