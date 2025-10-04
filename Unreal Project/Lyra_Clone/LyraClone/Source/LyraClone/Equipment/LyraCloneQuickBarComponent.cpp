// Fill out your copyright notice in the Description page of Project Settings.

#include "LyraCloneQuickBarComponent.h"
#include "LyraCloneEquipmentDefinition.h"
#include "LyraCloneEquipmentInstance.h"
#include "LyraCloneEquipmentManagerComponent.h"
#include "LyraClone/Inventory/LyraCloneInventoryFragment_EquippableItem.h"
#include "LyraClone/Inventory/LyraCloneInventoryItemInstance.h"
#include <LyraClone/Inventory/LyraCloneInventoryManagerComponent.h>

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

	// �κ��丮 �߰� �̺�Ʈ ���ε�
	if (AController* OwnerController = Cast<AController>(GetOwner()))
	{
		if (APawn* Pawn = OwnerController->GetPawn())
		{
			if (auto* Inv = Pawn->FindComponentByClass<ULyraCloneInventoryManagerComponent>())
			{
				Inv->OnItemAdded.AddUObject(this, &ThisClass::HandleItemAdded);
			}
		}
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

void ULyraCloneQuickBarComponent::HandleItemAdded(ULyraCloneInventoryItemInstance* NewItem)
{
	if (!NewItem) return;

	// 1) �� ���� �켱 ��ġ
	int32 SlotIndex = FindFirstEmptySlot();
	if (SlotIndex != INDEX_NONE)
	{
		AddItemToSlot(SlotIndex, NewItem);

		// 2) ������̸� �ڵ� ������ ��å�� ���
		if (ActiveSlotIndex == INDEX_NONE)
		{
			SetActiveSlotIndex(SlotIndex);
		}
	}
	else
	{
		// ������ �� á����: �׳� ������(������ �Է�����)
		// ���ϸ� ��å�� ���� �����/ť�� ������ ���⼭ ����
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
	//if (Slots.IsValidIndex(NewIndex) && (ActiveSlotIndex != NewIndex))
	//{
	//	// UnequipItem/EquipItem�� ����, NewIndex�� ���� �Ҵ�� Item�� â�� �� ������Ʈ�� �����Ѵ�
	//	UnequipItemInSlot();
	//	ActiveSlotIndex = NewIndex;
	//	EquipItemInSlot();
	//}

	if (!Slots.IsValidIndex(NewIndex) || ActiveSlotIndex == NewIndex)
		return;

	const int32 PrevIndex = ActiveSlotIndex;

	UnequipItemInSlot();           // ���� ���� ����
	ActiveSlotIndex = NewIndex;
	EquipItemInSlot();             // �� ���� ����

	if (!EquippedItem)             // ���� ������ ��� �ѹ�
	{
		ActiveSlotIndex = PrevIndex;
		if (PrevIndex != INDEX_NONE) EquipItemInSlot();
	}
}

int32 ULyraCloneQuickBarComponent::FindFirstEmptySlot() const
{
	for (int32 i = 0; i < Slots.Num(); ++i)
	{
		if (Slots[i] == nullptr) return i;
	}
	return INDEX_NONE;
}

ULyraCloneInventoryItemInstance* ULyraCloneQuickBarComponent::GetItemInSlot(int32 Index) const
{
	return Slots.IsValidIndex(Index) ? Slots[Index] : nullptr;
}

void ULyraCloneQuickBarComponent::ClearSlot(int32 Index)
{
	if (Slots.IsValidIndex(Index)) Slots[Index] = nullptr;
}

int32 ULyraCloneQuickBarComponent::GetActiveSlotIndex() const
{
	return ActiveSlotIndex;
}

void ULyraCloneQuickBarComponent::CycleActiveSlot(int32 Step)
{
	if (Slots.Num() == 0) return;

	const int32 Start = (ActiveSlotIndex == INDEX_NONE) ? -1 : ActiveSlotIndex;
	for (int32 hop = 1; hop <= Slots.Num(); ++hop)
	{
		const int32 Try = (Start + Step * hop + Slots.Num() * 8) % Slots.Num();
		if (GetItemInSlot(Try))
		{
			SetActiveSlotIndex(Try);
			break;
		}
	}
}