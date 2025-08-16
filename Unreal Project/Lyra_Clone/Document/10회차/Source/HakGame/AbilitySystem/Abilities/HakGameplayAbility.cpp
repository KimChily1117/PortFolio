// Fill out your copyright notice in the Description page of Project Settings.


#include "HakGameplayAbility.h"

UHakGameplayAbility::UHakGameplayAbility(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer)
{
	ActivationPolicy = EHakAbilityActivationPolicy::OnInputTriggered;
}