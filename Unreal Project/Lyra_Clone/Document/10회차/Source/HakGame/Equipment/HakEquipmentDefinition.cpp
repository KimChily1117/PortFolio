// Fill out your copyright notice in the Description page of Project Settings.


#include "HakEquipmentDefinition.h"
#include "HakEquipmentInstance.h"

UHakEquipmentDefinition::UHakEquipmentDefinition(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
	// 기본값으로, HakEquipmentInstance로 설정
	InstanceType = UHakEquipmentInstance::StaticClass();
}
