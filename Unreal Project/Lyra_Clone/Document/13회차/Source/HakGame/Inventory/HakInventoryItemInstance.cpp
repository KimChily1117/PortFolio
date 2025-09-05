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
		// HakInventoryItemDefinition은 모든 멤버 변수가 EditDefaultsOnly로 선언되어 있으므로, GetDefault로 가져와도 무관하다
		// - Fragment 정보는 Instance가 아닌 Definition에 있다
		return GetDefault<UHakInventoryItemDefinition>(ItemDef)->FindFragmentByClass(FragmentClass);
	}

	return nullptr;
}
