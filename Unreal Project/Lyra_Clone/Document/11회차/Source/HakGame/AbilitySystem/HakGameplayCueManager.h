// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameplayCueManager.h"
#include "HakGameplayCueManager.generated.h"

/**
 * 
 */
UCLASS()
class HAKGAME_API UHakGameplayCueManager : public UGameplayCueManager
{
	GENERATED_BODY()
public:
	static UHakGameplayCueManager* Get();

	UHakGameplayCueManager(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	void RefreshGameplayCuePrimaryAsset();
};
