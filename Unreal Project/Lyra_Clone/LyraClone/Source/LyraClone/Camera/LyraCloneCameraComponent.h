// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Camera/CameraComponent.h"
#include "LyraCloneCameraMode.h"
#include "LyraCloneCameraComponent.generated.h"

/**
 * 
 */
class ULyraCloneCameraModeStack;

/** template forward declaration */
template <class TClass> class TSubcSlassOf;

DECLARE_DELEGATE_RetVal(TSubclassOf<ULyraCloneCameraMode>, FLyraCloneCameraModeDelegate);



UCLASS()
class LYRACLONE_API ULyraCloneCameraComponent : public UCameraComponent
{
	GENERATED_BODY()
public:
	ULyraCloneCameraComponent(const FObjectInitializer& ObjectInitializer);
	

	static ULyraCloneCameraComponent* FindCameraComponent(const AActor* Actor) { return (Actor ? Actor->FindComponentByClass<ULyraCloneCameraComponent>() : nullptr); }

	/**
	* member methods
	*/
	AActor* GetTargetActor() const { return GetOwner(); }
	void UpdateCameraModes();


public:
	//CameraComponent Interface

	virtual void OnRegister() final;
	virtual void GetCameraView(float DeataTime, FMinimalViewInfo& DesiredView) final;

public:

	/**
	 * member variables
	/** 카메라의 blending 기능을 지원하는 stack */
	UPROPERTY()
	TObjectPtr<ULyraCloneCameraModeStack> CameraModeStack;


	/** 현재 CameraMode를 가져오는 Delegate */
	FLyraCloneCameraModeDelegate DetermineCameraModeDelegate;
};
