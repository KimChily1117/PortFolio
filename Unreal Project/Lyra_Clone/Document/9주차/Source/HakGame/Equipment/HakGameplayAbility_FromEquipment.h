// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "HakGame/AbilitySystem/Abilities/HakGameplayAbility.h"
#include "HakGameplayAbility_FromEquipment.generated.h"

class UHakEquipmentInstance;
/**
 * 
 */
UCLASS()
class HAKGAME_API UHakGameplayAbility_FromEquipment : public UHakGameplayAbility
{
	GENERATED_BODY()
public:
	UHakEquipmentInstance* GetAssociatedEquipment() const;
};
