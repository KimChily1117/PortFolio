// Fill out your copyright notice in the Description page of Project Settings.


#include "HakInventoryItemInstance.h"

#include "HakInventoryItemDefinition.h"

UHakInventoryItemInstance::UHakInventoryItemInstance(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
}

const UHakInventoryItemFragment* UHakInventoryItemInstance::FindFragmentByClass(TSubclassOf<UHakInventoryItemFragment> FragmentClass) const
{
	if ((ItemDef != nullptr) && (FragmentClass != nullptr))
	{
		// HakInventoryItemDefinition�� ��� ��� ������ EditDefaultsOnly�� ����Ǿ� �����Ƿ�, GetDefault�� �����͵� �����ϴ�
		// - Fragment ������ Instance�� �ƴ� Definition�� �ִ�
		return GetDefault<UHakInventoryItemDefinition>(ItemDef)->FindFragmentByClass(FragmentClass);
	}

	return nullptr;
}
