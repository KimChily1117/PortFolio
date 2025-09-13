// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CommonUserWidget.h"
#include "UObject/ObjectPtr.h"
#include "UObject/UObjectGlobals.h"
#include "LyraCloneWeaponUserInterface.generated.h"


/** forward declaration */
class ULyraCloneWeaponInstance;


/**
 * 
 */
UCLASS()
class LYRACLONE_API ULyraCloneWeaponUserInterface : public UCommonUserWidget
{
	GENERATED_BODY()
public:
	ULyraCloneWeaponUserInterface(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/** Weapon ���Ͽ� ���� BP Event */
	UFUNCTION(BlueprintImplementableEvent)
	void OnWeaponChanged(ULyraCloneWeaponInstance* OldWeapon, ULyraCloneWeaponInstance* NewWeapon);

	/**
	 * UUserWidget's interface
	 */
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;

	/** ���� ������ WeaponInstance�� �����Ѵ� (NativeTick�� Ȱ���Ͽ� �ֱ��� ������Ʈ�Ѵ�) */
	UPROPERTY(Transient)
	TObjectPtr<ULyraCloneWeaponInstance> CurrentInstance;
};
