// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameStateBase.h"
#include "HakGameState.generated.h"

class UHakExperienceManagerComponent;
/**
 * 
 */
UCLASS()
class HAKGAME_API AHakGameState : public AGameStateBase
{
	GENERATED_BODY()
public:
	AHakGameState();

public:
	UPROPERTY()
	TObjectPtr<UHakExperienceManagerComponent> ExperienceManagerComponent;
};
