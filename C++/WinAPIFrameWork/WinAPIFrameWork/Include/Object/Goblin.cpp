#include "Goblin.h"
#include "../Core.h"
#include "../Collider/ColliderRect.h"


CGoblin::CGoblin()	: 
	 m_fFireTime(0.f), m_fFireLimitTime(1.12f)
{
}

CGoblin::CGoblin(const CGoblin & goblin)	:
	CMoveObj(goblin)
{
	m_eDir = goblin.m_eDir;
	m_fFireTime = goblin.m_fFireTime;
	m_fFireLimitTime = goblin.m_fFireLimitTime;

}

CGoblin::~CGoblin()
{
}

bool CGoblin::Init()
{
	SetPos(800.f, 100.f);
	SetSize(100.f, 100.f);
	SetPivot(0.8f,0.8f);
	SetSpeed(300.f);	
	SetTexture("Goblin", L"Goblin.bmp");

	m_pTexture->SetColorKey(255, 255, 255);

	m_eDir = MD_FRONT;

	CColliderRect* pRC = AddCollider<CColliderRect>("Goblin");

	pRC->SetRect(-50.f,-50.f,50.f,50.f);

	/*pRC->AddCollisionFunction(CS_ENTER,this,&CGoblin::CollisionBullet);*/	


	SAFE_RELEASE(pRC);
	return true;
}

int CGoblin::Update(float fDeltaTime)
{
	CMoveObj::Update(fDeltaTime);

	MoveYFromSpeed(fDeltaTime, m_eDir);

	if (m_tPos.y + m_tSize.y >= GETRESOLUTION.iH)
	{
		m_tPos.y = GETRESOLUTION.iH - m_tSize.y;
		m_eDir = MD_BACK;
	}
	
	else if (m_tPos.y <= 0.f)
	{
		m_tPos.y = 0.f;
		m_eDir = MD_FRONT;
	}

	m_fFireTime += fDeltaTime;



	if (m_fFireTime >= m_fFireLimitTime)
	{
		m_fFireTime -= m_fFireTime;
		/*Fire();*/
	}

	return 0;
}

int CGoblin::LateUpdate(float fDeltaTime)
{
	CMoveObj::LateUpdate(fDeltaTime);
	return 0;
}

void CGoblin::Collision(float fDeltaTime)
{
	CMoveObj::Collision(fDeltaTime);
}

void CGoblin::Render(HDC hDC, float fDeltaTime)
{
	CMoveObj::Render(hDC, fDeltaTime);
	/*Rectangle(hDC, m_tPos.x, m_tPos.y, m_tPos.x + m_tSize.x, m_tPos.y + m_tSize.y);*/

}

CGoblin * CGoblin::Clone()
{
	return new CGoblin(*this);
}

void CGoblin::CollisionBullet(CCollider * pSrc, CCollider * pDest, float fDeltaTime)
{
	/*MessageBox(NULL, L"충돌", L"충돌", MB_OK);*/
}

void CGoblin::Fire()
{
	CObj*	pBullet = CObj::CreateCloneObj("Bullet", "GoblinBullet",
		m_pLayer);

	((CMoveObj*)pBullet)->SetAngle(PI);

	POSITION	tPos;

	tPos.x = m_tPos.x + (1.f - m_tPivot.x) * m_tSize.x;
	tPos.y = m_tPos.y + (0.5f - m_tPivot.x) * m_tSize.y;

	pBullet->SetPos(tPos.x, tPos.y - pBullet->GetSize().y / 2.f);



	/* 이 함수가 문제인거같은데 */
	//float x = GetLeft() - 
	//	(pBullet->GetSize().x * (1.f - pBullet->GetPivot().x));

	//float y = GetCenter().y;

	//pBullet->SetPos(x, y);

	SAFE_RELEASE(pBullet);
}
