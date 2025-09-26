// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "ModularPlayerController.h"
#include "HakPlayerController.generated.h"

/**
 * 
 */
UCLASS()
class HAKGAME_API AHakPlayerController : public AModularPlayerController
{
	GENERATED_BODY()
public:
	AHakPlayerController(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());
};
