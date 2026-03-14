// Fill out your copyright notice in the Description page of Project Settings.

#include "LyraCloneQuickBarComponent.h"
#include "LyraCloneEquipmentDefinition.h"
#include "LyraCloneEquipmentInstance.h"
#include "LyraCloneEquipmentManagerComponent.h"
#include "LyraClone/Inventory/LyraCloneInventoryFragment_EquippableItem.h"
#include "LyraClone/Inventory/LyraCloneInventoryItemInstance.h"
#include <LyraClone/Inventory/LyraCloneInventoryManagerComponent.h>


namespace QuickBarStatTags
{
	static FGameplayTag GetMagazineSizeTag()
	{
		return FGameplayTag::RequestGameplayTag(
			FName(TEXT("LyraClone.ShooterGame.Weapon.MagazineSize")),
			false
		);
	}

	static FGameplayTag GetMagazineAmmoTag()
	{
		return FGameplayTag::RequestGameplayTag(
			FName(TEXT("LyraClone.ShooterGame.Weapon.MagazineAmmo")),
			false
		);
	}

	// 나중에 예비탄 태그 추가할 때 쓰면 됨
	static FGameplayTag GetReserveAmmoTag()
	{
		return FGameplayTag::RequestGameplayTag(
			FName(TEXT("LyraClone.ShooterGame.Weapon.ReserveAmmo")),
			false
		);
	}

