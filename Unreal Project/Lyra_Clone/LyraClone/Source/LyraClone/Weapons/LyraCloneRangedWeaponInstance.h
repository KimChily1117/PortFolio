// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "LyraClone/Weapons/LyraCloneWeaponInstance.h"
#include "LyraCloneRangedWeaponInstance.generated.h"

/**
 * 
 */
UCLASS()
class LYRACLONE_API ULyraCloneRangedWeaponInstance : public ULyraCloneWeaponInstance
{
	GENERATED_BODY()
public:
	ULyraCloneRangedWeaponInstance(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());
public:
	/** À¯È¿ »ç°Å¸® */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "WeaponConfig", meta = (ForceUnits = cm))
	float MaxDamageRange = 25000.0f;

	/** ÃÑÅº ¹üÀ§ (Sphere Trace Sweep) */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "WeaponConfig", meta = (ForceUnits = cm))
	float BulletTraceWeaponRadius = 0.0f;


};
