// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "HakCameraMode.generated.h"

/** Camera Blending ��� ���� */
UCLASS(Abstract, NotBlueprintable)
class UHakCameraMode : public UObject
{
	GENERATED_BODY()
public:
	UHakCameraMode(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());
};

/** Camera Blending�� ����ϴ� ��ü */
UCLASS()
class UHakCameraModeStack : public UObject
{
	GENERATED_BODY()
public:
	UHakCameraModeStack(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/**
	 * member variables
	 */

	 /** ������ CameraMode�� ���� */
	UPROPERTY()
	TArray<TObjectPtr<UHakCameraMode>> CameraModeInstances;

	/** Camera Matrix Blend ������Ʈ ���� ť */
	UPROPERTY()
	TArray<TObjectPtr<UHakCameraMode>> CameraModeStack;
};