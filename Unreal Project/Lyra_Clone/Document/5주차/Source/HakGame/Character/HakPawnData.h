// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "HakPawnData.generated.h"

class UHakInputConfig;
class UHakCameraMode;

/**
 * 
 */
UCLASS()
class HAKGAME_API UHakPawnData : public UPrimaryDataAsset
{
	GENERATED_BODY()
public:
	UHakPawnData(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/** Pawn¿« Class */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Hak|Pawn")
	TSubclassOf<APawn> PawnClass;

	/** Camera Mode */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Hak|Camera")
	TSubclassOf<UHakCameraMode> DefaultCameraMode;

	/** input configuration used by player controlled pawns to create input mappings and bind input actions */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Hak|InputConfig")
	TObjectPtr<UHakInputConfig> InputConfig;
};
