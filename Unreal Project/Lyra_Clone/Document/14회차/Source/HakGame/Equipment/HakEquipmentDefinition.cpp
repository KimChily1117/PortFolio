// Fill out your copyright notice in the Description page of Project Settings.


#include "HakEquipmentDefinition.h"
#include "HakEquipmentInstance.h"

UHakEquipmentDefinition::UHakEquipmentDefinition(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
	// �⺻������, HakEquipmentInstance�� ����
	InstanceType = UHakEquipmentInstance::StaticClass();
}
