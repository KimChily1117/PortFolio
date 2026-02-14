//#include "LCGameplayAbility_MeleeWeapon.h"
//
//#include "Abilities/Tasks/AbilityTask_PlayMontageAndWait.h"
//#include "Abilities/Tasks/AbilityTask_WaitGameplayEvent.h"
//#include "Components/SkeletalMeshComponent.h"
//#include "Animation/AnimInstance.h"
//#include "Animation/AnimMontage.h"
//
//ULCGameplayAbility_MeleeWeapon::ULCGameplayAbility_MeleeWeapon()
//{
//	InstancingPolicy = EGameplayAbilityInstancingPolicy::InstancedPerActor;
//
//	ComboSections = { TEXT("ComboAtk1"), TEXT("ComboAtk2"), TEXT("ComboAtk3") };
//
//	ComboWindowBeginEventTag = FGameplayTag::RequestGameplayTag(TEXT("Event.Melee.ComboWindow.Begin"));
//	ComboWindowEndEventTag = FGameplayTag::RequestGameplayTag(TEXT("Event.Melee.ComboWindow.End"));
//}
//
//bool ULCGameplayAbility_MeleeWeapon::IsAttackMontagePlaying() const
//{
//	if (!AttackMontage) return false;
//
//	const FGameplayAbilityActorInfo* Info = CurrentActorInfo;
//	if (!Info || !Info->AvatarActor.IsValid()) return false;
//
//	AActor* Avatar = Info->AvatarActor.Get();
//	if (!Avatar) return false;
//
//	USkeletalMeshComponent* Mesh = Avatar->FindComponentByClass<USkeletalMeshComponent>();
//	if (!Mesh) return false;
//
//	UAnimInstance* AnimInst = Mesh->GetAnimInstance();
//	if (!AnimInst) return false;
//
//	return AnimInst->Montage_IsPlaying(AttackMontage);
//}
//
//void ULCGameplayAbility_MeleeWeapon::ActivateAbility(
//	const FGameplayAbilitySpecHandle Handle,
//	const FGameplayAbilityActorInfo* ActorInfo,
//	const FGameplayAbilityActivationInfo ActivationInfo,
//	const FGameplayEventData* TriggerEventData)
//{
//	Super::ActivateAbility(Handle, ActorInfo, ActivationInfo, TriggerEventData);
//
//	if (!ActorInfo || !ActorInfo->AvatarActor.IsValid())
//	{
//		EndAbility(Handle, ActorInfo, ActivationInfo, true, true);
//		return;
//	}
//
//	//  재 Activate = 추가 입력으로 처리
//	if (IsAttackMontagePlaying())
//	{
//		UE_LOG(LogTemp, Warning, TEXT("[Melee] Re-Activate (CanAccept=%d)"), bCanAcceptComboInput ? 1 : 0);
//
//		if (bCanAcceptComboInput)
//		{
//			bComboInputBuffered = true;
//			UE_LOG(LogTemp, Warning, TEXT("[Melee] BUFFERED = 1 (Re-Activate)"));
//		}
//		return;
//	}
//
//
//	// ✅ 첫 발동 초기화
//	CurrentComboIndex = 0;
//	bCanAcceptComboInput = false;
//	bComboInputBuffered = false;
//
//	AActor* Avatar = GetAvatarActorFromActorInfo();
//
//	// ComboWindow Begin
//	if (ComboWindowBeginEventTag.IsValid())
//	{
//		WaitBeginTask = UAbilityTask_WaitGameplayEvent::WaitGameplayEvent(
//			this, ComboWindowBeginEventTag, Avatar, false, false);
//
//		if (WaitBeginTask)
//		{
//			WaitBeginTask->EventReceived.AddDynamic(this, &ULCGameplayAbility_MeleeWeapon::OnComboWindowBegin);
//			WaitBeginTask->ReadyForActivation();
//		}
//	}
//
//	// ComboWindow End
//	if (ComboWindowEndEventTag.IsValid())
//	{
//		WaitEndTask = UAbilityTask_WaitGameplayEvent::WaitGameplayEvent(
//			this, ComboWindowEndEventTag, Avatar, false, false);
//
//		if (WaitEndTask)
//		{
//			WaitEndTask->EventReceived.AddDynamic(this, &ULCGameplayAbility_MeleeWeapon::OnComboWindowEnd);
//			WaitEndTask->ReadyForActivation();
//		}
//	}
//
//	// Play montage at ComboAtk1
//	if (AttackMontage)
//	{
//		const FName StartSection = (ComboSections.Num() > 0) ? ComboSections[0] : NAME_None;
//
//		MontageTask = UAbilityTask_PlayMontageAndWait::CreatePlayMontageAndWaitProxy(
//			this, NAME_None, AttackMontage, 1.0f, StartSection, false);
//
//		if (MontageTask)
//		{
//			MontageTask->OnCompleted.AddDynamic(this, &ULCGameplayAbility_MeleeWeapon::OnAttackMontageCompleted);
//			MontageTask->OnCancelled.AddDynamic(this, &ULCGameplayAbility_MeleeWeapon::OnAttackMontageCancelled);
//			MontageTask->OnInterrupted.AddDynamic(this, &ULCGameplayAbility_MeleeWeapon::OnAttackMontageInterrupted);
//			MontageTask->ReadyForActivation();
//		}
//	}
//}
//
//void ULCGameplayAbility_MeleeWeapon::EndAbility(
//	const FGameplayAbilitySpecHandle Handle,
//	const FGameplayAbilityActorInfo* ActorInfo,
//	const FGameplayAbilityActivationInfo ActivationInfo,
//	bool bReplicateEndAbility,
//	bool bWasCancelled)
//{
//	bCanAcceptComboInput = false;
//	bComboInputBuffered = false;
//	CurrentComboIndex = 0;
//
//	Super::EndAbility(Handle, ActorInfo, ActivationInfo, bReplicateEndAbility, bWasCancelled);
//}
//
//void ULCGameplayAbility_MeleeWeapon::OnComboWindowBegin(FGameplayEventData Payload)
//{
//	bCanAcceptComboInput = true;
//	bComboInputBuffered = false;
//	UE_LOG(LogTemp, Warning, TEXT("[Melee] ComboWindow BEGIN"));
//}
//
//void ULCGameplayAbility_MeleeWeapon::OnComboWindowEnd(FGameplayEventData Payload)
//{
//	bCanAcceptComboInput = false;
//	UE_LOG(LogTemp, Warning, TEXT("[Melee] ComboWindow END Buffered=%d"), bComboInputBuffered ? 1 : 0);
//
//	if (bComboInputBuffered)
//	{
//		TryAdvanceCombo();
//		bComboInputBuffered = false;
//	}
//}
//
//void ULCGameplayAbility_MeleeWeapon::TryAdvanceCombo()
//{
//	if (!AttackMontage) return;
//	if (ComboSections.Num() <= 0) return;
//
//	const int32 EffectiveMax = FMath::Min(MaxComboCount, ComboSections.Num());
//	if (CurrentComboIndex + 1 >= EffectiveMax) return;
//
//	++CurrentComboIndex;
//
//	AActor* Avatar = GetAvatarActorFromActorInfo();
//	if (!Avatar) return;
//
//	USkeletalMeshComponent* Mesh = Avatar->FindComponentByClass<USkeletalMeshComponent>();
//	if (!Mesh) return;
//
//	UAnimInstance* AnimInst = Mesh->GetAnimInstance();
//	if (!AnimInst) return;
//
//	const FName NextSection = ComboSections[CurrentComboIndex];
//	AnimInst->Montage_JumpToSection(NextSection, AttackMontage);
//
//	UE_LOG(LogTemp, Warning, TEXT("[Melee] JUMP -> %s"), *NextSection.ToString());
//}
//
//void ULCGameplayAbility_MeleeWeapon::OnAttackMontageCompleted()
//{
//	EndAbility(CurrentSpecHandle, CurrentActorInfo, CurrentActivationInfo, true, false);
//}
//
//void ULCGameplayAbility_MeleeWeapon::OnAttackMontageCancelled()
//{
//	EndAbility(CurrentSpecHandle, CurrentActorInfo, CurrentActivationInfo, true, true);
//}
//
//void ULCGameplayAbility_MeleeWeapon::OnAttackMontageInterrupted()
//{
//	EndAbility(CurrentSpecHandle, CurrentActorInfo, CurrentActivationInfo, true, true);
//}

