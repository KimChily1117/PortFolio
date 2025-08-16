// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "LyraClone/Cosmetics/LyraCloneCosmeticAnimationTypes.h"
#include "LyraClone/Equipment/LyraCloneEquipmentInstance.h"
#include "LyraCloneWeaponInstance.generated.h"

/**
 * 
 */
UCLASS()
class LYRACLONE_API ULyraCloneWeaponInstance : public ULyraCloneEquipmentInstance
{
	GENERATED_BODY()
public:
	ULyraCloneWeaponInstance(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/** Weapon�� ������ AnimLayer�� �����Ͽ� ��ȯ */
	UFUNCTION(BlueprintCallable, BlueprintPure=false , Category = Animation)
	TSubclassOf<UAnimInstance> PickBestAnimLayer(bool bEquipped, const FGameplayTagContainer& CosmeticTags) const;

	/** Weapon�� Equip/Unequip�� ���� Animation Set ������ ��� �ִ� */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = Animation)
	FLyraCloneAnimLayerSelectionSet EquippedAnimSet;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = Animation)
	FLyraCloneAnimLayerSelectionSet UnequippedAnimSet;

};
