#pragma once
#include "ISkill.h"

class GarenESpell : public ISkill
{
	// ISkill��(��) ���� ��ӵ�
	void Use(shared_ptr<GameObject> caster, shared_ptr<GameObject> target) override;
};

