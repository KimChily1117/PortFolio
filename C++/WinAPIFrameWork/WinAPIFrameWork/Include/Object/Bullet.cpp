#include "Bullet.h"
#include "../Resources/Texture.h"
#include "../Collider/ColliderRect.h"
#include "../Animation/Animation.h"


CBullet::CBullet() :
	m_fDist(0.f),
	m_fLimitDist(500.f)
{
}

CBullet::CBullet(const CBullet & bullet)	:
	CMoveObj(bullet)
{
	m_fLimitDist = bullet.m_fLimitDist;
	m_fDist = bullet.m_fDist;
}


CBullet::~CBullet()
{
}

bool CBullet::Init()
{
	SetSpeed(500.f);

	SetPivot(0.5f, 0.5f);

	SetTexture("Bullet", L"Bullet.bmp");

	/*SetColorKey(255, 255, 255);*/
	
	CColliderRect* pRC = AddCollider<CColliderRect>("BulletBody");

	pRC->SetRect(-25.f, -25.f,25.f,25.f);

	SAFE_RELEASE(pRC);
	
	CAnimation* pAni = CreateAnimation("BulletAnimation");

	AddAnimationClip("Bullet_Psycho",AT_ATLAS,AO_LOOP,0.7f,10,1,0,0,10,1,0.f,
		"Psycho",L"Effect/Psycho.bmp"); 
	
	SetAnimationClipColorKey("Bullet_Psycho", 255, 255, 255);

	SAFE_RELEASE(pAni);


	return true;
}

int CBullet::Update(float fDeltaTime)
{
	CMoveObj::Update(fDeltaTime);

	MoveAngle(fDeltaTime);

	m_fDist += GetSpeed() * fDeltaTime;

	if (m_fDist >= m_fLimitDist) //사정거리 다되면 오브젝트 삭제
	{
		Die();
	}

	return 0;
}

int CBullet::LateUpdate(float fDeltaTime)
{
	CMoveObj::LateUpdate(fDeltaTime);
	return 0;
}

void CBullet::Collision(float fDeltaTime)
{
	CMoveObj::Collision(fDeltaTime);
}

void CBullet::Render(HDC hDC, float fDeltaTime)
{
	CMoveObj::Render(hDC, fDeltaTime);
	/*Ellipse(hDC, m_tPos.x, m_tPos.y, m_tPos.x + m_tSize.x, m_tPos.y + m_tSize.y);*/
}

CBullet *CBullet::Clone()
{
	return new CBullet(*this);
}

void CBullet::Hit(CCollider * pSrc, CCollider * pDest, float fDeltaTime)
{
	Die(); //삭제하거나
	MessageBox(NULL, L"충돌", L"충돌", MB_OK); // 충돌 메세지창을 출력

}
