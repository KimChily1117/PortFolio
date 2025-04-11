#pragma once
#include "ISkill.h"

class AnnieQSpell : public ISkill
{
public:
	void Use(shared_ptr<GameObject> caster, shared_ptr<GameObject> target) override;
};

