// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Abilities/GameplayAbility.h"
#include "HakGameplayAbility.generated.h"

UENUM(BlueprintType)
enum class EHakAbilityActivationPolicy : uint8
{
	/** Input이 Trigger 되었을 경우 (Presssed/Released) */
	OnInputTriggered,
	/** Input이 Held되어 있을 경우 */
	WhileInputActive,
	/** avatar가 생성되었을 경우, 바로 할당 */
	OnSpawn,
};

/** forward declarations */
class UHakAbilityCost;

/**
 * 
 */
UCLASS()
class HAKGAME_API UHakGameplayAbility : public UGameplayAbility
{
	GENERATED_BODY()
public:
	UHakGameplayAbility(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/**
	 * UGameplayAbility interfaces
	 */
	virtual bool CheckCost(const FGameplayAbilitySpecHandle Handle, const FGameplayAbilityActorInfo* ActorInfo, OUT FGameplayTagContainer* OptionalRelevantTags = nullptr) const override;
	virtual void ApplyCost(const FGameplayAbilitySpecHandle Handle, const FGameplayAbilityActorInfo* ActorInfo, const FGameplayAbilityActivationInfo ActivationInfo) const override;

	/** 언제 GA가 활성화될지 정책 */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Hak|AbilityActivation")
	EHakAbilityActivationPolicy ActivationPolicy;

	/** ability costs to apply HakGameplayAbility separately */
	UPROPERTY(EditDefaultsOnly, Instanced, Category = "Hak|Costs")
	TArray<TObjectPtr<UHakAbilityCost>> AdditionalCosts;
};
