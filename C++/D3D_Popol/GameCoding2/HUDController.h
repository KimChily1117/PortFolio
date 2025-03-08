#pragma once
#include "MonoBehaviour.h"

class HUDController : public MonoBehaviour
{
public:
	void Awake() override;
	void Update() override;

	void UpdatePlayerStatus();


	shared_ptr<GameObject> QSkillSocket;
	shared_ptr<GameObject> WSkillSocket;
	shared_ptr<GameObject> ESkillSocket;
	shared_ptr<GameObject> RSkillSocket;
	
	shared_ptr<GameObject> HpBar;
	shared_ptr<GameObject> MpBar;

	shared_ptr<GameObject> ChampMark;
};

