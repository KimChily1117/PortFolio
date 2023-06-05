#include "Player.h"
#include "../Core/Input.h"
#include "Bullet.h"
#include "../Animation/Animation.h"


CPlayer::CPlayer()
{

}

CPlayer::CPlayer(const CPlayer & player)
	:CMoveObj(player)	
{
}

CPlayer::~CPlayer()
{
}

bool CPlayer::Init()
{
	SetPos(100,100);
	SetSize(194.f, 180.f);
	SetSpeed(500.f);
	SetPivot(0,0);
	SetTexture("Player", L"Dungeon.bmp");
	m_pTexture->SetColorKey(255, 255, 255);

	CAnimation* pAni = CreateAnimation("PlayerAnimation");

	AddAnimationClip("Tagorr_Anim", AT_ATLAS, AO_LOOP, 0.3f, 6, 1, 0, 0, 6, 1, 0.f,
		"Tagorr", L"Player/Idle/Idle.bmp");

	SetAnimationClipColorKey("Tagorr_Anim", 255, 255, 255);

	SAFE_RELEASE(pAni);


	return true;
}

void CPlayer::Input(float fDeltaTime)
{
	CMoveObj::Input(fDeltaTime);

	if (KEYPRESS("MoveFront"))
	{
		MoveYFromSpeed(fDeltaTime,MD_BACK);
	}
	if (KEYPRESS("MoveBack"))
	{
		MoveYFromSpeed(fDeltaTime, MD_FRONT);
	}
	if (KEYPRESS("MoveLeft"))
	{
		MoveXFromSpeed(fDeltaTime, MD_BACK);
	}
	if (KEYPRESS("MoveRight"))
	{
		MoveXFromSpeed(fDeltaTime, MD_FRONT);
	}

	if (KEYDOWN("Fire"))
	{
		Fire();
	}

	if (KEYDOWN("Skill1"))
	{
		MessageBox(NULL, L"Skill1", L"Skill1", MB_OK);
	}

}

int CPlayer::Update(float fDeltaTime)
{
	CMoveObj::Update(fDeltaTime);
	return 0;
}

int CPlayer::LateUpdate(float fDeltaTime)
{
	CMoveObj::LateUpdate(fDeltaTime);
	return 0;
}

void CPlayer::Collision(float fDeltaTime)
{
	CMoveObj::Collision(fDeltaTime);
}

void CPlayer::Render(HDC hDC, float fDeltaTime)
{
	CMoveObj::Render(hDC,fDeltaTime);
	//Rectangle(hDC, m_tPos.x, m_tPos.y, m_tPos.x + m_tSize.x, m_tPos.y + m_tSize.y);
}

CPlayer * CPlayer::Clone()
{
	return new CPlayer(*this);
}

void CPlayer::Fire()                
{
	CObj*	pBullet = CObj::CreateCloneObj("Bullet" , "PlayerBullet",
		m_pLayer);

	pBullet->AddCollisionFunction("BulletBody", CS_ENTER,
		(CBullet*)pBullet, &CBullet::Hit);
									// 이름 , 콜라이더가 빠져나갈때 , pBullet객체를
									// 충돌함수는?
	//  오른쪽 가운데를 구한다
	POSITION	tPos;

	tPos.x = m_tPos.x + (1.f - m_tPivot.x) * m_tSize.x;
	tPos.y = m_tPos.y + (0.5f - m_tPivot.x) * m_tSize.y;

	pBullet->SetPos(tPos.x, tPos.y - pBullet->GetSize().y / 2.f);

	SAFE_RELEASE(pBullet);
}
