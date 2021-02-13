#pragma once
#include "../Game.h"

class CPathManager
{

private:
	unordered_map<string, wstring> m_mapPath; //그냥키는 string으로 
	//여기다가 (유니코드 2바이트짜리) wstring으로 경로를 저장한다.


public:
	bool	Init();
	bool CreatePath(const string& strKey, const wchar_t* pPath, 
		const string& strBaseKey = ROOT_PATH);
	
	const wchar_t* FindPath(const string& strKey);
	
	
	DECLARE_SINGLE(CPathManager)
};

