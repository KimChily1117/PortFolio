// Fill out your copyright notice in the Description page of Project Settings.


#include "HakEquipmentManagerComponent.h"
#include "HakEquipmentDefinition.h"
#include "HakEquipmentInstance.h"
#include "AbilitySystemGlobals.h"
#include "HakGame/AbilitySystem/HakAbilitySystemComponent.h"

UHakEquipmentInstance* FHakEquipmentList::AddEntry(TSubclassOf<UHakEquipmentDefinition> EquipmentDefinition)
{
	UHakEquipmentInstance* Result = nullptr;
	check(EquipmentDefinition != nullptr);
	check(OwnerComponent);
	check(OwnerComponent->GetOwner()->HasAuthority());

	// EquipmentDefinition�� ��� �������� EditDefaultsOnly�� ���ǵǾ� �־� GetDefault�� ��� �͵� �츮���� �ʿ��� �͵��� ��� ����ִ�
	const UHakEquipmentDefinition* EquipmentCDO = GetDefault<UHakEquipmentDefinition>(EquipmentDefinition);

	TSubclassOf<UHakEquipmentInstance> InstanceType = EquipmentCDO->InstanceType;
	if (!InstanceType)
	{
		InstanceType = UHakEquipmentInstance::StaticClass();
	}

	// Entries�� �߰�������
	FHakAppliedEquipmentEntry& NewEntry = Entries.AddDefaulted_GetRef();
	NewEntry.EquipmentDefinition = EquipmentDefinition;
	NewEntry.Instance = NewObject<UHakEquipmentInstance>(OwnerComponent->GetOwner(), InstanceType);
	Result = NewEntry.Instance;

	UHakAbilitySystemComponent* ASC = GetAbilitySystemComponent();
	check(ASC);
	{
		for (const TObjectPtr<UHakAbilitySet> AbilitySet : EquipmentCDO->AbilitySetsToGrant)
		{
			AbilitySet->GiveToAbilitySystem(ASC, &NewEntry.GrantedHandles, Result);
		}
	}

	// ActorsToSpawn�� ����, Actor���� �ν��Ͻ�ȭ ������
	// - ���? EquipmentInstance��!
	Result->SpawnEquipmentActors(EquipmentCDO->ActorsToSpawn);

	return Result;
}

void FHakEquipmentList::RemoveEntry(UHakEquipmentInstance* Instance)
{
	// �ܼ��� �׳� Entries�� ��ȸ�ϸ�, Instance�� ã�Ƽ�
	for (auto EntryIt = Entries.CreateIterator(); EntryIt; ++EntryIt)
	{
		FHakAppliedEquipmentEntry& Entry = *EntryIt;
		if (Entry.Instance == Instance)
		{
			UHakAbilitySystemComponent* ASC = GetAbilitySystemComponent();
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

UHakAbilitySystemComponent* FHakEquipmentList::GetAbilitySystemComponent() const
{
	check(OwnerComponent);
	AActor* OwningActor = OwnerComponent->GetOwner();

	// GetAbilitySystemComponentFromActor�� ��� Ȯ���غ���:
	// - EquipmentManagerComponent�� AHakCharacter�� Owner�� ������ �ִ�
	// - �ش� �Լ��� IAbilitySystemInterface�� ���� AbilitySystemComponent�� ��ȯ�Ѵ�
	// - �츮�� HakCharacter�� IAbilitySystemInterface�� ��ӹ��� �ʿ䰡 �ִ�
	return Cast<UHakAbilitySystemComponent>(UAbilitySystemGlobals::GetAbilitySystemComponentFromActor(OwningActor));
}

UHakEquipmentManagerComponent::UHakEquipmentManagerComponent(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
                                                                                                            , EquipmentList(this)
{
}

UHakEquipmentInstance* UHakEquipmentManagerComponent::EquipItem(TSubclassOf<UHakEquipmentDefinition> EquipmentDefinition)
{
	UHakEquipmentInstance* Result = nullptr;
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

void UHakEquipmentManagerComponent::UnequipItem(UHakEquipmentInstance* ItemInstance)
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

TArray<UHakEquipmentInstance*> UHakEquipmentManagerComponent::GetEquipmentInstancesOfType(TSubclassOf<UHakEquipmentInstance> InstanceType) const
{
	TArray<UHakEquipmentInstance*> Results;

	// EquipmentList�� ��ȸ�ϸ�
	for (const FHakAppliedEquipmentEntry& Entry : EquipmentList.Entries)
	{
		if (UHakEquipmentInstance* Instance = Entry.Instance)
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
