// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameUIManagerSubsystem.h"
#include "LyraCloneUIManagerSubsystem.generated.h"

/**
 * 
 */
UCLASS()
class LYRACLONE_API ULyraCloneUIManagerSubsystem : public UGameUIManagerSubsystem
{
	GENERATED_BODY()
	
	/*
		UGameInstance	
	*/

	virtual void Initialize(FSubsystemCollectionBase& Collection) override;

	virtual void Deinitialize() override;

	virtual bool ShouldCreateSubsystem(UObject* Outer) const override;

	UPROPERTY(Transient)
	TObjectPtr<UGameUIPolicy> CurrentPolicy = nullptr;

	/*
	default UI Policy 생성할 class 
	우리는 해당 클래스는 B_BttGameUIPolicy
	*/

	UPROPERTY(Config, EditAnywhere)
	TSoftClassPtr<UGameUIPolicy> DefaultUIPolicyClass;

};