#include "LCGameplayAbility_MeleeWeapon.h"

#include "GameFramework/Actor.h"
#include "Components/SkeletalMeshComponent.h"
#include "Animation/AnimInstance.h"
#include "Abilities/Tasks/AbilityTask_PlayMontageAndWait.h"
#include "Engine/World.h"
#include "TimerManager.h"
#include "LyraCloneComboActionData.h" // 네가 쓰는 데이터 에셋
#include "GameplayAbilities\Public\AbilitySystemComponent.h"


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


	if (UAbilitySystemComponent* ASC = GetAbilitySystemComponentFromActorInfo())
	{
		FGameplayTag TAG_BlockMove = FGameplayTag::RequestGameplayTag(TEXT("State.Movement.Blocked"));
		ASC->AddLooseGameplayTag(TAG_BlockMove);

		// 선택: “이미 움직이고 있던 관성”까지 끊고 싶으면 여기서 StopMovementImmediately도 가능
		// (CharacterMovement 접근 가능한 경우에만)
	}



	// 마지막 타 락 중이면 어떤 재발동도 무시
	if (bIgnoreFurtherInput)
	{
		return;
	}


	if (!ActorInfo || !ActorInfo->AvatarActor.IsValid() || !AttackMontage || !ComboData)
	{
		EndAbility(Handle, ActorInfo, ActivationInfo, true, true);
		return;
	}

	// 이미 공격 중이면 "추가 입력"은 InputPressed로 처리할 거라 여기선 리턴
	if (IsMontagePlaying())
	{
		return;
	}

	ComboActionBegin();

	// 몽타주 재생 (Task로 끝났을 때 GA 종료)
	UAbilityTask_PlayMontageAndWait* Task =
		UAbilityTask_PlayMontageAndWait::CreatePlayMontageAndWaitProxy(
			this,
			NAME_None,
			AttackMontage,
			AttackSpeedRate,
			/*StartSection*/ NAME_None,
			/*bStopWhenAbilityEnds*/ false
		);

	if (Task)
	{
		Task->OnCompleted.AddDynamic(this, &ULCGameplayAbility_MeleeWeapon::OnAttackMontageCompleted);
		Task->OnCancelled.AddDynamic(this, &ULCGameplayAbility_MeleeWeapon::OnAttackMontageCancelled);
		Task->OnInterrupted.AddDynamic(this, &ULCGameplayAbility_MeleeWeapon::OnAttackMontageInterrupted);
		Task->ReadyForActivation();
	}
}

