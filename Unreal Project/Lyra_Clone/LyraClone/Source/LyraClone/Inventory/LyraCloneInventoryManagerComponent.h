// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "LyraCloneInventoryManagerComponent.generated.h"

/** forward declarations */
class ULyraCloneInventoryItemInstance;
class ULyraCloneInventoryItemDefinition;

/** Inventory Item ���� ��ü */
USTRUCT(BlueprintType)
struct FLyraCloneInventoryEntry
{
	GENERATED_BODY()

	UPROPERTY()
	TObjectPtr<ULyraCloneInventoryItemInstance> Instance = nullptr;
};

/** Inventory Item ���� ��ü */
USTRUCT(BlueprintType)
struct FLyraCloneInventoryList
{
	GENERATED_BODY()

	FLyraCloneInventoryList(UActorComponent* InOwnerComponent = nullptr) : OwnerComponent(InOwnerComponent)
	{}

	ULyraCloneInventoryItemInstance* AddEntry(TSubclassOf<ULyraCloneInventoryItemDefinition> ItemDef);

	UPROPERTY()
	TArray<FLyraCloneInventoryEntry> Entries;

	UPROPERTY()
	TObjectPtr<UActorComponent> OwnerComponent;
};

// LyraCloneInventoryManagerComponent.h
DECLARE_MULTICAST_DELEGATE_OneParam(FOnItemAdded, ULyraCloneInventoryItemInstance* /*NewItem*/);



/**
 * PlayerController�� Component�μ� Inventory�� �����Ѵ�
 * - ��� UActorComponent ����� �ƴ� UControllerComponent�� ��ӹ޾Ƶ� �ɰ� ������... �ϴ� Lyra �������� UActorComponent�� ��ӹް� �ִ�
 */
UCLASS( ClassGroup=(Custom), meta=(BlueprintSpawnableComponent) )
class LYRACLONE_API ULyraCloneInventoryManagerComponent : public UActorComponent
{
	GENERATED_BODY()
public:
	ULyraCloneInventoryManagerComponent(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());


	/** InventoryItemDefinition�� ����, InventoryList�� �߰��Ͽ� �����ϸ�, InventoryItemInstance�� ��ȯ�Ѵ� */
	UFUNCTION(BlueprintCallable, Category = Inventory)
	ULyraCloneInventoryItemInstance* AddItemDefinition(TSubclassOf<ULyraCloneInventoryItemDefinition> ItemDef);

	UPROPERTY()
	FLyraCloneInventoryList InventoryList;


public:
	FOnItemAdded OnItemAdded;
};
