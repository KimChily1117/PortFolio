#include "Timer.h"

DEFINITION_SINGLE(CTimer);

CTimer::CTimer() :
	m_fTimeScale(1.f)
{


}

CTimer::~CTimer()
{


}

bool CTimer::Init()
{
	QueryPerformanceFrequency(&m_tSecond);
	QueryPerformanceCounter(&m_tTime);

	m_fDeltaTime = 0.f;
	m_fFPS = 0.f;
	m_iFrameMax = 60;
	m_iFrame = 0;

	return true;
}

void CTimer::Update()
{
	LARGE_INTEGER tTime;
	QueryPerformanceCounter(&tTime);

	m_fDeltaTime = (tTime.QuadPart - m_tTime.QuadPart) /
		(float)m_tSecond.QuadPart;

	m_tTime = tTime;
}


