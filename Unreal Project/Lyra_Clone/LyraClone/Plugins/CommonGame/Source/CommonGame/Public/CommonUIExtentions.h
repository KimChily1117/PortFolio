// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameplayTagContainer.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "CommonUIExtentions.generated.h"

//forward declarations

class UCommonActivatableWidget;
class ULocalPlayer;



/**
 * 
 */
UCLASS()
class COMMONGAME_API UCommonUIExtentions : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()
	
public:
	UCommonUIExtentions();

	static UCommonActivatableWidget* PushContentToLayer_ForPlayer(const ULocalPlayer* LocalPlayer, FGameplayTag LayerName, TSubclassOf<UCommonActivatableWidget> WidgetClass);
};
