#pragma once
#include "../Object/Obj.h"
#include "../Game.h"


class CLayer
{
private:
	friend class CScene;
protected:
	CLayer();
public:
	~CLayer();
private:
	class CScene*	m_pScene;
	string			m_strTag;
	int				m_iZOrder;
	list<class CObj*>	m_ObjList;
	bool			m_bEnable;
	bool			m_bLife;

public:
	void SetEnable(bool bEnable)
	{
		m_bEnable = bEnable;
	}

	void Die()
	{
		m_bLife = false;
	}

	bool GetEnable()	const
	{
		return m_bEnable;
	}

	bool GetLife()	const
	{
		return m_bLife;
	}

public:

	void SetTag(const string& strTag)
	{
		m_strTag = strTag;
	}

	void SetZOrder(int iZOrder)
	{

		m_iZOrder = iZOrder;
	}

	void SetScene(class CScene* pScene)
	{
		m_pScene = pScene;
	}

	int GetZOrder() const
	{
		return m_iZOrder;
	}

	string GetTag() const
	{
		return m_strTag;
	}

	CScene* GetScene() const
	{

		return m_pScene;
	}
public:
	void AddObject(class CObj* pObj)
	{
		pObj->SetScene(m_pScene);
		pObj->SetLayer(this);
		pObj->AddRef();
		m_ObjList.push_back(pObj);
	}

public:
	bool Init();
	void Input(float fDeltaTime);
	int Update(float fDeltaTime);
	int LateUpdate(float fDeltaTime);
	void Collision(float fDeltaTime);
	void Render(HDC hdc, float fDeltaTime);

};

