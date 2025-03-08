#pragma once
#include "..\GameCoding2\Enum.pb.h"

class UIManager
{
	DECLARE_SINGLE(UIManager)

public:
	void Init();
	void Update();


	void SetTarget(Protocol::PLAYER_CHAMPION_TYPE type);

	void SetHUDControllerGameObject(shared_ptr<GameObject> go) { _hud = go;	}
	void SetCursorControllerGameObject(shared_ptr<GameObject> go) { _cursorManagement = go; }
	void SetSkillIndicatorControllerGameObject(shared_ptr<GameObject> go) { _skillIndicator = go; }

	shared_ptr<GameObject> GetHUD() { return _hud; }
	shared_ptr<GameObject> GetCursorController() { return _cursorManagement; }
	shared_ptr<GameObject> GetSkillIndicatorController() { return _skillIndicator; }

private:
	// HUD�� �ʿ��� ���� ������ Setting
	void InitHUD();	
	// Ŀ�� , �ε������͸� �̰ɷ� �ٲ���
	void InitCursor();
	void InitIndicator();

	shared_ptr<GameObject> _hud;
	shared_ptr<GameObject> _cursorManagement;
	shared_ptr<GameObject> _skillIndicator;


	shared_ptr<Shader> _shader;
};

