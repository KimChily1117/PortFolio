// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "HakPawnData.generated.h"

/**
 * 
 */
UCLASS()
class HAKGAME_API UHakPawnData : public UPrimaryDataAsset
{
	GENERATED_BODY()
public:
	UHakPawnData(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());
	
};