void ULCGameplayAbility_MeleeWeapon::EndAbility(
	const FGameplayAbilitySpecHandle Handle,
	const FGameplayAbilityActorInfo* ActorInfo,
	const FGameplayAbilityActivationInfo ActivationInfo,
	bool bReplicateEndAbility,
	bool bWasCancelled)
{
	if (UWorld* World = GetWorld())
	{
		World->GetTimerManager().ClearTimer(ComboTimerHandle);
	}

	bIgnoreFurtherInput = false;
	CurrentCombo = 0;
	bHasNextComboCommand = false;

	if (UAbilitySystemComponent* ASC = GetAbilitySystemComponentFromActorInfo())
	{
		FGameplayTag TAG_BlockMove = FGameplayTag::RequestGameplayTag(TEXT("State.Movement.Blocked"));

		ASC->RemoveLooseGameplayTag(TAG_BlockMove);
	}


	Super::EndAbility(Handle, ActorInfo, ActivationInfo, bReplicateEndAbility, bWasCancelled);
}

void ULCGameplayAbility_MeleeWeapon::InputPressed(
	const FGameplayAbilitySpecHandle Handle,
	const FGameplayAbilityActorInfo* ActorInfo,
	const FGameplayAbilityActivationInfo ActivationInfo)
{
	Super::InputPressed(Handle, ActorInfo, ActivationInfo);

	// 마지막 타 락 중이면 어떤 재발동도 무시
	if (bIgnoreFurtherInput)
	{
		return;
	}


	// 공격 중일 때만 “콤보 예약”
	if (CurrentCombo > 0)
	{
		// ArenaBattle 로직: 타이머가 유효하면 다음 콤보 예약
		// 타이머가 이미 끝났으면(=윈도우 종료) 예약 무효 처리
		if (ComboTimerHandle.IsValid())
		{
			bHasNextComboCommand = true;
			UE_LOG(LogTemp, Warning, TEXT("[Melee] Combo Command Buffered = 1 (Combo=%d)"), CurrentCombo);
		}
		else
		{
			bHasNextComboCommand = false;
			UE_LOG(LogTemp, Warning, TEXT("[Melee] Combo Command Rejected (Window Closed)"));
		}
	}
}

