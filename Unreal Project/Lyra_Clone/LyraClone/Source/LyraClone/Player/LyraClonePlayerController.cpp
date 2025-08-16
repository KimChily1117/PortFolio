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
	// �켱 PostProcessInput()�� ���� ȣ��Ǵ��� Ȯ���غ���:
	// - UPlayerInput::ProcessInputStack()���� ȣ��ȴ�

	if (ULyraCloneAbilitySystemComponent* HakASC = GetHakAbilitySystemComponent())
	{
		HakASC->ProcessAbilityInput(DeltaTime, bGamePaused);
	}

	Super::PostProcessInput(DeltaTime, bGamePaused);
}

ALyraClonePlayerState* ALyraClonePlayerController::GetHakPlayerState() const
{
	// ECastCheckedType�� NullAllowed�� Null ��ȯ�� �ǵ��� ��� �����ϴ�
	return CastChecked<ALyraClonePlayerState>(PlayerState, ECastCheckedType::NullAllowed);
}

ULyraCloneAbilitySystemComponent* ALyraClonePlayerController::GetHakAbilitySystemComponent() const
{
	const ALyraClonePlayerState* HakPS = GetHakPlayerState();
	return (HakPS ? HakPS->GetHakAbilitySystemComponent() : nullptr);
}
   