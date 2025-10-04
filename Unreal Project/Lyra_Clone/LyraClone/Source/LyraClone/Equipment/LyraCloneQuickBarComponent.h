// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Components/ControllerComponent.h"
#include "LyraCloneQuickBarComponent.generated.h"

class ULyraCloneEquipmentManagerComponent;
class ULyraCloneEquipmentInstance;
class ULyraCloneInventoryItemInstance;

/**
 * HUD�� QuckBar�� �����ϸ� �ȴ�:
 * - ���� MMORPG������ ShortCut HUD�� �����ϸ� �ȴ�
 *
 * �ش� Component�� ControllerComponent�μ�, PlayerController�� ���� ���۰� �� �ϳ��� �����ص� �ȴ�
 * - HUD(Slate)�� Inventory/Equipment(Gameplay)�� �ٸ�(Bridge) ��Ȱ�ϴ� Component�� ��������
 * - �ش� Component�� Lyra�� HUD �� Slate Widget�� �ٷ�鼭 �ٽ� ���Ե� �����̴�
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
	ULyraCloneEquipmentManagerComponent* FindEquipmentManager() const;
	void UnequipItemInSlot();
	void EquipItemInSlot();

	void HandleItemAdded(ULyraCloneInventoryItemInstance* NewItem);


	UFUNCTION(BlueprintCallable)
	void AddItemToSlot(int32 SlotIndex, ULyraCloneInventoryItemInstance* Item);

	UFUNCTION(BlueprintCallable, Category = "LyraClone")
	void SetActiveSlotIndex(int32 NewIndex);

	int32 FindFirstEmptySlot() const;

	ULyraCloneInventoryItemInstance* GetItemInSlot(int32 Index) const;

	void ClearSlot(int32 Index);

	int32 GetActiveSlotIndex() const;

	void CycleActiveSlot(int32 Step);

	/** HUD QuickBar Slot ���� */
	UPROPERTY()
	int32 NumSlots = 3;

	/** HUD QuickBar Slot ����Ʈ */
	UPROPERTY()
	TArray<TObjectPtr<ULyraCloneInventoryItemInstance>> Slots;

	/** ���� Ȱ��ȭ�� Slot Index (�Ƹ� Lyra�� �� �Ѱ��� Slot�� Ȱ��ȭ�Ǵ°�����?) */
	UPROPERTY()
	int32 ActiveSlotIndex = -1;

	/** ���� ������ ��� ���� */
	UPROPERTY()
	TObjectPtr<ULyraCloneEquipmentInstance> EquippedItem;
};
