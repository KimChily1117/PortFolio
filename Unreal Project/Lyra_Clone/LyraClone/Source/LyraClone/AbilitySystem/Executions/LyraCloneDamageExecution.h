// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameplayEffectExecutionCalculation.h"
#include "LyraCloneDamageExecution.generated.h"

/**
 * 
 */
UCLASS()
class LYRACLONE_API ULyraCloneDamageExecution : public UGameplayEffectExecutionCalculation
{
	GENERATED_BODY()
public:
	ULyraCloneDamageExecution();

	virtual void Execute_Implementation
	(
		const FGameplayEffectCustomExecutionParameters& ExecutionParams,
		FGameplayEffectCustomExecutionOutput& OutExecutionOutput
	) const override;
};