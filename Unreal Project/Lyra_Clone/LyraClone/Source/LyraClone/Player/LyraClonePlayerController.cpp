// Fill out your copyright notice in the Description page of Project Settings.

#include "LyraClonePlayerController.h"
#include "LyraClonePlayerState.h"
#include "LyraClone/Camera/LyraClonePlayerCameraManager.h"
#include "LyraClone/AbilitySystem/LyraCloneAbilitySystemComponent.h"

ALyraClonePlayerController::ALyraClonePlayerController(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
	PlayerCameraManagerClass = ALyraClonePlayerCameraManager::StaticClass();
}

void ALyraClonePlayerController::PostProcessInput(const float DeltaTime, const bool bGamePaused)
{
	// 우선 PostProcessInput()가 언제 호출되는지 확인해보자:
	// - UPlayerInput::ProcessInputStack()에서 호출된다

	if (ULyraCloneAbilitySystemComponent* HakASC = GetHakAbilitySystemComponent())
	{
		HakASC->ProcessAbilityInput(DeltaTime, bGamePaused);
	}

	Super::PostProcessInput(DeltaTime, bGamePaused);
}

ALyraClonePlayerState* ALyraClonePlayerController::GetHakPlayerState() const
{
	// ECastCheckedType의 NullAllowed는 Null 반환을 의도할 경우 유용하다
	return CastChecked<ALyraClonePlayerState>(PlayerState, ECastCheckedType::NullAllowed);
}

ULyraCloneAbilitySystemComponent* ALyraClonePlayerController::GetHakAbilitySystemComponent() const
{
	const ALyraClonePlayerState* HakPS = GetHakPlayerState();
	return (HakPS ? HakPS->GetHakAbilitySystemComponent() : nullptr);
}
   