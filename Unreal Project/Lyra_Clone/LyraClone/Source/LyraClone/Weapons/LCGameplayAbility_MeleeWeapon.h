//#pragma once
//
//#include "CoreMinimal.h"
//#include "GameplayTagContainer.h"
//#include "LyraClone/Equipment/LCGameplayAbility_FromEquipment.h"
//#include "LCGameplayAbility_MeleeWeapon.generated.h"
//
//class UAnimMontage;
//class UAbilityTask_PlayMontageAndWait;
//class UAbilityTask_WaitGameplayEvent;
//class UGameplayEffect;
//
//UCLASS()
//class LYRACLONE_API ULCGameplayAbility_MeleeWeapon : public ULCGameplayAbility_FromEquipment
//{
//	GENERATED_BODY()
//
//public:
//	ULCGameplayAbility_MeleeWeapon();
//
//	virtual void ActivateAbility(
//		const FGameplayAbilitySpecHandle Handle,
//		const FGameplayAbilityActorInfo* ActorInfo,
//		const FGameplayAbilityActivationInfo ActivationInfo,
//		const FGameplayEventData* TriggerEventData) override;
//
//	virtual void EndAbility(
//		const FGameplayAbilitySpecHandle Handle,
//		const FGameplayAbilityActorInfo* ActorInfo,
//		const FGameplayAbilityActivationInfo ActivationInfo,
//		bool bReplicateEndAbility,
//		bool bWasCancelled) override;
//
//protected:
//	// ===== Animation =====
//	UPROPERTY(EditDefaultsOnly, Category = "Melee|Animation")
//	TObjectPtr<UAnimMontage> AttackMontage = nullptr;
//
//	UPROPERTY(EditDefaultsOnly, Category = "Melee|Combo")
//	int32 MaxComboCount = 3;
//
//	UPROPERTY(EditDefaultsOnly, Category = "Melee|Combo")
//	TArray<FName> ComboSections;
//
//	UPROPERTY(EditDefaultsOnly, Category = "Melee|Combo")
//	FGameplayTag ComboWindowBeginEventTag;
//
//	UPROPERTY(EditDefaultsOnly, Category = "Melee|Combo")
//	FGameplayTag ComboWindowEndEventTag;
//
//	int32 CurrentComboIndex = 0;
//	bool bCanAcceptComboInput = false;
//	bool bComboInputBuffered = false;
//
//	// ===== Tasks =====
//	UPROPERTY()
//	TObjectPtr<UAbilityTask_PlayMontageAndWait> MontageTask;
//
//	UPROPERTY()
//	TObjectPtr<UAbilityTask_WaitGameplayEvent> WaitBeginTask;
//
//	UPROPERTY()
//	TObjectPtr<UAbilityTask_WaitGameplayEvent> WaitEndTask;
//
//	// ===== Internal =====
//	bool IsAttackMontagePlaying() const;
//	void TryAdvanceCombo();
//
//	UFUNCTION()
//	void OnComboWindowBegin(FGameplayEventData Payload);
//
//	UFUNCTION()
//	void OnComboWindowEnd(FGameplayEventData Payload);
//
//	UFUNCTION()
//	void OnAttackMontageCompleted();
//
//	UFUNCTION()
//	void OnAttackMontageCancelled();
//
//	UFUNCTION()
//	void OnAttackMontageInterrupted();
//};

#pragma once

#include "CoreMinimal.h"
#include "LyraClone/AbilitySystem/Abilities/LyraCloneGameplayAbility.h"
#include "LyraCloneComboActionData.h"
#include "LCGameplayAbility_MeleeWeapon.generated.h"

class UAnimMontage;

UCLASS()
class LYRACLONE_API ULCGameplayAbility_MeleeWeapon : public ULyraCloneGameplayAbility
{
	GENERATED_BODY()

public:
	ULCGameplayAbility_MeleeWeapon();

	// GA
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

	// Input
	virtual void InputPressed(
		const FGameplayAbilitySpecHandle Handle,
		const FGameplayAbilityActorInfo* ActorInfo,
		const FGameplayAbilityActivationInfo ActivationInfo) override;

protected:
	// === Data ===
	UPROPERTY(EditDefaultsOnly, Category = "Melee|Combo")
	TObjectPtr<UAnimMontage> AttackMontage;

	UPROPERTY(EditDefaultsOnly, Category = "Melee|Combo")
	TObjectPtr<ULyraCloneComboActionData> ComboData;

	UPROPERTY(EditDefaultsOnly, Category = "Melee|Combo")
	float AttackSpeedRate = 1.0f;

	// 마지막 타수 중 입력 무시
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Combo")
	bool bIgnoreFurtherInput = false;

	int32 GetEffectiveMaxCombo() const
	{
		return ComboData ? ComboData->MaxComboCount : 1;
	}


	// === Runtime ===
	UPROPERTY()
	int32 CurrentCombo = 0;

	UPROPERTY()
	bool bHasNextComboCommand = false;

	FTimerHandle ComboTimerHandle;

	// Combo flow (ArenaBattle style)
	void ComboActionBegin();
	void ComboActionEnd();

	void SetComboCheckTimer();
	void ComboCheck();

	// Helpers
	UAnimInstance* GetAnimInstance() const;
	bool IsMontagePlaying() const;
	void JumpToComboSection(int32 ComboNumber) const;

	// Montage end callbacks
	UFUNCTION()
	void OnAttackMontageCompleted();

	UFUNCTION()
	void OnAttackMontageCancelled();

	UFUNCTION()
	void OnAttackMontageInterrupted();
};



