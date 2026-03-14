// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "LyraCloneInventoryManagerComponent.generated.h"

/** forward declarations */
class ULyraCloneInventoryItemInstance;
class ULyraCloneInventoryItemDefinition;

/** Inventory Item 단위 객체 */
USTRUCT(BlueprintType)
struct FLyraCloneInventoryEntry
{
	GENERATED_BODY()

	UPROPERTY()
	TObjectPtr<ULyraCloneInventoryItemInstance> Instance = nullptr;
};

/** Inventory Item 관리 객체 */
USTRUCT(BlueprintType)
struct FLyraCloneInventoryList
{
	GENERATED_BODY()

	FLyraCloneInventoryList(UActorComponent* InOwnerComponent = nullptr) : OwnerComponent(InOwnerComponent)
	{}

	ULyraCloneInventoryItemInstance* AddEntry(TSubclassOf<ULyraCloneInventoryItemDefinition> ItemDef);
	ULyraCloneInventoryItemInstance* RemoveEntry(ULyraCloneInventoryItemInstance* ItemInstance);

	UPROPERTY()
	TArray<FLyraCloneInventoryEntry> Entries;

	UPROPERTY()
	TObjectPtr<UActorComponent> OwnerComponent;
};

// LyraCloneInventoryManagerComponent.h
DECLARE_MULTICAST_DELEGATE_OneParam(FOnItemAdded, ULyraCloneInventoryItemInstance* /*NewItem*/);



/**
 * PlayerController의 Component로서 Inventory를 관리한다
 * - 사실 UActorComponent 상속이 아닌 UControllerComponent를 상속받아도 될거 같은데... 일단 Lyra 기준으로 UActorComponent를 상속받고 있다
 */
UCLASS( ClassGroup=(Custom), meta=(BlueprintSpawnableComponent) )
class LYRACLONE_API ULyraCloneInventoryManagerComponent : public UActorComponent
{
	GENERATED_BODY()
public:
	ULyraCloneInventoryManagerComponent(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());


	/** InventoryItemDefinition을 통해, InventoryList에 추가하여 관리하며, InventoryItemInstance를 반환한다 */
	UFUNCTION(BlueprintCallable, Category = Inventory)
	ULyraCloneInventoryItemInstance* AddItemDefinition(TSubclassOf<ULyraCloneInventoryItemDefinition> ItemDef);

	UFUNCTION(BlueprintCallable, Category = Inventory)
	ULyraCloneInventoryItemInstance* RemoveItemInstance(ULyraCloneInventoryItemInstance* ItemInstance);




	UPROPERTY()
	FLyraCloneInventoryList InventoryList;


public:
	FOnItemAdded OnItemAdded;
};
