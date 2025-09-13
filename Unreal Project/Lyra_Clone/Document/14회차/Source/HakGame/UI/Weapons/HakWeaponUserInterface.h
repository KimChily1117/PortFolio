// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CommonUserWidget.h"
#include "UObject/ObjectPtr.h"
#include "UObject/UObjectGlobals.h"
#include "HakWeaponUserInterface.generated.h"

/** forward declaration */
class UHakWeaponInstance;

/**
 * 
 */
UCLASS()
class HAKGAME_API UHakWeaponUserInterface : public UCommonUserWidget
{
	GENERATED_BODY()
public:
	UHakWeaponUserInterface(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/** Weapon ���Ͽ� ���� BP Event */
	UFUNCTION(BlueprintImplementableEvent)
	void OnWeaponChanged(UHakWeaponInstance* OldWeapon, UHakWeaponInstance* NewWeapon);

	/**
	 * UUserWidget's interface
	 */
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;

	/** ���� ������ WeaponInstance�� �����Ѵ� (NativeTick�� Ȱ���Ͽ� �ֱ��� ������Ʈ�Ѵ�) */
	UPROPERTY(Transient)
	TObjectPtr<UHakWeaponInstance> CurrentInstance;
};
