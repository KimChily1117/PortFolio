#pragma once
#include "Game.h"
class CCore
{
private:
	static CCore* m_pInst;
	//�޸� ���������� ���α׷��� ����ɶ� , ���������� �����
	//Ư¡ : ��°�Ǿ��� �ɹ� ������ 
	//�Ϲ� ������ ��ü���� ���� ����(�޸𸮰�)
	//�ٵ� Static ���� �ɹ� ������ ��� CCore��� �ɹ��� �����Ҷ� 
	// ���� �޸𸮸� ����? �Ѵ� ����� (�Ѱ��� ��Ƴ��� ���̾�) 

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

