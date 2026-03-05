// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraCloneAIController.h"
#include <LyraClone\Character\LyraClonePawnExtensionComponent.h>

ALyraCloneAIController::ALyraCloneAIController(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer)
{
	// AI도 PlayerState를 만들게 해서 (GameMode의 PlayerStateClass를 사용)
	// PlayerState에 있는 ASC/HealthSet을 봇도 동일하게 사용 가능
	bWantsPlayerState = true;

	// (선택) 봇이 서버에서만 의미가 있으니, 필요하면 여기서도 가드/설정 추가 가능
	// bReplicates = true; // 보통 Controller는 기본적으로 복제되지만, 상황에 따라 체크
}

void ALyraCloneAIController::OnPossess(APawn* InPawn)
{
	Super::OnPossess(InPawn);

	if (ULyraClonePawnExtensionComponent* PawnExt = ULyraClonePawnExtensionComponent::FindPawnExtensionComponent(InPawn))
	{
		// ★ AI는 SetupPlayerInputComponent 안 타는 경우가 많아서 여기서 강제 초기화
		PawnExt->TryInitializeAbilitySystemFromPlayerState();

		// AI는 입력이 없으니 여기서 CheckDefaultInitialization까지 돌려도 부작용 거의 없음
		// (단, PawnData가 없으면 DataAvailable에서 멈출 수는 있음)
		PawnExt->CheckDefaultInitialization();
	}
}

void ALyraCloneAIController::OnUnPossess()
{
	// 필요하면 여기서 UninitializeAbilitySystem() 호출하는데,
	// 지금 단계에서는 “안정적으로 붙이는게 먼저”라서 굳이 건드리지 말자
	Super::OnUnPossess();
}