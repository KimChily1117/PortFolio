// Fill out your copyright notice in the Description page of Project Settings.


#include "LCGameplayAbility_FromEquipment.h"
#include "LyraCloneEquipmentInstance.h"
#include "LyraClone/Inventory/LyraCloneInventoryItemInstance.h"

ULyraCloneEquipmentInstance* ULCGameplayAbility_FromEquipment::GetAssociatedEquipment() const
{
	// CurrentActorInfo�� AbilitySystemComponent�� CurrentSpecHandle�� Ȱ���Ͽ�, GameplayAbilitySpec�� ������:
	// - CurrentSpecHandle�� SetCurrentActorInfo() ȣ���� ��, Handle ���� �޾Ƽ� �����:
	// - CurrentSpecHandle�� CurrentActorInfo�� ���� ��
	// - FindAbilitySpecFromHandle�� GiveAbility�� ���� ActivatableAbilities�� ��ȸ�Ͽ� GameplayAbilitySpec�� ã�Ƴ�
	if (FGameplayAbilitySpec* Spec = UGameplayAbility::GetCurrentAbilitySpec())
	{
		// GameplayAbility_FromEquipment�� EquipmentInstance�κ��� GiveAbility�� ���������Ƿ�, SourceObject�� EquipmentInstance�� ����Ǿ� ����
		return Cast<ULyraCloneEquipmentInstance>(Spec->SourceObject.Get());
	}
	return nullptr;
}

ULyraCloneInventoryItemInstance* ULCGameplayAbility_FromEquipment::GetAssociatedItem() const
{
	if (ULyraCloneEquipmentInstance* Equipment = GetAssociatedEquipment())
	{
		// In Lyra, equipment is equipped by inventory item instance:
		// - so, equipment's instigator should be inventory item instance
		// - otherwise, it will return nullptr by failing casting to HakInventoryItemInstance
		return Cast<ULyraCloneInventoryItemInstance>(Equipment->GetInstigator());
	}
	return nullptr;
}
