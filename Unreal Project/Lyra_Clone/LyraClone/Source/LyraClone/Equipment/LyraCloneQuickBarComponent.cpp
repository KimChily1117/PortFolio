// Fill out your copyright notice in the Description page of Project Settings.

#include "LyraCloneQuickBarComponent.h"
#include "LyraCloneEquipmentDefinition.h"
#include "LyraCloneEquipmentInstance.h"
#include "LyraCloneEquipmentManagerComponent.h"
#include "LyraClone/Inventory/LyraCloneInventoryFragment_EquippableItem.h"
#include "LyraClone/Inventory/LyraCloneInventoryItemInstance.h"

ULyraCloneQuickBarComponent::ULyraCloneQuickBarComponent(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
}

void ULyraCloneQuickBarComponent::BeginPlay()
{
	// NumSlots�� ���� �̸� Slots�� �Ҵ��Ѵ�
	if (Slots.Num() < NumSlots)
	{
		Slots.AddDefaulted(NumSlots - Slots.Num());
	}

	// �ݵ�� BeginPlay() �ҷ��ִ� ���� ����� ����!
	Super::BeginPlay();
}

ULyraCloneEquipmentManagerComponent* ULyraCloneQuickBarComponent::FindEquipmentManager() const
{
	if (AController* OwnerController = Cast<AController>(GetOwner()))
	{
		if (APawn* Pawn = OwnerController->GetPawn())
		{
			return Pawn->FindComponentByClass<ULyraCloneEquipmentManagerComponent>();
		}
	}

	return nullptr;
}

void ULyraCloneQuickBarComponent::UnequipItemInSlot()
{
	// ����� QuickBar�� Controller�� �پ��ִ� Component������, EquipmentManagerComponent�� Controller�� ����(Possess)�ϰ� �ִ� Pawn�� Component�̴�
	if (ULyraCloneEquipmentManagerComponent* EquipmentManager = FindEquipmentManager())
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

void ULyraCloneQuickBarComponent::EquipItemInSlot()
{
	check(Slots.IsValidIndex(ActiveSlotIndex));
	check(EquippedItem == nullptr);

	// ���� Ȱ��ȭ�� ActiveSlotIndex�� Ȱ���Ͽ� Ȱ��ȭ�� InventoryItemInstance�� ã�´�
	if (ULyraCloneInventoryItemInstance* SlotItem = Slots[ActiveSlotIndex])
	{
		// Slot Item�� ���� (InventoryItemInstance) InventoryFragment_EquippableItem�� Fragment�� ã�´�:
		// - ã�� ���� �����ߴٸ�, ������ �� ���� Inventory Item���� �ǹ��Ѵ�
		if (const ULyraCloneInventoryFragment_EquippableItem* EquipInfo = SlotItem->FindFragmentByClass<ULyraCloneInventoryFragment_EquippableItem>())
		{
			// EquippableItem���� EquipmentDefinition�� ã�´�:
			// - EquipmentDefinition�� �־��, ������ �� �ִ�
			TSubclassOf<ULyraCloneEquipmentDefinition> EquipDef = EquipInfo->EquipmentDefinition;
			if (EquipDef)
			{
				// �Ʒ��� Unequip�̶� ����ϰ� EquipmentManager�� ���� �����Ѵ�
				if (ULyraCloneEquipmentManagerComponent* EquipmentManager = FindEquipmentManager())
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

void ULyraCloneQuickBarComponent::AddItemToSlot(int32 SlotIndex, ULyraCloneInventoryItemInstance* Item)
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

void ULyraCloneQuickBarComponent::SetActiveSlotIndex(int32 NewIndex)
{
	if (Slots.IsValidIndex(NewIndex) && (ActiveSlotIndex != NewIndex))
	{
		// UnequipItem/EquipItem�� ����, NewIndex�� ���� �Ҵ�� Item�� â�� �� ������Ʈ�� �����Ѵ�
		UnequipItemInSlot();
		ActiveSlotIndex = NewIndex;
		EquipItemInSlot();
	}
}
