// Fill out your copyright notice in the Description page of Project Settings.

#include "HakGameplayAbility_FromEquipment.h"
#include "HakEquipmentInstance.h"

UHakEquipmentInstance* UHakGameplayAbility_FromEquipment::GetAssociatedEquipment() const
{
	// CurrentActorInfo�� AbilitySystemComponent�� CurrentSpecHandle�� Ȱ���Ͽ�, GameplayAbilitySpec�� ������:
	// - CurrentSpecHandle�� SetCurrentActorInfo() ȣ���� ��, Handle ���� �޾Ƽ� �����:
	// - CurrentSpecHandle�� CurrentActorInfo�� ���� ��
	// - FindAbilitySpecFromHandle�� GiveAbility�� ���� ActivatableAbilities�� ��ȸ�Ͽ� GameplayAbilitySpec�� ã�Ƴ�
	if (FGameplayAbilitySpec* Spec = UGameplayAbility::GetCurrentAbilitySpec())
	{
		// GameplayAbility_FromEquipment�� EquipmentInstance�κ��� GiveAbility�� ���������Ƿ�, SourceObject�� EquipmentInstance�� ����Ǿ� ����
		return Cast<UHakEquipmentInstance>(Spec->SourceObject.Get());
	}
	return nullptr;
}
