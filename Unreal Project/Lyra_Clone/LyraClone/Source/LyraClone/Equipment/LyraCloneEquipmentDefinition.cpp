// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraCloneEquipmentDefinition.h"
#include "LyraCloneEquipmentInstance.h"

ULyraCloneEquipmentDefinition::ULyraCloneEquipmentDefinition(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
	// �⺻������, HakEquipmentInstance�� ����
	InstanceType = ULyraCloneEquipmentInstance::StaticClass();
}
