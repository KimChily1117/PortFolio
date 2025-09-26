// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "HakExperienceActionSet.generated.h"

class UGameFeatureAction;

/**
 * 
 */
UCLASS()
class HAKGAME_API UHakExperienceActionSet : public UPrimaryDataAsset
{
	GENERATED_BODY()
public:
	UHakExperienceActionSet();

	/**
	 * member variables
	 */
	UPROPERTY(EditAnywhere, Category = "Actions to Perform")
	TArray<TObjectPtr<UGameFeatureAction>> Actions;
};
