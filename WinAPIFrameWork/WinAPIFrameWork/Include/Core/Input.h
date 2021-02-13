#pragma once

#include "../Game.h"

typedef struct _tagKeyInfo
{
	string strName;
	vector<DWORD>	vecKey;
	bool	bDown;
	bool	bPress;
	bool	bUp;

	_tagKeyInfo() :
		bDown(false),
		bPress(false),
		bUp(false)
	{
	}
}KEYINFO, *PKEYINFO;

class CInput
{
private:
	HWND	m_hWnd;
	unordered_map<string, PKEYINFO>	m_mapKey; // 키를 비교하기 위해서 
	PKEYINFO m_pCreateKey; // 키입력을 받기 위해서

public:
	bool Init(HWND hwnd);
	void Update(float fDeltaTime);
	bool KeyDown(const string& strKey) const;
	bool KeyPress(const string& strKey) const;
	bool KeyUp(const string& strKey) const;
public:
	template <typename T>
	bool AddKey(const T& data)
	{
		const char* pTType = typeid(T).name();

		if (strcmp(pTType, "char") == 0 ||
			strcmp(pTType, "int")== 0)
		{
			m_pCreateKey->vecKey.push_back((DWORD)data);
		}

		else
		{
			m_pCreateKey->strName = data;
			m_mapKey.insert(make_pair(m_pCreateKey->strName, m_pCreateKey));
		}

		
		return true;
	}

public:
	template <typename T, typename ... Types>
	bool AddKey(const T& data, const Types& ...arg)
	{//VK랑 일반 asdf 같은키를 구별해야함 
	//그래서 vector<DWORD>를 선언
		if (!m_pCreateKey)
			m_pCreateKey = new KEYINFO;
		

		const char* pTType = typeid(T).name();
		 
		if (strcmp(pTType,"char") == 0 ||
			strcmp(pTType,"int") == 0)
		{
			m_pCreateKey->vecKey.push_back((DWORD)data);
		}

		else
		{
			m_pCreateKey->strName = data;
			m_mapKey.insert(make_pair(m_pCreateKey->strName, m_pCreateKey));
		}

		AddKey(arg...);

		if (m_pCreateKey)
			m_pCreateKey = NULL;
		

		return true;
	}


private:
	PKEYINFO FindKey(const string& strKey) const;
	DECLARE_SINGLE(CInput);
	
};

