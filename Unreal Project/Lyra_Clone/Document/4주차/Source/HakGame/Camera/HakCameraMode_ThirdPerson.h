// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "HakCameraMode.h"
#include "HakCameraMode_ThirdPerson.generated.h"

/**
 * 
 */
UCLASS(Abstract , Blueprintable)
class HAKGAME_API UHakCameraMode_ThirdPerson : public UHakCameraMode
{
	GENERATED_BODY()
public:
	UHakCameraMode_ThirdPerson(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());
};
