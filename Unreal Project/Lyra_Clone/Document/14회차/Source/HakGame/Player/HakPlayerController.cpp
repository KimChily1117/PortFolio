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
	// �켱 PostProcessInput()�� ���� ȣ��Ǵ��� Ȯ���غ���:
	// - UPlayerInput::ProcessInputStack()���� ȣ��ȴ�

	if (UHakAbilitySystemComponent* HakASC = GetHakAbilitySystemComponent())
	{
		HakASC->ProcessAbilityInput(DeltaTime, bGamePaused);
	}

	Super::PostProcessInput(DeltaTime, bGamePaused);
}

AHakPlayerState* AHakPlayerController::GetHakPlayerState() const
{
	// ECastCheckedType�� NullAllowed�� Null ��ȯ�� �ǵ��� ��� �����ϴ�
	return CastChecked<AHakPlayerState>(PlayerState, ECastCheckedType::NullAllowed);
}

UHakAbilitySystemComponent* AHakPlayerController::GetHakAbilitySystemComponent() const
{
	const AHakPlayerState* HakPS = GetHakPlayerState();
	return (HakPS ? HakPS->GetHakAbilitySystemComponent() : nullptr);
}
