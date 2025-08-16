// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "HakWeaponInstance.h"
#include "HakRangedWeaponInstance.generated.h"

/**
 * 
 */
UCLASS()
class HAKGAME_API UHakRangedWeaponInstance : public UHakWeaponInstance
{
	GENERATED_BODY()
public:
	/** ��ȿ ��Ÿ� */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "WeaponConfig", meta = (ForceUnits = cm))
	float MaxDamageRange = 25000.0f;

	/** ��ź ���� (Sphere Trace Sweep) */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "WeaponConfig", meta = (ForceUnits = cm))
	float BulletTraceWeaponRadius = 0.0f;
};
