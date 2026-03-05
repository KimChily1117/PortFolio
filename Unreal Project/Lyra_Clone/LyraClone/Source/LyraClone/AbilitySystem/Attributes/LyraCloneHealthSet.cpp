// LyraCloneHealthSet.cpp

#include "LyraCloneHealthSet.h"

#include "AbilitySystemComponent.h"
#include "GameplayEffectExtension.h"
#include "GameFramework/Actor.h"
#include "Net/UnrealNetwork.h"

// 이 CPP 전용 로그 카테고리 (헤더 수정 없이 사용 가능)
DEFINE_LOG_CATEGORY_STATIC(LogLyraCloneHealthSet, Log, All);

ULyraCloneHealthSet::ULyraCloneHealthSet()
	: Super()
	, Health(50)
	, MaxHealth(100)
	, Healing(0.f)
	, Damage(0.f)
{
	bOutOfHealth = false;
}

void ULyraCloneHealthSet::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);

	DOREPLIFETIME_CONDITION_NOTIFY(ULyraCloneHealthSet, Health, COND_None, REPNOTIFY_Always);
	DOREPLIFETIME_CONDITION_NOTIFY(ULyraCloneHealthSet, MaxHealth, COND_None, REPNOTIFY_Always);
}

void ULyraCloneHealthSet::OnRep_Health(const FGameplayAttributeData& OldValue)
{
	GAMEPLAYATTRIBUTE_REPNOTIFY(ULyraCloneHealthSet, Health, OldValue);
	bOutOfHealth = (GetHealth() <= 0.0f);
}

void ULyraCloneHealthSet::OnRep_MaxHealth(const FGameplayAttributeData& OldValue)
{
	GAMEPLAYATTRIBUTE_REPNOTIFY(ULyraCloneHealthSet, MaxHealth, OldValue);
}

void ULyraCloneHealthSet::PostAttributeChange(const FGameplayAttribute& Attribute, float OldValue, float NewValue)
{
	Super::PostAttributeChange(Attribute, OldValue, NewValue);

	// MaxHealth 줄었을 때 Health가 위에 있으면 내려주기
	if (Attribute == GetMaxHealthAttribute())
	{
		if (GetHealth() > NewValue)
		{
			if (UAbilitySystemComponent* ASC = GetOwningAbilitySystemComponent())
			{
				ASC->ApplyModToAttribute(GetHealthAttribute(), EGameplayModOp::Override, NewValue);
			}
		}
	}

	// 죽었다가 다시 체력이 생기면 플래그 해제
	if (bOutOfHealth && GetHealth() > 0.0f)
	{
		bOutOfHealth = false;
	}
}

void ULyraCloneHealthSet::ClampAttribute(const FGameplayAttribute& Attribute, float& NewValue) const
{
	// HealthAttribute는 [0, GetMaxHealth]로 설정
	if (Attribute == GetHealthAttribute())
	{
		NewValue = FMath::Clamp(NewValue, 0.0f, GetMaxHealth());
	}
	// MaxHealthAttribute는 [1.0, inf]로 설정
	else if (Attribute == GetMaxHealthAttribute())
	{
		NewValue = FMath::Max(NewValue, 1.0f);
	}
}

void ULyraCloneHealthSet::PreAttributeBaseChange(const FGameplayAttribute& Attribute, float& NewValue) const
{
	Super::PreAttributeBaseChange(Attribute, NewValue);
	ClampAttribute(Attribute, NewValue);
}

void ULyraCloneHealthSet::PreAttributeChange(const FGameplayAttribute& Attribute, float& NewValue)
{
	Super::PreAttributeChange(Attribute, NewValue);
	ClampAttribute(Attribute, NewValue);
}

bool ULyraCloneHealthSet::PreGameplayEffectExecute(FGameplayEffectModCallbackData& Data)
{
	return Super::PreGameplayEffectExecute(Data);
}

