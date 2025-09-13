// Fill out your copyright notice in the Description page of Project Settings.

#include "HakWeaponUserInterface.h"
#include "HakGame/Equipment/HakEquipmentManagerComponent.h"
#include "HakGame/Weapons/HakWeaponInstance.h"

UHakWeaponUserInterface::UHakWeaponUserInterface(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{}

void UHakWeaponUserInterface::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);

	// Pawn을 가져오고
	if (APawn* Pawn = GetOwningPlayerPawn())
	{
		// EquipmentManaterComponent를 활용하여, WeaponInstance를 가져오자
		if (UHakEquipmentManagerComponent* EquipmentManager = Pawn->FindComponentByClass<UHakEquipmentManagerComponent>())
		{
			if (UHakWeaponInstance* NewInstance = EquipmentManager->GetFirstInstanceOfType<UHakWeaponInstance>())
			{
				if (NewInstance != CurrentInstance && NewInstance->GetInstigator() != nullptr)
				{
					// 새로 업데이트해주고, OnWeaponChanged 호출 진행
					UHakWeaponInstance* OldWeapon = CurrentInstance;
					CurrentInstance = NewInstance;
					OnWeaponChanged(OldWeapon, CurrentInstance);
				}
			}
		}
	}
}