#pragma once
#include "../Core/Ref.h"
#include "../Object/Obj.h"

class CCollider :
	public CRef
{
protected:
	friend class CObj;

protected:
	CCollider();
	CCollider(const CCollider& coll);
	virtual ~CCollider() = 0;

protected:
	COLLIDER_TYPE	m_eCollType;
	class CObj*		m_pObj;
	// �� �ݶ��̴��������ϰ��ִ� ������Ʈ�� �˱�����
	list<CCollider*>	m_CollisionList;
	list <function<void(CCollider*, CCollider*, float)>> m_FuncList[CS_END];

public:
	void AddCollisionFunction(COLLISION_STATE eState,
		void(*pFunc)(CCollider*, CCollider*, float))
	{
		function<void(CCollider*, CCollider*, float)>	func;

		func = bind(pFunc, placeholders::_1, placeholders::_2,
			placeholders::_3);

		m_FuncList[eState].push_back(func);
	}

	template <typename T>
	void AddCollisionFunction(COLLISION_STATE eState,T* pObj,
		void(T::*pFunc)(CCollider*, CCollider*, float))
	{
		function<void(CCollider*, CCollider*, float)>	func;

		func = bind(pFunc,pObj,placeholders::_1, placeholders::_2,
			placeholders::_3);

		m_FuncList[eState].push_back(func);
	}

	
	void CallFunction(COLLISION_STATE eState,CCollider* pDest,float fDeltaTime)
	{
		list <function<void(CCollider*, CCollider*, float)>>::iterator iter;
		list <function<void(CCollider*, CCollider*, float)>>::iterator iterEnd = m_FuncList[eState].end();


		for (iter = m_FuncList[eState].begin(); iter!= iterEnd; ++iter)
		{
			(*iter)(this, pDest, fDeltaTime);
		}
	}	


public:

	void AddCollider(CCollider* pCollider)
	{
		m_CollisionList.push_back(pCollider);
	}

	bool	CheckCollisionList(CCollider* pCollider)
	{
		list<CCollider*>::iterator	iter;
		list<CCollider*>::iterator	iterEnd = m_CollisionList.end();

		for(iter = m_CollisionList.begin(); iter!=iterEnd; ++iter)
		{
			if(*iter == pCollider) // �ּҺ񱳸� ������ ���ٸ�
			{			
				return true;
			}
			
			//�װ� �ƴϸ�			
			return false;
		}		
	}
	
	void EraseCollisionList(CCollider* pCollider)
	{		
		list<CCollider*>::iterator	iter;
		list<CCollider*>::iterator	iterEnd = m_CollisionList.end();

		for(iter = m_CollisionList.begin(); iter!=iterEnd; ++iter)
		{
			if(*iter == pCollider) // �ּҺ񱳸� ������ ���ٸ�
			{			
				m_CollisionList.erase(iter);
				break;
			}		
			
		}
	}

public:
	COLLIDER_TYPE GetColliderType()	const
	{
		return m_eCollType;
	}

	class CObj*	GetObj()	const
	{
		return m_pObj;
	}

public:
	void SetObj(class CObj* pObj)
	{
		m_pObj = pObj;
	}

public:

	virtual bool Init() = 0;
	virtual void Input(float fDeltaTime);
	virtual int Update(float fDeltaTime);
	virtual int LateUpdate(float fDeltaTime);
	virtual bool Collision(CCollider* pDest);
	// ��� ��� �ǹ̰�����. ���⿡ ��� �浹 ����� ���� ���ֵ��� �������� 
	virtual void Render(HDC hDC, float fDeltaTime);
	virtual CCollider* Clone() = 0;
	 
protected:
	bool CollisionRectToRect(const RECTANGLE& src,
		const RECTANGLE& dest);

};

