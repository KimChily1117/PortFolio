// Fill out your copyright notice in the Description page of Project Settings.


#include "HakInventoryManagerComponent.h"
#include "HakInventoryItemDefinition.h"
#include "HakInventoryItemInstance.h"

UHakInventoryItemInstance* FHakInventoryList::AddEntry(TSubclassOf<UHakInventoryItemDefinition> ItemDef)
{
	UHakInventoryItemInstance* Result = nullptr;
	check(ItemDef);
	check(OwnerComponent);

	AActor* OwningActor = OwnerComponent->GetOwner();
	check(OwningActor->HasAuthority());

	FHakInventoryEntry& NewEntry = Entries.AddDefaulted_GetRef();
	NewEntry.Instance = NewObject<UHakInventoryItemInstance>(OwningActor);
	NewEntry.Instance->ItemDef = ItemDef;

	Result = NewEntry.Instance;
	return Result;
}

UHakInventoryManagerComponent::UHakInventoryManagerComponent(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer)
	, InventoryList(this)
{}

UHakInventoryItemInstance* UHakInventoryManagerComponent::AddItemDefinition(TSubclassOf<UHakInventoryItemDefinition> ItemDef)
{
	UHakInventoryItemInstance* Result = nullptr;
	if (ItemDef)
	{
		Result = InventoryList.AddEntry(ItemDef);
	}
	return Result;
}
