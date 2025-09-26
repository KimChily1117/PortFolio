// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "HakGame/Cosmetics/HakCosmeticAnimationTypes.h"
#include "HakGame/Equipment/HakEquipmentInstance.h"
#include "HakWeaponInstance.generated.h"

/**
 * 
 */
UCLASS()
class HAKGAME_API UHakWeaponInstance : public UHakEquipmentInstance
{
	GENERATED_BODY()
public:
	UHakWeaponInstance(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/** Weapon�� Equip/Unequip�� ���� Animation Set ������ ��� �ִ� */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = Animation)
	FHakAnimLayerSelectionSet EquippedAnimSet;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = Animation)
	FHakAnimLayerSelectionSet UnequippedAnimSet;
};