void ULyraCloneHealthSet::PostGameplayEffectExecute(const FGameplayEffectModCallbackData& Data)
{
	Super::PostGameplayEffectExecute(Data);

#if !(UE_BUILD_SHIPPING || UE_BUILD_TEST)
	{
		AActor* OwnerActor = GetOwningActor();
		const ENetMode NetMode = OwnerActor ? OwnerActor->GetNetMode() : NM_Standalone;
		const ENetRole Role = OwnerActor ? OwnerActor->GetLocalRole() : ROLE_None;

		UE_LOG(LogLyraCloneHealthSet, Warning,
			TEXT("[PostGE ENTER] Target=%s  NetMode=%d  Role=%d  Attr=%s  Mag=%.2f  Health=%.2f  Damage=%.2f  Healing=%.2f  GE=%s"),
			*GetNameSafe(OwnerActor),
			(int32)NetMode,
			(int32)Role,
			*Data.EvaluatedData.Attribute.GetName(),
			Data.EvaluatedData.Magnitude,
			GetHealth(),
			GetDamage(),
			GetHealing(),
			*GetNameSafe(Data.EffectSpec.Def));
	}
#endif


	const float MinimumHealth = 0.0f;

	// 컨텍스트(가해자/원인자)
	const FGameplayEffectContextHandle& EffectContext = Data.EffectSpec.GetEffectContext();
	AActor* Instigator = EffectContext.GetOriginalInstigator();
	AActor* Causer = EffectContext.GetEffectCauser();

	// 오너(타겟) / 권한 체크
	AActor* OwnerActor = GetOwningActor();
	const bool bAuthority = (OwnerActor && OwnerActor->HasAuthority());

	float OutOfHealthMagnitude = 0.0f; // 죽음에 기여한 “값” 기록(보통 Damage)

	// ---------------------------------------------------------
	// Damage(meta) → Health 감소
	// ---------------------------------------------------------
	if (Data.EvaluatedData.Attribute == GetDamageAttribute())
	{
		const float DamageDone = FMath::Max(0.0f, GetDamage());
		const float OldHealth = GetHealth();

		if (DamageDone > 0.0f)
		{
			const float NewHealth = FMath::Clamp(OldHealth - DamageDone, MinimumHealth, GetMaxHealth());
			SetHealth(NewHealth);

			OutOfHealthMagnitude = DamageDone;

#if !(UE_BUILD_SHIPPING || UE_BUILD_TEST)
			if (bAuthority)
			{
				UE_LOG(LogLyraCloneHealthSet, Warning,
					TEXT("[DamageMeta] Target=%s  Damage=%.2f  Health %.2f -> %.2f  Instigator=%s  Causer=%s  GE=%s"),
					*GetNameSafe(OwnerActor),
					DamageDone,
					OldHealth, NewHealth,
					*GetNameSafe(Instigator),
					*GetNameSafe(Causer),
					*GetNameSafe(Data.EffectSpec.Def));
			}
#endif
		}

		// Meta 값 리셋
		SetDamage(0.0f);
	}
	// ---------------------------------------------------------
	// Healing(meta) → Health 증가
	// ---------------------------------------------------------
	else if (Data.EvaluatedData.Attribute == GetHealingAttribute())
	{
		const float HealDone = FMath::Max(0.0f, GetHealing());
		const float OldHealth = GetHealth();

		if (HealDone > 0.0f)
		{
			const float NewHealth = FMath::Clamp(OldHealth + HealDone, MinimumHealth, GetMaxHealth());
			SetHealth(NewHealth);

#if !(UE_BUILD_SHIPPING || UE_BUILD_TEST)
			if (bAuthority)
			{
				UE_LOG(LogLyraCloneHealthSet, Log,
					TEXT("[HealingMeta] Target=%s  Heal=%.2f  Health %.2f -> %.2f  Instigator=%s  Causer=%s  GE=%s"),
					*GetNameSafe(OwnerActor),
					HealDone,
					OldHealth, NewHealth,
					*GetNameSafe(Instigator),
					*GetNameSafe(Causer),
					*GetNameSafe(Data.EffectSpec.Def));
			}
#endif
		}

		// Meta 값 리셋
		SetHealing(0.0f);
	}
	// ---------------------------------------------------------
	// Health 직접 변경이 들어오면 클램프
	// ---------------------------------------------------------
	else if (Data.EvaluatedData.Attribute == GetHealthAttribute())
	{
		const float OldHealth = GetHealth();
		SetHealth(FMath::Clamp(GetHealth(), MinimumHealth, GetMaxHealth()));

#if !(UE_BUILD_SHIPPING || UE_BUILD_TEST)
		if (bAuthority)
		{
			UE_LOG(LogLyraCloneHealthSet, Log,
				TEXT("[HealthDirect] Target=%s  Health %.2f -> %.2f  GE=%s"),
				*GetNameSafe(OwnerActor),
				OldHealth,
				GetHealth(),
				*GetNameSafe(Data.EffectSpec.Def));
		}
#endif
	}

	// ---------------------------------------------------------
	// OutOfHealth는 “0 되는 순간 1번만”
	// ---------------------------------------------------------
	if ((GetHealth() <= 0.0f) && !bOutOfHealth)
	{
#if !(UE_BUILD_SHIPPING || UE_BUILD_TEST)
		if (bAuthority)
		{
			UE_LOG(LogLyraCloneHealthSet, Error,
				TEXT("[OutOfHealth] Target=%s  Instigator=%s  Causer=%s  FinalDamage=%.2f  GE=%s"),
				*GetNameSafe(OwnerActor),
				*GetNameSafe(Instigator),
				*GetNameSafe(Causer),
				OutOfHealthMagnitude,
				*GetNameSafe(Data.EffectSpec.Def));
		}
#endif

		OnOutOfHealth.Broadcast(Instigator, Causer, Data.EffectSpec, OutOfHealthMagnitude);
	}

	bOutOfHealth = (GetHealth() <= 0.0f);
}