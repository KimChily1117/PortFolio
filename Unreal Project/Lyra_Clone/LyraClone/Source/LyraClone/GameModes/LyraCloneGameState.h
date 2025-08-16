// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameStateBase.h"
#include "LyraCloneGameState.generated.h"


class UExperienceManagerComponent;


/**
 * 
 */
UCLASS()
class LYRACLONE_API ALyraCloneGameState : public AGameStateBase
{
	GENERATED_BODY()
	
public:
	ALyraCloneGameState();


	UPROPERTY()
	TObjectPtr<UExperienceManagerComponent> ExperienceManagerComponent;
};
