// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameplayEffectExecutionCalculation.h"
#include "HakHealExecution.generated.h"

/**
 * 
 */
UCLASS()
class HAKGAME_API UHakHealExecution : public UGameplayEffectExecutionCalculation
{
	GENERATED_BODY()
public:
	UHakHealExecution();

	/** �ش� �޼���� GameplayEffectExecutionCalculation�� Execute() BlueprintNativeEvent�� �������̵� �Ѵ� */
	virtual void Execute_Implementation(const FGameplayEffectCustomExecutionParameters& ExecutionParams, FGameplayEffectCustomExecutionOutput& OutExecutionOutput) const override;
};
