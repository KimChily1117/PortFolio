#pragma once
#include "Game.h"
class CCore
{
private:
	static CCore* m_pInst;
	//메모리 생성시점은 프로그램이 실행될때 , 전역변수와 비슷함
	//특징 : 어째되었던 맴버 변수임 
	//일반 변수는 객체마다 따로 잡힘(메모리가)
	//근데 Static 정적 맴버 변수는 모든 CCore라는 맴버를 선언할때 
	// 같은 메모리를 공유? 한다 보면됨 (한개를 잡아놓고 같이씀) 

public:
	static CCore* GetInst()
	{
		if (!m_pInst)
		{
			m_pInst = new CCore;
		}
		return m_pInst;
	}

	static void DestroyInst()
	{
		SAFE_DELETE(m_pInst);
	}

private:
	CCore();
	~CCore();

private:
	static bool		m_bLoop;

private:
	HINSTANCE	m_hInst;
	HWND		m_hWnd;
	RESOLUTION	m_tRS;
	HDC			m_hDC;
public:

	RESOLUTION GetResolution()	const
	{
		return m_tRS;
	}

public:
	bool Init(HINSTANCE hInst);
	int Run();
private:
	void Logic();
	void Input(float fDeltaTime);
	int Update(float fDeltaTime);
	int LateUpdate(float fDeltaTime);
	void Collision(float fDeltaTime);
	void Render(float fDeltaTime);

private:
	ATOM MyRegisterClass();
	BOOL Create();
public:
	static LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam);

};

