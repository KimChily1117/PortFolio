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

void UHakInventoryItemInstance::AddStatTagStack(FGameplayTag Tag, int32 StackCount)
{
	StatTags.AddStack(Tag, StackCount);
}

void UHakInventoryItemInstance::RemoveStatTagStack(FGameplayTag Tag, int32 StackCount)
{
	StatTags.RemoveStack(Tag, StackCount);
}

int32 UHakInventoryItemInstance::GetStatTagStackCount(FGameplayTag Tag) const
{
	return StatTags.GetStackCount(Tag);
}

bool UHakInventoryItemInstance::HasStatTag(FGameplayTag Tag) const
{
	return StatTags.ContainsTag(Tag);
}