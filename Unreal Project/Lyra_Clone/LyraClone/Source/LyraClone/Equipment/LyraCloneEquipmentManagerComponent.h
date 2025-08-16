// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Components/PawnComponent.h"
#include "LyraClone/AbilitySystem/LyraCloneAbilitySet.h"
#include "LyraCloneEquipmentManagerComponent.generated.h"

/** forward declarations */
class ULyraCloneEquipmentDefinition;
class ULyraCloneEquipmentInstance;

USTRUCT(BlueprintType)
struct FLyraCloneAppliedEquipmentEntry
{
	GENERATED_BODY()

	/** �������� ���� ��Ÿ ������ */
	UPROPERTY()
	TSubclassOf<ULyraCloneEquipmentDefinition> EquipmentDefinition;

	/** EquipmentDefinition�� ���� �������� �ν��Ͻ� */
	UPROPERTY()
	TObjectPtr<ULyraCloneEquipmentInstance> Instance = nullptr;

	/** ���⿡ �Ҵ�� ��밡���� GameplayAbility */
	UPROPERTY()
	FLyraCloneAbilitySet_GrantedHandles GrantedHandles;
};

/**
 * ����� EquipmentInstance�� �ν��Ͻ��� Entry���� �����ϰ� �ִ�:
 * - LyraCloneEquipmentList�� ������ ��ü�� �����Ѵٰ� ���� �ȴ�
 */
USTRUCT(BlueprintType)
struct FLyraCloneEquipmentList
{
	GENERATED_BODY()

	FLyraCloneEquipmentList(UActorComponent* InOwnerComponent = nullptr)
		: OwnerComponent(InOwnerComponent)
	{}

	ULyraCloneEquipmentInstance* AddEntry(TSubclassOf<ULyraCloneEquipmentDefinition> EquipmentDefinition);
	void RemoveEntry(ULyraCloneEquipmentInstance* Instance);

	ULyraCloneAbilitySystemComponent* GetAbilitySystemComponent() const;

	/** �������� ���� ���� ����Ʈ */
	UPROPERTY()
	TArray<FLyraCloneAppliedEquipmentEntry> Entries;

	UPROPERTY()
	TObjectPtr<UActorComponent> OwnerComponent;
};

/**
 * Pawn�� Component�μ� �������� ���� ������ ����Ѵ�
 */
UCLASS()
class LYRACLONE_API ULyraCloneEquipmentManagerComponent : public UPawnComponent
{
	GENERATED_BODY()
public:
	ULyraCloneEquipmentManagerComponent(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	ULyraCloneEquipmentInstance* EquipItem(TSubclassOf<ULyraCloneEquipmentDefinition> EquipmentDefinition);
	void UnequipItem(ULyraCloneEquipmentInstance* ItemInstance);

	/** ������ �� ó�� ���� ��ȯ ������ NULL */
	ULyraCloneEquipmentInstance* GetFirstInstanceOfType(TSubclassOf<ULyraCloneEquipmentInstance> InstanceType);

	template <typename T>
	T* GetFirstInstanceOfType()
	{
		return (T*)GetFirstInstanceOfType(T::StaticClass());
	}

	UFUNCTION(BlueprintCallable)
	TArray<ULyraCloneEquipmentInstance*> GetEquipmentInstancesOfType(TSubclassOf<ULyraCloneEquipmentInstance> InstanceType) const;

	UPROPERTY()
	FLyraCloneEquipmentList EquipmentList;
};
