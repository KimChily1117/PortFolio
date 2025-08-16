// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameplayTagContainer.h"
#include "LyraCloneInventoryItemDefinition.h"
#include "LyraCloneInventoryFragment_SetStats.generated.h"

/**
 * 
 */
UCLASS()
class LYRACLONE_API ULyraCloneInventoryFragment_SetStats : public ULyraCloneInventoryItemFragment
{
	GENERATED_BODY()
public:
	virtual void OnInstanceCreated(ULyraCloneInventoryItemInstance* Instance) const override;

	/** InitialItemStats gives constructor's parameters for LyraCloneGameplayTagStackContainer */
	UPROPERTY(EditDefaultsOnly, Category = Equipment)
	TMap<FGameplayTag, int32> InitialItemStats;
};
