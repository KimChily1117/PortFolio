// Fill out your copyright notice in the Description page of Project Settings.


#include "LCGameplayAbility_MeleeWeapon.h"
#include "AbilitySystemGlobals.h"
#include "AbilitySystemComponent.h"
#include "GameFramework/Actor.h"
#include "Engine/World.h"
#include "GameplayEffect.h"
#include "Animation/AnimMontage.h"
#include "Abilities/Tasks/AbilityTask_PlayMontageAndWait.h"

ULCGameplayAbility_MeleeWeapon::ULCGameplayAbility_MeleeWeapon()
{
	InstancingPolicy = EGameplayAbilityInstancingPolicy::InstancedPerActor;
}

void ULCGameplayAbility_MeleeWeapon::ActivateAbility(
	const FGameplayAbilitySpecHandle Handle,
	const FGameplayAbilityActorInfo* ActorInfo,
	const FGameplayAbilityActivationInfo ActivationInfo,
	const FGameplayEventData* TriggerEventData)
{
	Super::ActivateAbility(Handle, ActorInfo, ActivationInfo, TriggerEventData);

	if (!ActorInfo || !ActorInfo->AvatarActor.IsValid())
	{
		EndAbility(Handle, ActorInfo, ActivationInfo, true, true);
		return;
	}

	HitActors.Reset();

	// Swing cue (optional)
	if (SwingCueTag.IsValid())
	{
		FGameplayCueParameters Params;
		ExecuteCueOnOwner(SwingCueTag, Params);
	}

	// Play montage (optional but typical melee)
	if (AttackMontage)
	{
		// NOTE: BP에서도 몽타주 재생해도 되지만,
		// C++에서 Task로 재생하면 끝났을 때 EndAbility 처리가 깔끔해짐.
		UAbilityTask_PlayMontageAndWait* Task =
			UAbilityTask_PlayMontageAndWait::CreatePlayMontageAndWaitProxy(
				this,
				NAME_None,
				AttackMontage,
				1.0f,
				NAME_None,
				false
			);

		Task->OnCompleted.AddDynamic(this, &ULCGameplayAbility_MeleeWeapon::OnAttackMontageCompleted);
		Task->OnCancelled.AddDynamic(this, &ULCGameplayAbility_MeleeWeapon::OnAttackMontageCancelled);
		Task->OnInterrupted.AddDynamic(this, &ULCGameplayAbility_MeleeWeapon::OnAttackMontageInterrupted);


		Task->ReadyForActivation();
	}

	if (bTraceOnActivate)
	{
		DoMeleeTraceOnce();
	}
}

void ULCGameplayAbility_MeleeWeapon::EndAbility(
	const FGameplayAbilitySpecHandle Handle,
	const FGameplayAbilityActorInfo* ActorInfo,
	const FGameplayAbilityActivationInfo ActivationInfo,
	bool bReplicateEndAbility,
	bool bWasCancelled)
{
	HitActors.Reset();

	Super::EndAbility(Handle, ActorInfo, ActivationInfo, bReplicateEndAbility, bWasCancelled);
}

void ULCGameplayAbility_MeleeWeapon::AnimNotify_DoHitTrace()
{
	// 권장: 실제 데미지/히트 확정은 서버에서만
	const FGameplayAbilityActorInfo* Info = CurrentActorInfo;
	if (!Info || !Info->AvatarActor.IsValid()) return;

	AActor* Avatar = Info->AvatarActor.Get();
	if (!Avatar) return;

	if (!Avatar->HasAuthority())
	{
		// 클라는 (원하면) 스윙/트레일 정도만
		// 데미지/히트는 서버가 확정
		return;
	}

	DoMeleeTraceOnce();
}

void ULCGameplayAbility_MeleeWeapon::OnAttackMontageCompleted()
{
	// GA 종료(블루프린트 K2 방식)
	K2_EndAbility();
}

void ULCGameplayAbility_MeleeWeapon::OnAttackMontageCancelled()
{
	K2_EndAbility();
}

void ULCGameplayAbility_MeleeWeapon::OnAttackMontageInterrupted()
{
	K2_EndAbility();
}

void ULCGameplayAbility_MeleeWeapon::DoMeleeTraceOnce()
{
	const FGameplayAbilityActorInfo* Info = CurrentActorInfo;
	if (!Info) return;

	AActor* Avatar = Info->AvatarActor.Get();
	if (!Avatar) return;

	UWorld* World = Avatar->GetWorld();
	if (!World) return;

	const FVector Forward = Avatar->GetActorForwardVector();
	const FVector Start = Avatar->GetActorLocation() + Forward * TraceStartForwardOffset;
	const FVector End = Start + Forward * TraceLength;

	FCollisionQueryParams Params(SCENE_QUERY_STAT(LC_MeleeTrace), false, Avatar);

	TArray<FHitResult> Hits;
	const bool bHit = World->SweepMultiByChannel(
		Hits,
		Start,
		End,
		FQuat::Identity,
		TraceChannel,
		FCollisionShape::MakeSphere(TraceRadius),
		Params
	);

	if (!bHit) return;

	for (const FHitResult& HR : Hits)
	{
		AActor* HitActor = HR.GetActor();
		if (!HitActor || HitActor == Avatar) continue;

		if (bPreventDuplicateHits)
		{
			if (HitActors.Contains(HitActor)) continue;
			HitActors.Add(HitActor);
		}

		HandleHit(HR);
	}
}

void ULCGameplayAbility_MeleeWeapon::HandleHit(const FHitResult& Hit)
{
	AActor* Target = Hit.GetActor();
	if (!Target) return;

	// Hit cue (optional) with params
	if (HitCueTag.IsValid())
	{
		FGameplayCueParameters Params;
		Params.Location = Hit.ImpactPoint;
		Params.Normal = Hit.ImpactNormal;
		Params.PhysicalMaterial = Hit.PhysMaterial.Get();
		ExecuteCueOnOwner(HitCueTag, Params);
	}

	// Damage (optional)
	ApplyDamageToActor(Target, Hit);
}

bool ULCGameplayAbility_MeleeWeapon::ApplyDamageToActor(AActor* TargetActor, const FHitResult& Hit)
{
	if (!DamageEffectClass) return false;

	UAbilitySystemComponent* SourceASC = CurrentActorInfo ? CurrentActorInfo->AbilitySystemComponent.Get() : nullptr;
	if (!SourceASC) return false;

	UAbilitySystemComponent* TargetASC = UAbilitySystemGlobals::GetAbilitySystemComponentFromActor(TargetActor);
	if (!TargetASC) return false;

	FGameplayEffectSpecHandle SpecHandle = MakeOutgoingGameplayEffectSpec(DamageEffectClass, GetAbilityLevel());
	if (!SpecHandle.IsValid()) return false;

	if (DamageSetByCallerTag.IsValid())
	{
		SpecHandle.Data->SetSetByCallerMagnitude(DamageSetByCallerTag, DamageAmount);
	}

	SourceASC->ApplyGameplayEffectSpecToTarget(*SpecHandle.Data.Get(), TargetASC);
	return true;
}

void ULCGameplayAbility_MeleeWeapon::ExecuteCueOnOwner(const FGameplayTag& CueTag, const FGameplayCueParameters& Params) const
{
	if (!CueTag.IsValid()) return;

	const FGameplayAbilityActorInfo* Info = CurrentActorInfo;
	if (!Info) return;

	if (UAbilitySystemComponent* ASC = Info->AbilitySystemComponent.Get())
	{
		ASC->ExecuteGameplayCue(CueTag, Params);
	}
}