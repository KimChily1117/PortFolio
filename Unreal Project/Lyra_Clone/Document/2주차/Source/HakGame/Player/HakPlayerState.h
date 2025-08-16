// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/PlayerState.h"
#include "HakPlayerState.generated.h"

class UHakPawnData;
class UHakExperienceDefinition;

/**
 * 
 */
UCLASS()
class HAKGAME_API AHakPlayerState : public APlayerState
{
	GENERATED_BODY()
public:
	/**
	 * AActor's interface
	 */
	virtual void PostInitializeComponents() final;

	/**
	 * member methods
	 */
	void OnExperienceLoaded(const UHakExperienceDefinition* CurrentExperience);

	UPROPERTY()
	TObjectPtr<const UHakPawnData> PawnData;
};
