#pragma once

enum class Champions
{
	ANNIE,
	GAREN
};




class UIManager
{
	DECLARE_SINGLE(UIManager)

public:
	void SetTarget();	
	shared_ptr<class PlayerHUD> _playerHUD;
};

