// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "LyraClone/Equipment/LCGameplayAbility_FromEquipment.h"
#include "LCGameplayAbility_MeleeWeapon.generated.h"


class UAnimMontage;
class UGameplayEffect;

/**
 * 
 */
UCLASS()
class LYRACLONE_API ULCGameplayAbility_MeleeWeapon : public ULCGameplayAbility_FromEquipment
{
	GENERATED_BODY()
	
protected:
	ULCGameplayAbility_MeleeWeapon();
	// GA entry point
	virtual void ActivateAbility(
		const FGameplayAbilitySpecHandle Handle,
		const FGameplayAbilityActorInfo* ActorInfo,
		const FGameplayAbilityActivationInfo ActivationInfo,
		const FGameplayEventData* TriggerEventData) override;

	virtual void EndAbility(
		const FGameplayAbilitySpecHandle Handle,
		const FGameplayAbilityActorInfo* ActorInfo,
		const FGameplayAbilityActivationInfo ActivationInfo,
		bool bReplicateEndAbility,
		bool bWasCancelled) override;

	// ----------------------------
	// Config (can move to equipment definition later)
	// ----------------------------
	UPROPERTY(EditDefaultsOnly, Category = "Melee|Anim")
	TObjectPtr<UAnimMontage> AttackMontage;

	/** If true, do hit trace immediately on start (debug/very simple melee). */
	UPROPERTY(EditDefaultsOnly, Category = "Melee|Trace")
	bool bTraceOnActivate = false;

	UPROPERTY(EditDefaultsOnly, Category = "Melee|Trace")
	float TraceLength = 150.f;

	UPROPERTY(EditDefaultsOnly, Category = "Melee|Trace")
	float TraceRadius = 30.f;

	/** Offset from actor location forward (helps prevent hitting yourself) */
	UPROPERTY(EditDefaultsOnly, Category = "Melee|Trace")
	float TraceStartForwardOffset = 50.f;

	/** Collision channel for melee (project-dependent) */
	UPROPERTY(EditDefaultsOnly, Category = "Melee|Trace")
	TEnumAsByte<ECollisionChannel> TraceChannel = ECC_Pawn;

	/** Prevent multi-hit to same actor during one attack */
	UPROPERTY(EditDefaultsOnly, Category = "Melee|Trace")
	bool bPreventDuplicateHits = true;

	/** Damage effect (optional). If null, you can just do cue-only for now. */
	UPROPERTY(EditDefaultsOnly, Category = "Melee|Damage")
	TSubclassOf<UGameplayEffect> DamageEffectClass;

	/** SetByCaller tag for damage magnitude (optional) */
	UPROPERTY(EditDefaultsOnly, Category = "Melee|Damage")
	FGameplayTag DamageSetByCallerTag;

	UPROPERTY(EditDefaultsOnly, Category = "Melee|Damage")
	float DamageAmount = 20.f;

	// Cues (optional)
	UPROPERTY(EditDefaultsOnly, Category = "Melee|Cues")
	FGameplayTag SwingCueTag;

	UPROPERTY(EditDefaultsOnly, Category = "Melee|Cues")
	FGameplayTag HitCueTag;

	// ----------------------------
	// Runtime
	// ----------------------------
	UPROPERTY()
	TSet<TObjectPtr<AActor>> HitActors;

	// Called from BP AnimNotify timing (recommended)
	UFUNCTION(BlueprintCallable, Category = "Melee")
	void AnimNotify_DoHitTrace();

	UFUNCTION()
	void OnAttackMontageCompleted();

	UFUNCTION()
	void OnAttackMontageCancelled();

	UFUNCTION()
	void OnAttackMontageInterrupted();


	// Core trace & handle
	void DoMeleeTraceOnce();
	void HandleHit(const FHitResult& Hit);

	// Helpers
	void ExecuteCueOnOwner(const FGameplayTag& CueTag, const FGameplayCueParameters& Params) const;
	bool ApplyDamageToActor(AActor* TargetActor, const FHitResult& Hit);
};
