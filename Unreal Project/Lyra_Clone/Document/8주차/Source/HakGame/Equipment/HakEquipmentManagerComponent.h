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

	/** �������� ���� ��Ÿ ������ */
	UPROPERTY()
	TSubclassOf<UHakEquipmentDefinition> EquipmentDefinition;

	/** EquipmentDefinition�� ���� �������� �ν��Ͻ� */
	UPROPERTY()
	TObjectPtr<UHakEquipmentInstance> Instance = nullptr;
};

/**
 * ����� EquipmentInstance�� �ν��Ͻ��� Entry���� �����ϰ� �ִ�:
 * - HakEquipmentList�� ������ ��ü�� �����Ѵٰ� ���� �ȴ�
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

	/** �������� ���� ���� ����Ʈ */
	UPROPERTY()
	TArray<FHakAppliedEquipmentEntry> Entries;

	UPROPERTY()
	TObjectPtr<UActorComponent> OwnerComponent;
};

/**
 * Pawn�� Component�μ� �������� ���� ������ ����Ѵ�
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
