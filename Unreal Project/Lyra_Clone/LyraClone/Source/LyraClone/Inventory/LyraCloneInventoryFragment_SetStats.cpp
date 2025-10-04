// Fill out your copyright notice in the Description page of Project Settings.

#include "LyraCloneInventoryFragment_SetStats.h"
#include "LyraCloneInventoryItemInstance.h"

void ULyraCloneInventoryFragment_SetStats::OnInstanceCreated(ULyraCloneInventoryItemInstance* Instance) const
{
	// iterating InitialItemStats and add stat tag to InventoryItemInstance
	for (const auto& InitialItemStat : InitialItemStats)
	{
		Instance->AddStatTagStack(InitialItemStat.Key, InitialItemStat.Value);
	}
}

int32 ULyraCloneInventoryFragment_SetStats::GetItemStatByTag(FGameplayTag Tag) const
{
	if (const int32* StatPtr = InitialItemStats.Find(Tag))
	{
		return *StatPtr;
	}

	return 0;
}