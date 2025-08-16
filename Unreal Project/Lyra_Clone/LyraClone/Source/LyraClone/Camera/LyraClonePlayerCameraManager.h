// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Camera/PlayerCameraManager.h"
#include "LyraClonePlayerCameraManager.generated.h"

/**
 * PlayerController¿¡ BindingÇÏ´Â Manager
 */

#define HAK_CAMERA_DEFAULT_FOV (80.0f)
#define HAK_CAMERA_DEFAULT_PITCH_MIN (-89.0f)
#define HAK_CAMERA_DEFAULT_PITCH_MAX (89.0f)

UCLASS()
class LYRACLONE_API ALyraClonePlayerCameraManager : public APlayerCameraManager
{
	GENERATED_BODY()

public:

	ALyraClonePlayerCameraManager(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());
	
};
