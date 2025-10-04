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
	// NumSlots에 따라 미리 Slots을 할당한다
	if (Slots.Num() < NumSlots)
	{
		Slots.AddDefaulted(NumSlots - Slots.Num());
	}

	// 인벤토리 추가 이벤트 바인딩
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




	// 반드시 BeginPlay() 불러주는 것을 까먹지 말자!
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
	// 참고로 QuickBar는 Controller에 붙어있는 Component이지만, EquipmentManagerComponent는 Controller가 소유(Possess)하고 있는 Pawn의 Component이다
	if (ULyraCloneEquipmentManagerComponent* EquipmentManager = FindEquipmentManager())
	{
		// 현재 장착된 Item에 있다면,
		if (EquippedItem)
		{
			// EquipementManager를 통해, Pawn의 장비를 해제시킨다
			EquipmentManager->UnequipItem(EquippedItem);

			// 그리고 Controller에도 EquipItem의 상태를 없는 것으로 업데이트한다
			EquippedItem = nullptr;
		}
	}
}

void ULyraCloneQuickBarComponent::EquipItemInSlot()
{
	check(Slots.IsValidIndex(ActiveSlotIndex));
	check(EquippedItem == nullptr);

	// 현재 활성화된 ActiveSlotIndex를 활용하여 활성화된 InventoryItemInstance를 찾는다
	if (ULyraCloneInventoryItemInstance* SlotItem = Slots[ActiveSlotIndex])
	{
		// Slot Item을 통해 (InventoryItemInstance) InventoryFragment_EquippableItem의 Fragment를 찾는다:
		// - 찾는 것이 실패했다면, 장착할 수 없는 Inventory Item임을 의미한다
		if (const ULyraCloneInventoryFragment_EquippableItem* EquipInfo = SlotItem->FindFragmentByClass<ULyraCloneInventoryFragment_EquippableItem>())
		{
			// EquippableItem에서 EquipmentDefinition을 찾는다:
			// - EquipmentDefinition이 있어야, 장착할 수 있다
			TSubclassOf<ULyraCloneEquipmentDefinition> EquipDef = EquipInfo->EquipmentDefinition;
			if (EquipDef)
			{
				// 아래는 Unequip이랑 비슷하게 EquipmentManager를 통해 장착한다
				if (ULyraCloneEquipmentManagerComponent* EquipmentManager = FindEquipmentManager())
				{
					EquippedItem = EquipmentManager->EquipItem(EquipDef);

					if (EquippedItem)
					{
						// EquippedItem에는 앞서 보았던 Instiagor로 Slot을 대상으로 넣는다
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

	// 1) 빈 슬롯 우선 배치
	int32 SlotIndex = FindFirstEmptySlot();
	if (SlotIndex != INDEX_NONE)
	{
		AddItemToSlot(SlotIndex, NewItem);

		// 2) “빈손이면 자동 장착” 정책만 허용
		if (ActiveSlotIndex == INDEX_NONE)
		{
			SetActiveSlotIndex(SlotIndex);
		}
	}
	else
	{
		// 슬롯이 꽉 찼으면: 그냥 보관만(스왑은 입력으로)
		// 원하면 정책에 따라 덮어쓰기/큐잉 로직을 여기서 구현
	}
}

void ULyraCloneQuickBarComponent::AddItemToSlot(int32 SlotIndex, ULyraCloneInventoryItemInstance* Item)
{
	// 해당 로직을 보면, Slosts는 Add로 동적 추가가 아닌, Index에 바로 넣는다:
	// - 그럼 미리 Pre-size 했다는 것인데 이는 BeginPlay()에서 진행한다
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
	//	// UnequipItem/EquipItem을 통해, NewIndex를 통해 할당된 Item을 창착 및 업데이트를 진행한다
	//	UnequipItemInSlot();
	//	ActiveSlotIndex = NewIndex;
	//	EquipItemInSlot();
	//}

	if (!Slots.IsValidIndex(NewIndex) || ActiveSlotIndex == NewIndex)
		return;

	const int32 PrevIndex = ActiveSlotIndex;

	UnequipItemInSlot();           // 이전 무기 해제
	ActiveSlotIndex = NewIndex;
	EquipItemInSlot();             // 새 무기 장착

	if (!EquippedItem)             // 장착 실패한 경우 롤백
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