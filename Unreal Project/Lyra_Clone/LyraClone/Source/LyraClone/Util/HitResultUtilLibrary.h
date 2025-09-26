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
	// Index의 TargetData가 HitResult를 가지고 있는지
	UFUNCTION(BlueprintPure, Category = "GAS|TargetData")
	static bool HasHitResultAt(const FGameplayAbilityTargetDataHandle& TargetData, int32 Index);

	// Index의 TargetData에서 HitResult를 꺼낸다(성공 시 true)
	UFUNCTION(BlueprintPure, Category = "GAS|TargetData")
	static bool GetHitResultAt(const FGameplayAbilityTargetDataHandle& TargetData, int32 Index, FHitResult& OutHit);

	// 전체 TargetData에서 모든 HitResult를 수집
	UFUNCTION(BlueprintPure, Category = "GAS|TargetData")
	static TArray<FHitResult> GetAllHitResults(const FGameplayAbilityTargetDataHandle& TargetData);
};