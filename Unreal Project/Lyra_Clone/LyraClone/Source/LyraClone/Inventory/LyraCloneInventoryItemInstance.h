// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "UObject/NoExportTypes.h"
#include "LyraClone/System/LyraCloneGameplayTagStack.h"
#include "LyraCloneInventoryItemInstance.generated.h"

class ULyraCloneInventoryItemFragment;
class ULyraCloneInventoryItemDefinition;

/**
 * 
 */
UCLASS(BlueprintType)
class LYRACLONE_API ULyraCloneInventoryItemInstance : public UObject
{
	GENERATED_BODY()
public:
	ULyraCloneInventoryItemInstance(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	UFUNCTION(BlueprintCallable, BlueprintPure = false, meta = (DeterminesOutputType = FragmentClass))
	const ULyraCloneInventoryItemFragment* FindFragmentByClass(TSubclassOf<ULyraCloneInventoryItemFragment> FragmentClass) const;

	template <typename ResultClass>
	const ResultClass* FindFragmentByClass() const
	{
		return (ResultClass*)FindFragmentByClass(ResultClass::StaticClass());
	}

	/** add/remove stack count to stat tag(=gameplay-tag stack) */
	void AddStatTagStack(FGameplayTag Tag, int32 StackCount);
	void RemoveStatTagStack(FGameplayTag Tag, int32 StackCount);

	/** whether stat tag has in StatTags */
	bool HasStatTag(FGameplayTag Tag) const;

	/** get the current count of gameplay-tag stack */
	UFUNCTION(BlueprintCallable, Category = Inventory)
	int32 GetStatTagStackCount(FGameplayTag Tag) const;

	/** gameplay-tag stacks for inventory item instance */
	UPROPERTY()
	FLyraCloneGameplayTagStackContainer StatTags;

	/** Inventory Item�� �ν��Ͻ����� �������� ���ǵǾ����� ��Ÿ Ŭ������ InventoryItemDefinition�� ��� �ִ� */
	UPROPERTY()
	TSubclassOf<ULyraCloneInventoryItemDefinition> ItemDef;
};
