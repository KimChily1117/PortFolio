#pragma once

enum SCENE_CREATE
{
	SC_CURRENT,
	SC_NEXT
};

// Direction
enum MOVE_DIR
{
	MD_BACK = -1,
	MD_NONE,
	MD_FRONT
};

//Collider Type

enum COLLIDER_TYPE
{
	CT_RECT,
	CT_SPHERE,
	CT_LINE,
	CT_POINT,
	CT_PIXEL,
	CT_END
};

//Collision  State
enum COLLISION_STATE
{
	CS_ENTER,
	CS_STAY,
	CS_LEAVE,
	CS_END
};

// Animation Type
// 

enum ANIMATION_TYPE
{
	AT_ATLAS,
	AT_FRAME,
	AT_END
};

// Animation Option

enum ANIMATION_OPTION
{
	AO_LOOP,
	AO_ONCE_RETURN,//�ѹ� ����ǰ� �⺻(idle)���·� ����
	AO_ONCE_DESTROY, // �ѹ� ����ǰ� �ı�(����Ʈ)�� ���� 
	AO_TIME_RETURN, 
	AO_TIME_DESTROY
};