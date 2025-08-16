// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraCloneEquipmentManagerComponent.h"
#include "LyraCloneEquipmentDefinition.h"
#include "LyraCloneEquipmentInstance.h"
#include "AbilitySystemGlobals.h"
#include "LyraClone/AbilitySystem/LyraCloneAbilitySystemComponent.h"

ULyraCloneEquipmentInstance* FLyraCloneEquipmentList::AddEntry(TSubclassOf<ULyraCloneEquipmentDefinition> EquipmentDefinition)
{
	ULyraCloneEquipmentInstance* Result = nullptr;
	check(EquipmentDefinition != nullptr);
	check(OwnerComponent);
	check(OwnerComponent->GetOwner()->HasAuthority());

	// EquipmentDefinition�� ��� �������� EditDefaultsOnly�� ���ǵǾ� �־� GetDefault�� ��� �͵� �츮���� �ʿ��� �͵��� ��� ����ִ�
	const ULyraCloneEquipmentDefinition* EquipmentCDO = GetDefault<ULyraCloneEquipmentDefinition>(EquipmentDefinition);

	TSubclassOf<ULyraCloneEquipmentInstance> InstanceType = EquipmentCDO->InstanceType;
	if (!InstanceType)
	{
		InstanceType = ULyraCloneEquipmentInstance::StaticClass();
	}

	// Entries�� �߰�������
	FLyraCloneAppliedEquipmentEntry& NewEntry = Entries.AddDefaulted_GetRef();
	NewEntry.EquipmentDefinition = EquipmentDefinition;
	NewEntry.Instance = NewObject<ULyraCloneEquipmentInstance>(OwnerComponent->GetOwner(), InstanceType);
	Result = NewEntry.Instance;

	ULyraCloneAbilitySystemComponent* ASC = GetAbilitySystemComponent();
	check(ASC);
	{
		for (const TObjectPtr<ULyraCloneAbilitySet> AbilitySet : EquipmentCDO->AbilitySetsToGrant)
		{
			AbilitySet->GiveToAbilitySystem(ASC, &NewEntry.GrantedHandles, Result);
		}
	}

	// ActorsToSpawn�� ����, Actor���� �ν��Ͻ�ȭ ������
	// - ���? EquipmentInstance��!
	Result->SpawnEquipmentActors(EquipmentCDO->ActorsToSpawn);

	return Result;
}

void FLyraCloneEquipmentList::RemoveEntry(ULyraCloneEquipmentInstance* Instance)
{
	// �ܼ��� �׳� Entries�� ��ȸ�ϸ�, Instance�� ã�Ƽ�
	for (auto EntryIt = Entries.CreateIterator(); EntryIt; ++EntryIt)
	{
		FLyraCloneAppliedEquipmentEntry& Entry = *EntryIt;
		if (Entry.Instance == Instance)
		{
			ULyraCloneAbilitySystemComponent* ASC = GetAbilitySystemComponent();
			check(ASC);
			{
				// TakeFromAbilitySystem�� GiveToAbilitySystem �ݴ� ��Ȱ��, ActivatableAbilities���� �����Ѵ�
				Entry.GrantedHandles.TakeFromAbilitySystem(ASC);
			}

			// Actor ���� �۾� �� iterator�� ���� �����ϰ� Array���� ���� ����
			Instance->DestroyEquipmentActors();
			EntryIt.RemoveCurrent();
		}
	}
}


ULyraCloneAbilitySystemComponent* FLyraCloneEquipmentList::GetAbilitySystemComponent() const
{
	check(OwnerComponent);
	AActor* OwningActor = OwnerComponent->GetOwner();

	// GetAbilitySystemComponentFromActor�� ��� Ȯ���غ���:
	// - EquipmentManagerComponent�� AHakCharacter�� Owner�� ������ �ִ�
	// - �ش� �Լ��� IAbilitySystemInterface�� ���� AbilitySystemComponent�� ��ȯ�Ѵ�
	// - �츮�� HakCharacter�� IAbilitySystemInterface�� ��ӹ��� �ʿ䰡 �ִ�
	return Cast<ULyraCloneAbilitySystemComponent>(UAbilitySystemGlobals::GetAbilitySystemComponentFromActor(OwningActor));
}

ULyraCloneEquipmentManagerComponent::ULyraCloneEquipmentManagerComponent(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
                                                                                                            , EquipmentList(this)
{
}

ULyraCloneEquipmentInstance* ULyraCloneEquipmentManagerComponent::EquipItem(TSubclassOf<ULyraCloneEquipmentDefinition> EquipmentDefinition)
{
	ULyraCloneEquipmentInstance* Result = nullptr;
	if (EquipmentDefinition)
	{
		Result = EquipmentList.AddEntry(EquipmentDefinition);
		if (Result)
		{
			Result->OnEquipped();
		}
	}
	return Result;
}

void ULyraCloneEquipmentManagerComponent::UnequipItem(ULyraCloneEquipmentInstance* ItemInstance)
{
	if (ItemInstance)
	{
		// �ش� �Լ��� BP�� Event��带 ȣ�����ش� (�ڼ��Ѱ� �ش� �Լ� �����ϸ鼭 ����)
		ItemInstance->OnUnequipped();

		// EquipementList�� �������ش�:
		// - �����ϴ� ������ ���� �߰��Ǿ��� Actor Instance�� ���Ÿ� �����Ѵ�
		EquipmentList.RemoveEntry(ItemInstance);
	}
}

ULyraCloneEquipmentInstance* ULyraCloneEquipmentManagerComponent::GetFirstInstanceOfType(TSubclassOf<ULyraCloneEquipmentInstance> InstanceType)
{
	for (FLyraCloneAppliedEquipmentEntry& Entry : EquipmentList.Entries)
	{
		if (ULyraCloneEquipmentInstance* Instance = Entry.Instance)
		{
			if (Instance->IsA(InstanceType))
			{
				return Instance;
			}
		}
	}
	return nullptr;
}

TArray<ULyraCloneEquipmentInstance*> ULyraCloneEquipmentManagerComponent::GetEquipmentInstancesOfType(TSubclassOf<ULyraCloneEquipmentInstance> InstanceType) const
{
	TArray<ULyraCloneEquipmentInstance*> Results;

	// EquipmentList�� ��ȸ�ϸ�
	for (const FLyraCloneAppliedEquipmentEntry& Entry : EquipmentList.Entries)
	{
		if (ULyraCloneEquipmentInstance* Instance = Entry.Instance)
		{
			// InstanceType�� �´� Class�̸� Results�� �߰��Ͽ� ��ȯ
			// - �츮�� ���, HakRangedWeaponInstance�� �ɰ���
			if (Instance->IsA(InstanceType))
			{
				Results.Add(Instance);
			}
		}
	}
	return Results;
}
