#pragma once
#include "../GameCoding2/Struct.pb.h"

class GameManager
{
	DECLARE_SINGLE(GameManager)
public:
	shared_ptr<GameObject> _myPlayer;
	Protocol::ObjectInfo _myPlayerInfo;
};

