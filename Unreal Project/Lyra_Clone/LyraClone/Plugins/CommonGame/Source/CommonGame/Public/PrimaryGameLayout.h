// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameplayTagContainer.h"
#include "CommonUserWidget.h"
#include "PrimaryGameLayout.generated.h"


class UCommonActivatableWidgetContainerBase;


/**
 * 
 */
UCLASS(Abstract)
class COMMONGAME_API UPrimaryGameLayout : public UCommonUserWidget
{
	GENERATED_BODY()

public:
	UPrimaryGameLayout(const FObjectInitializer& obj);
	
	/** Layer를 추가하며, GameplayTag를 할당한다 */
	UFUNCTION(BlueprintCallable, Category = "Layer")
	void RegisterLayer(FGameplayTag LayerTag, UCommonActivatableWidgetContainerBase* LayerWidget);

	/** GameplayTag --- CommonActivatableWidgetContainerBase */
	UPROPERTY(Transient, meta = (Categories = "UI.Layer"))
	TMap<FGameplayTag, TObjectPtr<UCommonActivatableWidgetContainerBase>> Layers;
};
