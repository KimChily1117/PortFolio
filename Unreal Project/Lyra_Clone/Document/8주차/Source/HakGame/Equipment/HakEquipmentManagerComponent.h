// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Components/PawnComponent.h"
#include "HakEquipmentManagerComponent.generated.h"

/** forward declarations */
class UHakEquipmentDefinition;
class UHakEquipmentInstance;

USTRUCT(BlueprintType)
struct FHakAppliedEquipmentEntry
{
	GENERATED_BODY()

	/** 장착물에 대한 메타 데이터 */
	UPROPERTY()
	TSubclassOf<UHakEquipmentDefinition> EquipmentDefinition;

	/** EquipmentDefinition을 통해 생성도니 인스턴스 */
	UPROPERTY()
	TObjectPtr<UHakEquipmentInstance> Instance = nullptr;
};

/**
 * 참고로 EquipmentInstance의 인스턴스를 Entry에서 관리하고 있다:
 * - HakEquipmentList는 생성된 객체를 관리한다고 보면 된다
 */
USTRUCT(BlueprintType)
struct FHakEquipmentList
{
	GENERATED_BODY()

	FHakEquipmentList(UActorComponent* InOwnerComponent = nullptr)
		: OwnerComponent(InOwnerComponent)
	{}

	UHakEquipmentInstance* AddEntry(TSubclassOf<UHakEquipmentDefinition> EquipmentDefinition);
	void RemoveEntry(UHakEquipmentInstance* Instance);

	/** 장착물에 대한 관리 리스트 */
	UPROPERTY()
	TArray<FHakAppliedEquipmentEntry> Entries;

	UPROPERTY()
	TObjectPtr<UActorComponent> OwnerComponent;
};

/**
 * Pawn의 Component로서 장착물에 대한 관리를 담당한다
 */
UCLASS()
class HAKGAME_API UHakEquipmentManagerComponent : public UPawnComponent
{
	GENERATED_BODY()
public:
	UHakEquipmentManagerComponent(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	UHakEquipmentInstance* EquipItem(TSubclassOf<UHakEquipmentDefinition> EquipmentDefinition);
	void UnequipItem(UHakEquipmentInstance* ItemInstance);

	UPROPERTY()
	FHakEquipmentList EquipmentList;
};
