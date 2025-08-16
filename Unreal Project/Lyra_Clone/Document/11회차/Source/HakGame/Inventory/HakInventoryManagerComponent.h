// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HakInventoryManagerComponent.generated.h"

/** forward declarations */
class UHakInventoryItemInstance;
class UHakInventoryItemDefinition;

/** Inventory Item 단위 객체 */
USTRUCT(BlueprintType)
struct FHakInventoryEntry
{
	GENERATED_BODY()

	UPROPERTY()
	TObjectPtr<UHakInventoryItemInstance> Instance = nullptr;
};

/** Inventory Item 관리 객체 */
USTRUCT(BlueprintType)
struct FHakInventoryList
{
	GENERATED_BODY()

	FHakInventoryList(UActorComponent* InOwnerComponent = nullptr) : OwnerComponent(InOwnerComponent)
	{}

	UHakInventoryItemInstance* AddEntry(TSubclassOf<UHakInventoryItemDefinition> ItemDef);

	UPROPERTY()
	TArray<FHakInventoryEntry> Entries;

	UPROPERTY()
	TObjectPtr<UActorComponent> OwnerComponent;
};

/**
 * PlayerController의 Component로서 Inventory를 관리한다
 * - 사실 UActorComponent 상속이 아닌 UControllerComponent를 상속받아도 될거 같은데... 일단 Lyra 기준으로 UActorComponent를 상속받고 있다
 */
UCLASS( ClassGroup=(Custom), meta=(BlueprintSpawnableComponent) )
class HAKGAME_API UHakInventoryManagerComponent : public UActorComponent
{
	GENERATED_BODY()
public:
	UHakInventoryManagerComponent(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/** InventoryItemDefinition을 통해, InventoryList에 추가하여 관리하며, InventoryItemInstance를 반환한다 */
	UFUNCTION(BlueprintCallable, Category = Inventory)
	UHakInventoryItemInstance* AddItemDefinition(TSubclassOf<UHakInventoryItemDefinition> ItemDef);

	UPROPERTY()
	FHakInventoryList InventoryList;
};
