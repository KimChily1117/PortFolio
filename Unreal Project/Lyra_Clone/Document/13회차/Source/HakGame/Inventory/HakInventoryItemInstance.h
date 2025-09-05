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


	/** Inventory Item의 인스턴스에는 무엇으로 정의되었는지 메타 클래스인 HakInventoryItemDefinition을 들고 있다 */
	UPROPERTY()
	TSubclassOf<UHakInventoryItemDefinition> ItemDef;
};
