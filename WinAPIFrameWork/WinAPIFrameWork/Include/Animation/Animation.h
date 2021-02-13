#pragma once
#include "../Core/Ref.h"

class CAnimation :
	public CRef
{
private:
	friend class CObj;

public:
	CAnimation();
	CAnimation(const CAnimation& anim);
	~CAnimation();

private:
	unordered_map<string,PANIMATIONCLIP>	m_mapClip;
	PANIMATIONCLIP	m_pCurClip;
	string			m_strCurClip;
	string			m_strDefalutClip;
	class CObj*		m_pObj;

public:

	PANIMATIONCLIP	GetCurrentClip()	const
	{
		return m_pCurClip;
	}

public:
	void SetObj(class CObj* pObj)
	{
		m_pObj = pObj;
	}


	bool AddClip(const string& strName, ANIMATION_TYPE eType,
		ANIMATION_OPTION eOption, float fAnimationLimitTime,
		int iFrameMaxX, int iFrameMaxY, int iStartX, int iStartY,
		int iLengthX, int iLengthY, float fOptionLimitTime ,
		const string& strTexKey, const wchar_t* pFileName,
		const string& strPathKey = TEXTURE_PATH); // 낱장단위 택스쳐로 애니메이션을 구성

	void SetClipColorKey(const string& strClip,unsigned char r, unsigned char g, unsigned char b);
	void SetCurrentClip(const string& strCurClip);
	void SetDefalutClip(const string& strDefalutClip);
	void ChangeClip(const string& strClip);

private:	
	PANIMATIONCLIP FindClip(const string& strName);



public:

	bool Init();
	void Update(float fTime);
	CAnimation* Clone();


};

