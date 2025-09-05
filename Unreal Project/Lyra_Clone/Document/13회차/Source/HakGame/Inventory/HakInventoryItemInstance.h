// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "UObject/NoExportTypes.h"
#include "HakInventoryItemInstance.generated.h"

class UHakInventoryItemFragment;
class UHakInventoryItemDefinition;

/**
 * 
 */
UCLASS(BlueprintType)
class HAKGAME_API UHakInventoryItemInstance : public UObject
{
	GENERATED_BODY()
public:
	UHakInventoryItemInstance(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	UFUNCTION(BlueprintCallable, BlueprintPure = false, meta = (DeterminesOutputType = FragmentClass))
	const UHakInventoryItemFragment* FindFragmentByClass(TSubclassOf<UHakInventoryItemFragment> FragmentClass) const;

	template <typename ResultClass>
	const ResultClass* FindFragmentByClass() const
	{
		return (ResultClass*)FindFragmentByClass(ResultClass::StaticClass());
	}


	/** Inventory Item�� �ν��Ͻ����� �������� ���ǵǾ����� ��Ÿ Ŭ������ HakInventoryItemDefinition�� ��� �ִ� */
	UPROPERTY()
	TSubclassOf<UHakInventoryItemDefinition> ItemDef;
};
