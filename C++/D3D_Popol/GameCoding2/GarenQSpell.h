#pragma once
#include "ISkill.h"

class GarenQSpell : public ISkill
{
	// ISkill��(��) ���� ��ӵ�
	void Use(shared_ptr<GameObject> caster, shared_ptr<GameObject> target) override;
};

