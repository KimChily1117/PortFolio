// Fill out your copyright notice in the Description page of Project Settings.


#include "HakAnimInstance.h"
#include "AbilitySystemGlobals.h"

void UHakAnimInstance::NativeInitializeAnimation()
{
	Super::NativeInitializeAnimation();

	if (AActor* OwningActor = GetOwningActor())
	{
		if (UAbilitySystemComponent* ASC = UAbilitySystemGlobals::GetAbilitySystemComponentFromActor(OwningActor))
		{
			InitializeWithAbilitySystem(ASC);
		}
	}
}

void UHakAnimInstance::InitializeWithAbilitySystem(UAbilitySystemComponent* ASC)
{
	// ASC ���� �����ϴ� GameplayTag�� AnimInstance�� ��� Property�� Delegate�� �����Ͽ�, �� ��ȭ�� ���� �ݿ��� �����Ѵ�
	GameplayTagPropertyMap.Initialize(this, ASC);
}