void ULCGameplayAbility_MeleeWeapon::ComboActionBegin()
{
	// ArenaBattle: CurrentCombo=1
	CurrentCombo = 1;
	bHasNextComboCommand = false;

	// 1타 섹션으로 점프(네 데이터 prefix 기반)
	JumpToComboSection(CurrentCombo);

	// 콤보 유효 타이머 설정
	SetComboCheckTimer();
}

void ULCGameplayAbility_MeleeWeapon::ComboActionEnd()
{
	CurrentCombo = 0;
	bHasNextComboCommand = false;
}

void ULCGameplayAbility_MeleeWeapon::SetComboCheckTimer()
{
	if (!ComboData) return;

	const int32 ComboIndex = CurrentCombo - 1;
	if (!ComboData->EffectiveFrameCount.IsValidIndex(ComboIndex)) return;

	const float EffectiveTime =
		(ComboData->EffectiveFrameCount[ComboIndex] / ComboData->FrameRate) / AttackSpeedRate;

	if (EffectiveTime <= 0.f) return;

	if (UWorld* World = GetWorld())
	{
		World->GetTimerManager().SetTimer(
			ComboTimerHandle,
			this,
			&ULCGameplayAbility_MeleeWeapon::ComboCheck,
			EffectiveTime,
			false
		);
	}
}

void ULCGameplayAbility_MeleeWeapon::ComboCheck()
{
	// ArenaBattle: 타이머 만료
	ComboTimerHandle.Invalidate();

	if (!bHasNextComboCommand)
	{
		UE_LOG(LogTemp, Warning, TEXT("[Melee] ComboCheck: No buffered input -> stop at %d"), CurrentCombo);
		return;
	}
	const int32 MaxCombo = ComboData ? ComboData->MaxComboCount : 1;
	// 다음 콤보로
	CurrentCombo = FMath::Clamp(CurrentCombo + 1, 1, ComboData->MaxComboCount);

	// 마지막 타로 넘어가는 순간부터 입력 잠금
	if (CurrentCombo >= MaxCombo)
	{
		bIgnoreFurtherInput = true;
		bHasNextComboCommand = false; // 더 이상 예약 의미 없음
	}


	JumpToComboSection(CurrentCombo);

	UE_LOG(LogTemp, Warning, TEXT("[Melee] ComboCheck: Jump to %d"), CurrentCombo);

	// 마지막 타면 타이머 더 안 건다 (윈도우 자체가 의미 없음)
	if (CurrentCombo < MaxCombo)
	{
		SetComboCheckTimer();
	}
	bHasNextComboCommand = false;
}

UAnimInstance* ULCGameplayAbility_MeleeWeapon::GetAnimInstance() const
{
	AActor* Avatar = GetAvatarActorFromActorInfo();
	if (!Avatar) return nullptr;

	USkeletalMeshComponent* Mesh = Avatar->FindComponentByClass<USkeletalMeshComponent>();
	if (!Mesh) return nullptr;

	return Mesh->GetAnimInstance();
}

bool ULCGameplayAbility_MeleeWeapon::IsMontagePlaying() const
{
	if (!AttackMontage) return false;
	if (UAnimInstance* AnimInst = GetAnimInstance())
	{
		return AnimInst->Montage_IsPlaying(AttackMontage);
	}
	return false;
}

void ULCGameplayAbility_MeleeWeapon::JumpToComboSection(int32 ComboNumber) const
{
	if (!AttackMontage || !ComboData) return;

	UAnimInstance* AnimInst = GetAnimInstance();
	if (!AnimInst) return;

	const FName SectionName = *FString::Printf(TEXT("%s%d"),
		*ComboData->MontageSectionNamePrefix,
		ComboNumber);

	AnimInst->Montage_JumpToSection(SectionName, AttackMontage);
}

void ULCGameplayAbility_MeleeWeapon::OnAttackMontageCompleted()
{
	ComboActionEnd();
	EndAbility(CurrentSpecHandle, CurrentActorInfo, CurrentActivationInfo, true, false);
}

void ULCGameplayAbility_MeleeWeapon::OnAttackMontageCancelled()
{
	ComboActionEnd();
	EndAbility(CurrentSpecHandle, CurrentActorInfo, CurrentActivationInfo, true, true);
}

void ULCGameplayAbility_MeleeWeapon::OnAttackMontageInterrupted()
{
	ComboActionEnd();
	EndAbility(CurrentSpecHandle, CurrentActorInfo, CurrentActivationInfo, true, true);
}
