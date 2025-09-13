// Fill out your copyright notice in the Description page of Project Settings.

#include "HakAbilitySet.h"
#include "HakAbilitySystemComponent.h"
#include "Abilities/HakGameplayAbility.h"

void FHakAbilitySet_GrantedHandles::AddAbilitySpecHandle(const FGameplayAbilitySpecHandle& Handle)
{
	if (Handle.IsValid())
	{
		AbilitySpecHandles.Add(Handle);
	}
}

void FHakAbilitySet_GrantedHandles::TakeFromAbilitySystem(UHakAbilitySystemComponent* HakASC)
{
	if (!HakASC->IsOwnerActorAuthoritative())
	{
		return;
	}

	for (const FGameplayAbilitySpecHandle& Handle : AbilitySpecHandles)
	{
		if (Handle.IsValid())
		{
			// ActivatableAbilities에서 제거한다:
			// - ClearAbility() 함수를 잠깐 보고 오자
			HakASC->ClearAbility(Handle);
		}
	}
}

UHakAbilitySet::UHakAbilitySet(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{}

void UHakAbilitySet::GiveToAbilitySystem(UHakAbilitySystemComponent* HakASC, FHakAbilitySet_GrantedHandles* OutGrantedHandles, UObject* SourceObject)
{
	check(HakASC);

	if (!HakASC->IsOwnerActorAuthoritative())
	{
		return;
	}

	// gameplay abilities를 허용:
	for (int32 AbilityIndex = 0; AbilityIndex < GrantedGameplayAbilities.Num(); ++AbilityIndex)
	{
		const FHakAbilitySet_GameplayAbility& AbilityToGrant = GrantedGameplayAbilities[AbilityIndex];
		if (!IsValid(AbilityToGrant.Ability))
		{
			continue;
		}

		// GiveAbility()에서 만약 EGameplayAbilityInstancingPolicy::InstancedPerActor라면, 내부적으로 Instance를 생성하지만 그렇지 않으면 CDO를 할당한다
		// - 이를 통해 UObject 갯수를 줄여 UObject가 늘어남에 따라 늘어나는 성능적/메모리적 부하를 줄일 수 있다
		UHakGameplayAbility* AbilityCDO = AbilityToGrant.Ability->GetDefaultObject<UHakGameplayAbility>();

		// AbilitySpec은 GiveAbility로 전달되어, ActivatbleAbilities에 추가된다
		FGameplayAbilitySpec AbilitySpec(AbilityCDO, AbilityToGrant.AbilityLevel);
		AbilitySpec.SourceObject = SourceObject;
		AbilitySpec.DynamicAbilityTags.AddTag(AbilityToGrant.InputTag);

		// GiveAbility()를 잠깐 살펴보도록 하자:
		const FGameplayAbilitySpecHandle AbilitySpecHandle = HakASC->GiveAbility(AbilitySpec);
		if (OutGrantedHandles)
		{
			OutGrantedHandles->AddAbilitySpecHandle(AbilitySpecHandle);
		}
	}
}

