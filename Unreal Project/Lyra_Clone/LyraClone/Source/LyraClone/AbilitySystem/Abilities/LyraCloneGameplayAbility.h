// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Abilities/GameplayAbility.h"
#include "LyraCloneGameplayAbility.generated.h"

UENUM(BlueprintType)
enum class ELyraCloneAbilityActivationPolicy : uint8
{
	/** Input�� Trigger �Ǿ��� ��� (Presssed/Released) */
	OnInputTriggered,
	/** Input�� Held�Ǿ� ���� ��� */
	WhileInputActive,
	/** avatar�� �����Ǿ��� ���, �ٷ� �Ҵ� */
	OnSpawn,
};


class ULyraCloneAbilityCost;

/**
 * 
 */
UCLASS()
class LYRACLONE_API ULyraCloneGameplayAbility : public UGameplayAbility
{
	GENERATED_BODY()
	
public:
	ULyraCloneGameplayAbility(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/**
	 * UGameplayAbility interfaces
	 */
	virtual bool CheckCost(const FGameplayAbilitySpecHandle Handle, const FGameplayAbilityActorInfo* ActorInfo, OUT FGameplayTagContainer* OptionalRelevantTags = nullptr) const override;
	virtual void ApplyCost(const FGameplayAbilitySpecHandle Handle, const FGameplayAbilityActorInfo* ActorInfo, const FGameplayAbilityActivationInfo ActivationInfo) const override;

	/** ���� GA�� Ȱ��ȭ���� ��å */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Hak|AbilityActivation")
	ELyraCloneAbilityActivationPolicy ActivationPolicy;

	/** ability costs to apply HakGameplayAbility separately */
	UPROPERTY(EditDefaultsOnly, Instanced, Category = "Hak|Costs")
	TArray<TObjectPtr<ULyraCloneAbilityCost>> AdditionalCosts;
};