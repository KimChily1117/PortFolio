#pragma once
#include "ISkill.h"

class GarenESpell : public ISkill
{
	// ISkill을(를) 통해 상속됨
	void Use(shared_ptr<GameObject> caster, shared_ptr<GameObject> target) override;
};

