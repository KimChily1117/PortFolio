#pragma once
#include "../Game.h"

class CSceneManager
{
	
protected:
	class CScene* m_pScene;
	class CScene* m_pNextScene;

public:
	class CScene* GetScene()	const
	{
		return m_pScene;
	}

public:
	bool Init();
	void Input(float fDeltaTime);
	int Update(float fDeltaTime);
	int LateUpdate(float fDeltaTime);
	void Collision(float fDeltaTime);
	void Render(HDC hdc , float fDeltaTime);

public:
	template <typename T>
	T* CreateScene(SCENE_CREATE sc)
	{
		T* pScene = new T;

		if (!pScene->Init())
		{
			SAFE_DELETE(pScene);
			return NULL;
		}
		else
		{
			switch (sc)
			{
			case SC_CURRENT:
				SAFE_DELETE(m_pScene);
				m_pScene = pScene;
				return pScene;
				break;
			

			case SC_NEXT:
				SAFE_DELETE(m_pNextScene);
				m_pNextScene = pScene;
				return pScene;
				break;
			}
		}		
	}

	DECLARE_SINGLE(CSceneManager)
};

