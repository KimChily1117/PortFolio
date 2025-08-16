// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameFeatureAction.h"
#include "GameFeaturesSubsystem.h"
#include "WorldActionBase.generated.h"


class FDelegateHandle;
class UGameInstance;
struct FGameFeatureActivatingContext;
struct FGameFeatureDeactivatingContext;
struct FWorldContext;

/**
 * 
 */
UCLASS(Abstract)
class LYRACLONE_API UWorldActionBase : public UGameFeatureAction
{
	GENERATED_BODY()
public:
	/**
	 * UGameFeatureAction's interface
	 */
	virtual void OnGameFeatureActivating(FGameFeatureActivatingContext& Context) override;

	/**
	 * interface
	 */
	virtual void AddToWorld(const FWorldContext& WorldContext, const FGameFeatureStateChangeContext& ChangeContext) PURE_VIRTUAL(UWorldActionBase::AddToWorld, );
};
