// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "AIController.h"
#include "LyraCloneAIController.generated.h"

/**
 * 봇용 AIController
 * - bWantsPlayerState = true 로 설정해서 AI도 PlayerState를 생성하게 함
 * - PlayerState 기반(ASC/AttributeSet) GAS 구조를 봇에도 그대로 적용하기 위함
 */


UCLASS()
class LYRACLONE_API ALyraCloneAIController : public AAIController
{
	GENERATED_BODY()
	
public:
	ALyraCloneAIController(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());
	void OnPossess(APawn* InPawn);
	void OnUnPossess();
};
