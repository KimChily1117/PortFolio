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
			// ActivatableAbilities���� �����Ѵ�:
			// - ClearAbility() �Լ��� ��� ���� ����
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

	// gameplay abilities�� ���:
	for (int32 AbilityIndex = 0; AbilityIndex < GrantedGameplayAbilities.Num(); ++AbilityIndex)
	{
		const FHakAbilitySet_GameplayAbility& AbilityToGrant = GrantedGameplayAbilities[AbilityIndex];
		if (!IsValid(AbilityToGrant.Ability))
		{
			continue;
		}

		// GiveAbility()���� ���� EGameplayAbilityInstancingPolicy::InstancedPerActor���, ���������� Instance�� ���������� �׷��� ������ CDO�� �Ҵ��Ѵ�
		// - �̸� ���� UObject ������ �ٿ� UObject�� �þ�� ���� �þ�� ������/�޸��� ���ϸ� ���� �� �ִ�
		UHakGameplayAbility* AbilityCDO = AbilityToGrant.Ability->GetDefaultObject<UHakGameplayAbility>();

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

