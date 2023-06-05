#include "Texture.h"
#include "../Core/PathManager.h"


CTexture::CTexture() :
	m_hMemDC(NULL),
	m_bColorKeyEnable(false),
	m_ColorKey(RGB(255,0,255)) // 디폴트 색상 
{
}


CTexture::~CTexture()
{
	//메모리를 해제할때 기존에 지정되어있던 도구를 다시 지정해준다
	SelectObject(m_hMemDC, m_hOldBitmap);

	// Bitmap을 지워준다. 

	DeleteObject(m_hBitmap);

	//DC를 지워준다.
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
	//메모리 dc를 만들어 준다. 
	m_hMemDC = CreateCompatibleDC(hDC);
	//매개변수로 받아온 hDC용 MemDC를 만듬 

	// 경로를 만들어줘야함 (pathmanager에서 받아옴)

	const wchar_t* pPath = GET_SINGLE(CPathManager)->FindPath(strPathKey);

	wstring strPath;

	if (pPath)
	{
		strPath = pPath;
	}
	
	strPath += pFileName;

	m_hBitmap = (HBITMAP)LoadImage(hInst, strPath.c_str(),
		IMAGE_BITMAP, 0, 0, LR_LOADFROMFILE);
	// cx,cy의 값을 0 으로 넣으면 원본 size로 읽어줌 


	// 위에서 만들어준 비트맵 도구를 DC에 저장한다. 
	// 지정할때 반환되는 값은 DC에 기본으로 지정되어 있던
	// 도구가 반환된다. 

	m_hOldBitmap = (HBITMAP)SelectObject(m_hMemDC, m_hBitmap);

	//로드가 끝이나면?

	GetObject(m_hBitmap, sizeof(m_tInfo), &m_tInfo);
	//이미지 정보를 받아올수있다. (get하기 위해서 구현)

	return true;
}

