// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "LyraClone/AbilitySystem/Abilities/LyraCloneGameplayAbility.h"
#include "LCGameplayAbility_FromEquipment.generated.h"

class ULyraCloneEquipmentInstance;
class ULyraCloneInventoryItemInstance;
/**
 * 
 */
UCLASS()
class LYRACLONE_API ULCGameplayAbility_FromEquipment : public ULyraCloneGameplayAbility
{
	GENERATED_BODY()
public:
	ULyraCloneEquipmentInstance* GetAssociatedEquipment() const;
	ULyraCloneInventoryItemInstance* GetAssociatedItem() const;
};
