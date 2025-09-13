// Fill out your copyright notice in the Description page of Project Settings.

#include "HakInventoryFragment_SetStats.h"
#include "HakInventoryItemInstance.h"

void UHakInventoryFragment_SetStats::OnInstanceCreated(UHakInventoryItemInstance* Instance) const
{
	// iterating InitialItemStats and add stat tag to InventoryItemInstance
	for (const auto& InitialItemStat : InitialItemStats)
	{
		Instance->AddStatTagStack(InitialItemStat.Key, InitialItemStat.Value);
	}
}