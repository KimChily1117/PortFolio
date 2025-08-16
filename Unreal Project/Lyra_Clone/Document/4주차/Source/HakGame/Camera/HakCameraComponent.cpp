// Fill out your copyright notice in the Description page of Project Settings.

#include "HakCameraComponent.h"

#include "HakCameraMode.h"

UHakCameraComponent::UHakCameraComponent(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer), CameraModeStack(nullptr)
{
	
}

void UHakCameraComponent::OnRegister()
{
	Super::OnRegister();

	if (!CameraModeStack)
	{
		// �ʱ�ȭ (BeginPlay�� ����)�� ���� �ʿ���� ��ü�� NewObject�� �Ҵ�
		CameraModeStack = NewObject<UHakCameraModeStack>(this);
	}
}

void UHakCameraComponent::GetCameraView(float DeltaTime, FMinimalViewInfo& DesiredView)
{
	check(CameraModeStack);

	UpdateCameraModes();
}

PRAGMA_DISABLE_OPTIMIZATION
void UHakCameraComponent::UpdateCameraModes()
{
	check(CameraModeStack);

	// DetermineCameraModeDelegate�� CameraMode Class�� ��ȯ�Ѵ�:
	// - �ش� delegate�� HeroComponent�� ��� �Լ��� ���ε��Ǿ� �ִ�
	if (DetermineCameraModeDelegate.IsBound())
	{
		if (const TSubclassOf<UHakCameraMode> CameraMode = DetermineCameraModeDelegate.Execute())
		{
			// CameraModeStack->PushCameraMode(CameraMode);
		}
	}
}
PRAGMA_ENABLE_OPTIMIZATION