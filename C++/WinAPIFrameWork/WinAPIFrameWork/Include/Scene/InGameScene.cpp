#include "InGameScene.h"
#include "../Object/Player.h"
#include "../Object/Goblin.h"
#include "../Object/Bullet.h"
#include "../Object/Stage.h"
#include "Layer.h"
#include "../Core/Camera.h"
#include "../Object/Obj.h"

CInGameScene::CInGameScene()
{
}


CInGameScene::~CInGameScene()
{
}

bool CInGameScene::Init()
{
	// 부모 Scene 클래스의 초기화 함수를 호출해준다.
	if (!CScene::Init())
	{
		return false;
	}

	CLayer* pStageLayer = FindLayer("Stage");

	CStage* pStage = CObj::CreateObj<CStage>("Stage", pStageLayer);

	SAFE_RELEASE(pStage);

	CLayer*		pLayer = FindLayer("Default");

	CPlayer* pPlayer = CObj::CreateObj<CPlayer>("Player", pLayer);
			
	GET_SINGLE(CCamera)->SetTarget(pPlayer);
	GET_SINGLE(CCamera)->SetPivot(0.8f,0.3f);


	SAFE_RELEASE(pPlayer);

	CGoblin*	pGoblin= CObj::CreateObj<CGoblin>("Goblin",pLayer);

	SAFE_RELEASE(pGoblin);

	//총알 프로토 타입을 만들어준다	

	CBullet*	pBullet = CScene::CreatePrototype<CBullet>("Bullet");	
	

	pBullet->SetSize(50.f,58.f);

	/*SAFE_RELEASE(pBullet);*/

	return true;
}
