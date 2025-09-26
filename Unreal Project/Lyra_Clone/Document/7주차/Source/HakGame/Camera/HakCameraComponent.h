// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "HakCameraMode.h"
#include "Camera/CameraComponent.h"
#include "HakCameraComponent.generated.h"

class UHakCameraModeStack;

/** template forward declaration */
template <class TClass> class TSubclassOf;

/** (return type, delegate_name) */
DECLARE_DELEGATE_RetVal(TSubclassOf<UHakCameraMode>, FHakCameraModeDelegate);

UCLASS()
class HAKGAME_API UHakCameraComponent : public UCameraComponent
{
	GENERATED_BODY()
public:
	UHakCameraComponent(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	static UHakCameraComponent* FindCameraComponent(const AActor* Actor) { return (Actor ? Actor->FindComponentByClass<UHakCameraComponent>() : nullptr); }

	/**
	* member methods
	*/
	AActor* GetTargetActor() const { return GetOwner(); }
	void UpdateCameraModes();

	/**
	 * CameraComponent interface
	 */
	virtual void OnRegister() final;
	virtual void GetCameraView(float DeltaTime, FMinimalViewInfo& DesiredView) final;

	/**
	 * member variables
	 */
	/** ī�޶��� blending ����� �����ϴ� stack */
	UPROPERTY()
	TObjectPtr<UHakCameraModeStack> CameraModeStack;

	/** ���� CameraMode�� �������� Delegate */
	FHakCameraModeDelegate DetermineCameraModeDelegate;
};
