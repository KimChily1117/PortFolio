// Fill out your copyright notice in the Description page of Project Settings.

#include "HakPlayerController.h"
#include "HakGame/Camera/HakPlayerCameraManager.h"

AHakPlayerController::AHakPlayerController(const FObjectInitializer& ObjectInitializer): Super(ObjectInitializer)
{
	PlayerCameraManagerClass = AHakPlayerCameraManager::StaticClass();
}