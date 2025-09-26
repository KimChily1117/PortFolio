// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "Abilities/GameplayAbilityTargetTypes.h"   // FGameplayAbilityTargetDataHandle
#include "HitResultUtilLibrary.generated.h"

/**
 * 
 */
UCLASS()
class LYRACLONE_API UHitResultUtilLibrary : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()
public:
	// Index�� TargetData�� HitResult�� ������ �ִ���
	UFUNCTION(BlueprintPure, Category = "GAS|TargetData")
	static bool HasHitResultAt(const FGameplayAbilityTargetDataHandle& TargetData, int32 Index);

	// Index�� TargetData���� HitResult�� ������(���� �� true)
	UFUNCTION(BlueprintPure, Category = "GAS|TargetData")
	static bool GetHitResultAt(const FGameplayAbilityTargetDataHandle& TargetData, int32 Index, FHitResult& OutHit);

	// ��ü TargetData���� ��� HitResult�� ����
	UFUNCTION(BlueprintPure, Category = "GAS|TargetData")
	static TArray<FHitResult> GetAllHitResults(const FGameplayAbilityTargetDataHandle& TargetData);
};