// Fill out your copyright notice in the Description page of Project Settings.


#include "HitResultUtilLibrary.h"

bool UHitResultUtilLibrary::HasHitResultAt(const FGameplayAbilityTargetDataHandle& TargetData, int32 Index)
{
	if (!TargetData.Data.IsValidIndex(Index)) return false;
	const FGameplayAbilityTargetData* Data = TargetData.Data[Index].Get();
	return (Data && Data->HasHitResult());
}


bool UHitResultUtilLibrary::GetHitResultAt(const FGameplayAbilityTargetDataHandle& TargetData, int32 Index, FHitResult& OutHit)
{
	OutHit = FHitResult();
	if (!TargetData.Data.IsValidIndex(Index)) return false;

	const FGameplayAbilityTargetData* Data = TargetData.Data[Index].Get();
	if (!Data || !Data->HasHitResult()) return false;

	const FHitResult* HR = Data->GetHitResult();
	if (!HR) return false;

	OutHit = *HR;
	return true;
}

TArray<FHitResult> UHitResultUtilLibrary::GetAllHitResults(const FGameplayAbilityTargetDataHandle& TargetData)
{
	TArray<FHitResult> Results;
	for (int32 i = 0; i < TargetData.Data.Num(); ++i)
	{
		const FGameplayAbilityTargetData* Data = TargetData.Data[i].Get();
		if (Data && Data->HasHitResult())
		{
			if (const FHitResult* HR = Data->GetHitResult())
			{
				Results.Add(*HR);
			}
		}
	}
	return Results;
}

