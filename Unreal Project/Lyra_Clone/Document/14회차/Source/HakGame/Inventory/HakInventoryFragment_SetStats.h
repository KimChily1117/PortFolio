// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameplayTagContainer.h"
#include "HakInventoryItemDefinition.h"
#include "HakInventoryFragment_SetStats.generated.h"

/**
 * 
 */
UCLASS()
class HAKGAME_API UHakInventoryFragment_SetStats : public UHakInventoryItemFragment
{
	GENERATED_BODY()
public:
	virtual void OnInstanceCreated(UHakInventoryItemInstance* Instance) const override;

	/** InitialItemStats gives constructor's parameters for HakGameplayTagStackContainer */
	UPROPERTY(EditDefaultsOnly, Category = Equipment)
	TMap<FGameplayTag, int32> InitialItemStats;
};
