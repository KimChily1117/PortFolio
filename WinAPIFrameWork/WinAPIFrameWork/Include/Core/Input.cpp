#include "Input.h"

DEFINITION_SINGLE(CInput)

CInput::CInput():
	m_pCreateKey(NULL)
{
}

CInput::~CInput()
{
	Safe_Delete_Map(m_mapKey);
}

bool CInput::Init(HWND hwnd)
{
	m_hWnd = hwnd;

	AddKey('W', "MoveFront");
	AddKey('S', "MoveBack");
	AddKey("MoveLeft",'A');
	AddKey("MoveRight",'D');
	AddKey("Fire",VK_SPACE);
	AddKey(VK_CONTROL, "Skill1",'1'); // Ctrl + 1 키를 조합해서 누를시 작동 (스킬로 명명)
									  // 메세지 박스 출력으로 대체

	return true;
}

void CInput::Update(float fDeltaTime) // 얘는 계속 실행되면서 키의 입력을 돌아봅니다
{
	unordered_map<string, PKEYINFO>::iterator	iter;
	unordered_map<string, PKEYINFO>::iterator	iterEnd = m_mapKey.end();

	for (iter = m_mapKey.begin(); iter != iterEnd; ++iter)
	{
		int iPushCount = 0;
		for (size_t i = 0; i < iter->second->vecKey.size(); ++i)
		{
			if (GetAsyncKeyState(iter->second->vecKey[i]) & 0x8000)
			{
				++iPushCount;
			}
		}

		if (iPushCount == iter->second->vecKey.size())
		{
			if (!iter->second->bDown && !iter->second->bPress)
			{
				iter->second->bDown = true;
			}

			else if (iter->second->bDown && !iter->second->bPress)
			{
				iter->second->bPress = true;
				iter->second->bDown = false;
			}
		}

		else
		{
			if (iter->second->bDown || iter->second->bPress)
			{
				iter->second->bUp = true;
				iter->second->bDown = false;
				iter->second->bPress = false;
			}

			else if (iter->second->bUp)
			{
				iter->second->bUp = false;
			}

		}

	}


}

PKEYINFO CInput::FindKey(const string & strKey)	const
{
	unordered_map<string, PKEYINFO>::const_iterator iter = m_mapKey.find(strKey);

	if (iter == m_mapKey.end())
	{
		return NULL;
	}

	return iter->second;
}


bool CInput::KeyDown(const string & strKey) const
{
	PKEYINFO	pInfo = FindKey(strKey);

	if (!pInfo)
	{
		return false;
	}
	return pInfo->bDown;
}

bool CInput::KeyPress(const string & strKey) const
{
	PKEYINFO	pInfo = FindKey(strKey);

	if (!pInfo)
	{
		return false;
	}
	return pInfo->bPress;
}

bool CInput::KeyUp(const string & strKey) const
{
	PKEYINFO	pInfo = FindKey(strKey);

	if (!pInfo)
	{
		return false;
	}
	return pInfo->bUp;
}

