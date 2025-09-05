// Fill out your copyright notice in the Description page of Project Settings.

#include "HakPlayerController.h"

#include "HakPlayerState.h"
#include "HakGame/AbilitySystem/HakAbilitySystemComponent.h"
#include "HakGame/Camera/HakPlayerCameraManager.h"

AHakPlayerController::AHakPlayerController(const FObjectInitializer& ObjectInitializer): Super(ObjectInitializer)
{
	PlayerCameraManagerClass = AHakPlayerCameraManager::StaticClass();
}

void AHakPlayerController::PostProcessInput(const float DeltaTime, const bool bGamePaused)
{
	// 우선 PostProcessInput()가 언제 호출되는지 확인해보자:
	// - UPlayerInput::ProcessInputStack()에서 호출된다

	if (UHakAbilitySystemComponent* HakASC = GetHakAbilitySystemComponent())
	{
		HakASC->ProcessAbilityInput(DeltaTime, bGamePaused);
	}

	Super::PostProcessInput(DeltaTime, bGamePaused);
}

AHakPlayerState* AHakPlayerController::GetHakPlayerState() const
{
	// ECastCheckedType의 NullAllowed는 Null 반환을 의도할 경우 유용하다
	return CastChecked<AHakPlayerState>(PlayerState, ECastCheckedType::NullAllowed);
}

UHakAbilitySystemComponent* AHakPlayerController::GetHakAbilitySystemComponent() const
{
	const AHakPlayerState* HakPS = GetHakPlayerState();
	return (HakPS ? HakPS->GetHakAbilitySystemComponent() : nullptr);
}
