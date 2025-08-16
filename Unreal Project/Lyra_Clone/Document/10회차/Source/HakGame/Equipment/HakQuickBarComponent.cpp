// Fill out your copyright notice in the Description page of Project Settings.


#include "HakQuickBarComponent.h"
#include "HakEquipmentDefinition.h"
#include "HakEquipmentInstance.h"
#include "HakEquipmentManagerComponent.h"
#include "HakGame/Inventory/HakInventoryFragment_EquippableItem.h"
#include "HakGame/Inventory/HakInventoryItemInstance.h"

UHakQuickBarComponent::UHakQuickBarComponent(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
}

void UHakQuickBarComponent::BeginPlay()
{
	// NumSlots�� ���� �̸� Slots�� �Ҵ��Ѵ�
	if (Slots.Num() < NumSlots)
	{
		Slots.AddDefaulted(NumSlots - Slots.Num());
	}

	// �ݵ�� BeginPlay() �ҷ��ִ� ���� ����� ����!
	Super::BeginPlay();
}

UHakEquipmentManagerComponent* UHakQuickBarComponent::FindEquipmentManager() const
{
	if (AController* OwnerController = Cast<AController>(GetOwner()))
	{
		if (APawn* Pawn = OwnerController->GetPawn())
		{
			return Pawn->FindComponentByClass<UHakEquipmentManagerComponent>();
		}
	}

	return nullptr;
}

void UHakQuickBarComponent::UnequipItemInSlot()
{
	// ����� QuickBar�� Controller�� �پ��ִ� Component������, EquipmentManagerComponent�� Controller�� ����(Possess)�ϰ� �ִ� Pawn�� Component�̴�
	if (UHakEquipmentManagerComponent* EquipmentManager = FindEquipmentManager())
	{
		// ���� ������ Item�� �ִٸ�,
		if (EquippedItem)
		{
			// EquipementManager�� ����, Pawn�� ��� ������Ų��
			EquipmentManager->UnequipItem(EquippedItem);

			// �׸��� Controller���� EquipItem�� ���¸� ���� ������ ������Ʈ�Ѵ�
			EquippedItem = nullptr;
		}
	}
}

void UHakQuickBarComponent::EquipItemInSlot()
{
	check(Slots.IsValidIndex(ActiveSlotIndex));
	check(EquippedItem == nullptr);

	// ���� Ȱ��ȭ�� ActiveSlotIndex�� Ȱ���Ͽ� Ȱ��ȭ�� InventoryItemInstance�� ã�´�
	if (UHakInventoryItemInstance* SlotItem = Slots[ActiveSlotIndex])
	{
		// Slot Item�� ���� (InventoryItemInstance) InventoryFragment_EquippableItem�� Fragment�� ã�´�:
		// - ã�� ���� �����ߴٸ�, ������ �� ���� Inventory Item���� �ǹ��Ѵ�
		if (const UHakInventoryFragment_EquippableItem* EquipInfo = SlotItem->FindFragmentByClass<UHakInventoryFragment_EquippableItem>())
		{
			// EquippableItem���� EquipmentDefinition�� ã�´�:
			// - EquipmentDefinition�� �־��, ������ �� �ִ�
			TSubclassOf<UHakEquipmentDefinition> EquipDef = EquipInfo->EquipmentDefinition;
			if (EquipDef)
			{
				// �Ʒ��� Unequip�̶� ����ϰ� EquipmentManager�� ���� �����Ѵ�
				if (UHakEquipmentManagerComponent* EquipmentManager = FindEquipmentManager())
				{
					EquippedItem = EquipmentManager->EquipItem(EquipDef);

					if (EquippedItem)
					{
						// EquippedItem���� �ռ� ���Ҵ� Instiagor�� Slot�� ������� �ִ´�
						EquippedItem->Instigator = SlotItem;
					}
				}
			}
		}
	}
}

void UHakQuickBarComponent::AddItemToSlot(int32 SlotIndex, UHakInventoryItemInstance* Item)
{
	// �ش� ������ ����, Slosts�� Add�� ���� �߰��� �ƴ�, Index�� �ٷ� �ִ´�:
	// - �׷� �̸� Pre-size �ߴٴ� ���ε� �̴� BeginPlay()���� �����Ѵ�
	if (Slots.IsValidIndex(SlotIndex) && (Item != nullptr))
	{
		if (Slots[SlotIndex] == nullptr)
		{
			Slots[SlotIndex] = Item;
		}
	}
}

void UHakQuickBarComponent::SetActiveSlotIndex(int32 NewIndex)
{
	if (Slots.IsValidIndex(NewIndex) && (ActiveSlotIndex != NewIndex))
	{
		// UnequipItem/EquipItem�� ����, NewIndex�� ���� �Ҵ�� Item�� â�� �� ������Ʈ�� �����Ѵ�
		UnequipItemInSlot();
		ActiveSlotIndex = NewIndex;
		EquipItemInSlot();
	}
}
