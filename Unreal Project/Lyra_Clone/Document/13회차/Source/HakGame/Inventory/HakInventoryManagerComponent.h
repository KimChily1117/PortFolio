// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HakInventoryManagerComponent.generated.h"

/** forward declarations */
class UHakInventoryItemInstance;
class UHakInventoryItemDefinition;

/** Inventory Item ���� ��ü */
USTRUCT(BlueprintType)
struct FHakInventoryEntry
{
	GENERATED_BODY()

	UPROPERTY()
	TObjectPtr<UHakInventoryItemInstance> Instance = nullptr;
};

/** Inventory Item ���� ��ü */
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
 * PlayerController�� Component�μ� Inventory�� �����Ѵ�
 * - ��� UActorComponent ����� �ƴ� UControllerComponent�� ��ӹ޾Ƶ� �ɰ� ������... �ϴ� Lyra �������� UActorComponent�� ��ӹް� �ִ�
 */
UCLASS( ClassGroup=(Custom), meta=(BlueprintSpawnableComponent) )
class HAKGAME_API UHakInventoryManagerComponent : public UActorComponent
{
	GENERATED_BODY()
public:
	UHakInventoryManagerComponent(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/** InventoryItemDefinition�� ����, InventoryList�� �߰��Ͽ� �����ϸ�, InventoryItemInstance�� ��ȯ�Ѵ� */
	UFUNCTION(BlueprintCallable, Category = Inventory)
	UHakInventoryItemInstance* AddItemDefinition(TSubclassOf<UHakInventoryItemDefinition> ItemDef);

	UPROPERTY()
	FHakInventoryList InventoryList;
};
