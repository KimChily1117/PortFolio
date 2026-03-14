// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameplayTagContainer.h"
#include "Components/ControllerComponent.h"
#include "LyraCloneQuickBarComponent.generated.h"

class ULyraCloneEquipmentManagerComponent;
class ULyraCloneEquipmentInstance;
class ULyraCloneInventoryItemInstance;
/**
 * HUD의 QuckBar를 생각하면 된다:
 * - 흔히 MMORPG에서는 ShortCut HUD를 연상하면 된다
 *
 * 해당 Component는 ControllerComponent로서, PlayerController에 의해 조작계 중 하나로 생각해도 된다
 * - HUD(Slate)와 Inventory/Equipment(Gameplay)의 다리(Bridge) 역활하는 Component로 생각하자
 * - 해당 Component는 Lyra의 HUD 및 Slate Widget을 다루면서 다시 보게될 예정이다
 */
UCLASS()
class LYRACLONE_API ULyraCloneQuickBarComponent : public UControllerComponent
{
	GENERATED_BODY()
public:
	ULyraCloneQuickBarComponent(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/**
	* ControllerComponent interface
	*/
	virtual void BeginPlay() override;

	/**
	* member methods
	*/

	UFUNCTION(BlueprintCallable)
	void SelectSlotByIndex(int32 SlotIndex);

	UFUNCTION(BlueprintCallable)
	void DropItemInActiveSlot();

	void HandleItemAdded(ULyraCloneInventoryItemInstance* NewItem);
	

	int32 FindSlotWithItem(ULyraCloneInventoryItemInstance* Item) const;
	int32 FindFirstEmptySlot() const;
	ULyraCloneInventoryItemInstance* GetItemInSlot(int32 Index) const;
	
	void ClearSlot(int32 Index);
	int32 GetActiveSlotIndex() const;

	//
	void CycleActiveSlot(int32 Step);

	UFUNCTION(BlueprintCallable, Category = "LyraClone")
	void SetActiveSlotIndex(int32 NewIndex);

	UFUNCTION(BlueprintCallable)
	void AddItemToSlot(int32 SlotIndex, ULyraCloneInventoryItemInstance* Item);



	ULyraCloneEquipmentManagerComponent* FindEquipmentManager() const;
	void UnequipItemInSlot();
	void EquipItemInSlot();
	void HandleNewPawn(APawn* NewPawn);

	int32 FindNextOccupiedSlot(int32 StartIndex, int32 Step = +1) const;

	int32 GetItemStatValue(const ULyraCloneInventoryItemInstance* Item, FGameplayTag StatTag) const;
	void SetItemStatValue(ULyraCloneInventoryItemInstance* Item, FGameplayTag StatTag, int32 NewValue);
	bool RefillAmmoForDrop(ULyraCloneInventoryItemInstance* Item);


	/** HUD QuickBar Slot 갯수 */
	UPROPERTY()
	int32 NumSlots = 3;

	/** HUD QuickBar Slot 리스트 */
	UPROPERTY()
	TArray<TObjectPtr<ULyraCloneInventoryItemInstance>> Slots;

	/** 현재 장착한 장비 정보 */
	UPROPERTY()
	TObjectPtr<ULyraCloneEquipmentInstance> EquippedItem;
	
	
	/** 현재 활성화된 Slot Index (아마 Lyra는 딱 한개만 Slot이 활성화되는가보다?) */
	UPROPERTY()
	int32 ActiveSlotIndex = -1;

};
