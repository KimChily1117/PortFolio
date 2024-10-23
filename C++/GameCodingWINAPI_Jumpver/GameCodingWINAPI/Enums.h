#pragma once
#include "Types.h"

enum class SceneType
{
	NONE,
	DevScene,
	GameScene
};
enum class ObjectType
{
	None,
	Player,
	Projectile,
};

enum class MoveDir
{
	Left,
	Right,
};


enum LAYER_TYPE
{
	LAYER_BACKGROUND,
	LAYER_OBJECT,
	// ...
	LAYER_UI,

	LAYER_MAXCOUNT
};

enum class ColliderType
{
	Box,
	Sphere,
};

enum COLLISION_LAYER_TYPE : uint8
{
	CLT_OBJECT,
	CLT_GROUND,
	CLT_WALL,
	// ...
};

