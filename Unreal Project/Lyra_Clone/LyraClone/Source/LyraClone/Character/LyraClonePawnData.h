// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "LyraClone/Camera/LyraCloneCameraMode.h"
#include "LyraClone/Input/LyraCloneInputConfig.h"
#include "LyraClonePawnData.generated.h"


class ULyraCloneAbilitySet;

/**
 * 
 */
UCLASS()
class LYRACLONE_API ULyraClonePawnData : public UPrimaryDataAsset
{
	GENERATED_BODY()

public:
	ULyraClonePawnData(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());



	/** Pawn�� Class */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Hak|Pawn")
	TSubclassOf<APawn> PawnClass;
	/** Pawn�� Class */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Hak|Camera")
	TSubclassOf<ULyraCloneCameraMode> DefaultCameraMode;
	/** input configuration used by player controlled pawns to create input mappings and bind input actions */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Hak|InputConfig")
	TObjectPtr<ULyraCloneInputConfig> InputConfig;

	/** �ش� Pawn�� Ability System�� ����� AbilitySet */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Hak|Abilities")
	TArray<TObjectPtr<ULyraCloneAbilitySet>> AbilitySets;
};
