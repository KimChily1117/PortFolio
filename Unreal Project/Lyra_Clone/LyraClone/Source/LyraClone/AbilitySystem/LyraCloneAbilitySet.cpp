// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraCloneAbilitySet.h"
#include "LyraCloneAbilitySystemComponent.h"
#include "Abilities/LyraCloneGameplayAbility.h"


void FLyraCloneAbilitySet_GrantedHandles::AddAbilitySpecHandle(const FGameplayAbilitySpecHandle& Handle)
{
	if (Handle.IsValid())
	{
		AbilitySpecHandles.Add(Handle);
	}
}

void FLyraCloneAbilitySet_GrantedHandles::TakeFromAbilitySystem(ULyraCloneAbilitySystemComponent* HakASC)
{
	if (!HakASC->IsOwnerActorAuthoritative())
	{
		return;
	}

	for (const FGameplayAbilitySpecHandle& Handle : AbilitySpecHandles)
	{
		if (Handle.IsValid())
		{
			// ActivatableAbilities���� �����Ѵ�:
			// - ClearAbility() �Լ��� ��� ���� ����
			HakASC->ClearAbility(Handle);
		}
	}
}

ULyraCloneAbilitySet::ULyraCloneAbilitySet(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
}

void ULyraCloneAbilitySet::GiveToAbilitySystem(ULyraCloneAbilitySystemComponent* HakASC, FLyraCloneAbilitySet_GrantedHandles* OutGrantedHandles, UObject* SourceObject)
{
	check(HakASC);

	if (!HakASC->IsOwnerActorAuthoritative())
	{
		return;
	}

	// gameplay abilities�� ���:
	for (int32 AbilityIndex = 0; AbilityIndex < GrantedGameplayAbilities.Num(); ++AbilityIndex)
	{
		const FLyraCloneAbilitySet_GameplayAbility& AbilityToGrant = GrantedGameplayAbilities[AbilityIndex];
		if (!IsValid(AbilityToGrant.Ability))
		{
			continue;
		}

		// GiveAbility()���� ���� EGameplayAbilityInstancingPolicy::InstancedPerActor���, ���������� Instance�� ���������� �׷��� ������ CDO�� �Ҵ��Ѵ�
		// - �̸� ���� UObject ������ �ٿ� UObject�� �þ�� ���� �þ�� ������/�޸��� ���ϸ� ���� �� �ִ�
		ULyraCloneGameplayAbility* AbilityCDO = AbilityToGrant.Ability->GetDefaultObject<ULyraCloneGameplayAbility>(); 

		// AbilitySpec�� GiveAbility�� ���޵Ǿ�, ActivatbleAbilities�� �߰��ȴ�
		FGameplayAbilitySpec AbilitySpec(AbilityCDO, AbilityToGrant.AbilityLevel);
		AbilitySpec.SourceObject = SourceObject;
		AbilitySpec.DynamicAbilityTags.AddTag(AbilityToGrant.InputTag);

		// GiveAbility()�� ��� ���캸���� ����:
		const FGameplayAbilitySpecHandle AbilitySpecHandle = HakASC->GiveAbility(AbilitySpec);
		if (OutGrantedHandles)
		{
			OutGrantedHandles->AddAbilitySpecHandle(AbilitySpecHandle);
		}
	}
}
