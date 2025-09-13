// Fill out your copyright notice in the Description page of Project Settings.

#include "HakWeaponUserInterface.h"
#include "HakGame/Equipment/HakEquipmentManagerComponent.h"
#include "HakGame/Weapons/HakWeaponInstance.h"

UHakWeaponUserInterface::UHakWeaponUserInterface(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{}

void UHakWeaponUserInterface::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);

	// Pawn�� ��������
	if (APawn* Pawn = GetOwningPlayerPawn())
	{
		// EquipmentManaterComponent�� Ȱ���Ͽ�, WeaponInstance�� ��������
		if (UHakEquipmentManagerComponent* EquipmentManager = Pawn->FindComponentByClass<UHakEquipmentManagerComponent>())
		{
			if (UHakWeaponInstance* NewInstance = EquipmentManager->GetFirstInstanceOfType<UHakWeaponInstance>())
			{
				if (NewInstance != CurrentInstance && NewInstance->GetInstigator() != nullptr)
				{
					// ���� ������Ʈ���ְ�, OnWeaponChanged ȣ�� ����
					UHakWeaponInstance* OldWeapon = CurrentInstance;
					CurrentInstance = NewInstance;
					OnWeaponChanged(OldWeapon, CurrentInstance);
				}
			}
		}
	}
}