	static FGameplayTag GetMaxReserveAmmoTag()
	{
		return FGameplayTag::RequestGameplayTag(
			FName(TEXT("LyraClone.ShooterGame.Weapon.MaxReserveAmmo")),
			false
		);
	}
}



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

	if (AController* OwnerController = Cast<AController>(GetOwner()))
	{
		// 앞으로 Pawn이 바뀔 때마다(처음 Possess 포함) 콜백이 온다
		OwnerController->GetOnNewPawnNotifier().AddUObject(this, &ThisClass::HandleNewPawn);

		// 이미 Pawn이 있으면 지금 바로 1회 바인딩
		HandleNewPawn(OwnerController->GetPawn());
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
		if (const ULyraCloneInventoryFragment_EquippableItem* EquipInfo = 
			SlotItem->FindFragmentByClass<ULyraCloneInventoryFragment_EquippableItem>())
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


void ULyraCloneQuickBarComponent::SelectSlotByIndex(int32 SlotIndex)
{
	if (!Slots.IsValidIndex(SlotIndex))
	{
		return;
	}

	if (Slots[SlotIndex] == nullptr)
	{
		return; // 빈 슬롯은 무시
	}

	SetActiveSlotIndex(SlotIndex);

}



void ULyraCloneQuickBarComponent::HandleItemAdded(ULyraCloneInventoryItemInstance* NewItem)
{
	if (!NewItem)
		return;
	if (FindSlotWithItem(NewItem) != INDEX_NONE)
		return; // 또는 기존 슬롯 비우고 이동 처리


	int32 SlotIndex = FindFirstEmptySlot();
	bool bReplacingActiveSlot = false;


	// 슬롯이 없으면 활성 슬롯으로 교체(= 스왑)
	if (SlotIndex == INDEX_NONE)
	{
		SlotIndex = (ActiveSlotIndex != INDEX_NONE) ? ActiveSlotIndex : 0;
		bReplacingActiveSlot = (SlotIndex == ActiveSlotIndex);

		UnequipItemInSlot();
		Slots[SlotIndex] = nullptr;
	}

	AddItemToSlot(SlotIndex, NewItem);

	// 같은 슬롯 재선택 시 SetActiveSlotIndex가 early return 하는 문제 방지
	if (bReplacingActiveSlot)
	{
		ActiveSlotIndex = INDEX_NONE;
	}

	SetActiveSlotIndex(SlotIndex);
}

int32 ULyraCloneQuickBarComponent::FindSlotWithItem(ULyraCloneInventoryItemInstance* Item) const
{
	for (int32 i = 0; i < Slots.Num(); ++i)
	{
		if (Slots[i] == Item)
			return i;
	}
	return INDEX_NONE;
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

	if (!Slots.IsValidIndex(NewIndex) || ActiveSlotIndex == NewIndex)
		return;

	const int32 PrevIndex = ActiveSlotIndex;

	UnequipItemInSlot();           // 이전 무기 해제
	ActiveSlotIndex = NewIndex;
	EquipItemInSlot();             // 새 무기 장착

	if (!EquippedItem)             // 장착 실패한 경우 롤백
	{
		ActiveSlotIndex = PrevIndex;
		if (PrevIndex != INDEX_NONE) 
			EquipItemInSlot();
	}
}

int32 ULyraCloneQuickBarComponent::FindFirstEmptySlot() const
{
	for (int32 i = 0; i < Slots.Num(); ++i)
	{
		if (Slots[i] == nullptr) 
			return i;
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
	if (Slots.Num() == 0) 
		return;

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

int32 ULyraCloneQuickBarComponent::FindNextOccupiedSlot(int32 StartIndex, int32 Step) const
{
	if (Slots.Num() == 0)
	{
		return INDEX_NONE;
	}

	const int32 Start = (StartIndex == INDEX_NONE) ? -1 : StartIndex;

	for (int32 Hop = 1; Hop <= Slots.Num(); ++Hop)
	{
		const int32 Try = (Start + Step * Hop + Slots.Num() * 8) % Slots.Num();
		if (Slots.IsValidIndex(Try) && Slots[Try] != nullptr)
		{
			return Try;
		}
	}

	return INDEX_NONE;
}


int32 ULyraCloneQuickBarComponent::GetItemStatValue(const ULyraCloneInventoryItemInstance* Item, FGameplayTag StatTag) const
{
	if (!Item || !StatTag.IsValid())
	{
		return 0;
	}

	// Lyra 계열 기본 스타일 가정
	return Item->GetStatTagStackCount(StatTag);
}

void ULyraCloneQuickBarComponent::SetItemStatValue(ULyraCloneInventoryItemInstance* Item, FGameplayTag StatTag, int32 NewValue)
{
	if (!Item || !StatTag.IsValid())
	{
		return;
	}

	const int32 OldValue = Item->GetStatTagStackCount(StatTag);

	if (OldValue > 0)
	{
		Item->RemoveStatTagStack(StatTag, OldValue);
	}

	if (NewValue > 0)
	{
		Item->AddStatTagStack(StatTag, NewValue);
	}
}

bool ULyraCloneQuickBarComponent::RefillAmmoForDrop(ULyraCloneInventoryItemInstance* Item)
{
	if (!Item)
	{
		return false;
	}

	const FGameplayTag MagazineSizeTag = QuickBarStatTags::GetMagazineSizeTag();
	const FGameplayTag MagazineAmmoTag = QuickBarStatTags::GetMagazineAmmoTag();

	if (!MagazineSizeTag.IsValid() || !MagazineAmmoTag.IsValid())
	{
		return false;
	}

	const int32 MagazineSize = GetItemStatValue(Item, MagazineSizeTag);

	// 무기가 아니거나 탄창 stat이 없으면 그냥 무시
	if (MagazineSize <= 0)
	{
		return false;
	}

	SetItemStatValue(Item, MagazineAmmoTag, MagazineSize);

	// 예비탄 태그를 나중에 실제로 쓰면 여기서 같이 복구
	const FGameplayTag ReserveAmmoTag = QuickBarStatTags::GetReserveAmmoTag();
	const FGameplayTag MaxReserveAmmoTag = QuickBarStatTags::GetMaxReserveAmmoTag();

	if (ReserveAmmoTag.IsValid() && MaxReserveAmmoTag.IsValid())
	{
		const int32 MaxReserveAmmo = GetItemStatValue(Item, MaxReserveAmmoTag);
		if (MaxReserveAmmo > 0)
		{
			SetItemStatValue(Item, ReserveAmmoTag, MaxReserveAmmo);
		}
	}

	return true;
}

void ULyraCloneQuickBarComponent::DropItemInActiveSlot()
{
	if (!Slots.IsValidIndex(ActiveSlotIndex))
	{
		return;
	}

	const int32 DroppedSlotIndex = ActiveSlotIndex;
	ULyraCloneInventoryItemInstance* Item = Slots[DroppedSlotIndex];
	if (!Item)
	{
		return;
	}

	UnequipItemInSlot();
	RefillAmmoForDrop(Item);

	if (AController* OwnerController = Cast<AController>(GetOwner()))
	{
		if (APawn* Pawn = OwnerController->GetPawn())
		{
			if (ULyraCloneInventoryManagerComponent* Inventory =
				Pawn->FindComponentByClass<ULyraCloneInventoryManagerComponent>())
			{
				Inventory->RemoveItemInstance(Item);
			}
		}
	}

	Slots[DroppedSlotIndex] = nullptr;
	ActiveSlotIndex = INDEX_NONE;
	EquippedItem = nullptr;

	const int32 NextSlot = FindNextOccupiedSlot(DroppedSlotIndex, +1);
	if (NextSlot != INDEX_NONE)
	{
		SetActiveSlotIndex(NextSlot);
	}
}

																					
void ULyraCloneQuickBarComponent::HandleNewPawn(APawn* NewPawn)
{
	if (!NewPawn)
	{
		return;
	}

	if (ULyraCloneInventoryManagerComponent* Inv = NewPawn->FindComponentByClass<ULyraCloneInventoryManagerComponent>())
	{
		Inv->OnItemAdded.RemoveAll(this);
		Inv->OnItemAdded.AddUObject(this, &ThisClass::HandleItemAdded);

		// OnItemRemoved delegate가 있으면 나중에 같이 바인딩하는 게 더 안전함
	}
}
