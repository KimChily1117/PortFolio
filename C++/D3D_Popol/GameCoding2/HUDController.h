#pragma once
#include "MonoBehaviour.h"

class HUDController : public MonoBehaviour
{
public:
	void Awake() override;
	void Update() override;

	void UpdatePlayerStatus();
	void CheckSkillInput();
	void UpdateSkillCooldowns();
	void UpdateSkillCooldown(shared_ptr<GameObject> skillButton, int pass, float& elapsedTime, float duration, const string& elementName, bool isSkillActive);


	shared_ptr<GameObject> QSkillSocket;
	shared_ptr<GameObject> WSkillSocket;
	shared_ptr<GameObject> ESkillSocket;
	shared_ptr<GameObject> RSkillSocket;
	
	shared_ptr<GameObject> HpBar;
	shared_ptr<GameObject> MpBar;

	shared_ptr<GameObject> ChampMark;
};

