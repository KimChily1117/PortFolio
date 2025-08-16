// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/PlayerController.h"
#include "ModularPlayerController.h"
#include "LyraClonePlayerController.generated.h"


class ULyraCloneAbilitySystemComponent;
class ALyraClonePlayerState;

/**
 * 
 */
UCLASS()
class LYRACLONE_API ALyraClonePlayerController : public AModularPlayerController
{
	GENERATED_BODY()
	
public:
	ALyraClonePlayerController(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/**
		* PlayerController interface
		*/
	virtual void PostProcessInput(const float DeltaTime, const bool bGamePaused) override;

	/**
	 * member methods
	 */
	ALyraClonePlayerState* GetHakPlayerState() const;
	ULyraCloneAbilitySystemComponent* GetHakAbilitySystemComponent() const;
};
