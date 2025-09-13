// Fill out your copyright notice in the Description page of Project Settings.

#include "LyraCloneWeaponUserInterface.h"
#include "LyraClone/Equipment/LyraCloneEquipmentManagerComponent.h"
#include "LyraClone/Weapons/LyraCloneWeaponInstance.h"


ULyraCloneWeaponUserInterface::ULyraCloneWeaponUserInterface(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
}

void ULyraCloneWeaponUserInterface::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);

	// Pawn�� ��������
	if (APawn* Pawn = GetOwningPlayerPawn())
	{
		// EquipmentManaterComponent�� Ȱ���Ͽ�, WeaponInstance�� ��������
		if (ULyraCloneEquipmentManagerComponent* EquipmentManager = Pawn->FindComponentByClass<ULyraCloneEquipmentManagerComponent>())
		{
			if (ULyraCloneWeaponInstance* NewInstance = EquipmentManager->GetFirstInstanceOfType<ULyraCloneWeaponInstance>())
			{
				if (NewInstance != CurrentInstance && NewInstance->GetInstigator() != nullptr)
				{
					// ���� ������Ʈ���ְ�, OnWeaponChanged ȣ�� ����
					ULyraCloneWeaponInstance* OldWeapon = CurrentInstance;
					CurrentInstance = NewInstance;
					OnWeaponChanged(OldWeapon, CurrentInstance);
				}
			}
		}
	}
}
