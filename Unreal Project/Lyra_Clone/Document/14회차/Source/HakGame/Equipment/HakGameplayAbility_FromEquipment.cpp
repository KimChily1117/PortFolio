// Fill out your copyright notice in the Description page of Project Settings.

#include "HakGameplayAbility_FromEquipment.h"
#include "HakEquipmentInstance.h"
#include "HakGame/Inventory/HakInventoryItemInstance.h"

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

UHakInventoryItemInstance* UHakGameplayAbility_FromEquipment::GetAssociatedItem() const
{
	if (UHakEquipmentInstance* Equipment = GetAssociatedEquipment())
	{
		// In Lyra, equipment is equipped by inventory item instance:
		// - so, equipment's instigator should be inventory item instance
		// - otherwise, it will return nullptr by failing casting to HakInventoryItemInstance
		return Cast<UHakInventoryItemInstance>(Equipment->GetInstigator());
	}
	return nullptr;
}
