// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "CommonPlayerController.h"
#include "HakPlayerController.generated.h"

class UHakAbilitySystemComponent;
class AHakPlayerState;
/**
 * 
 */
UCLASS()
class HAKGAME_API AHakPlayerController : public ACommonPlayerController
{
	GENERATED_BODY()
public:
	AHakPlayerController(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/**
	* PlayerController interface
	*/
	virtual void PostProcessInput(const float DeltaTime, const bool bGamePaused) override;

	/**
	 * member methods
	 */
	AHakPlayerState* GetHakPlayerState() const;
	UHakAbilitySystemComponent* GetHakAbilitySystemComponent() const;
};
