// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "GameFramework/HUD.h"
#include "HakHUD.generated.h"

/**
 * 
 */
UCLASS()
class HAKGAME_API AHakHUD : public AHUD
{
	GENERATED_BODY()
public:
	AHakHUD(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/**
	 * GameFrameworkComponentManager�� AddReceiver�� ���� �޼����
	 */
	virtual void PreInitializeComponents() override;
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;
};
