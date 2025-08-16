// Fill out your copyright notice in the Description page of Project Settings.
#include "LyraCloneAnimInstance.h"
#include "AbilitySystemGlobals.h"

ULyraCloneAnimInstance::ULyraCloneAnimInstance(const FObjectInitializer& Fobj) : Super(Fobj)
{
}

void ULyraCloneAnimInstance::NativeInitializeAnimation()
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

void ULyraCloneAnimInstance::InitializeWithAbilitySystem(UAbilitySystemComponent* ASC)
{
	// ASC ���� �����ϴ� GameplayTag�� AnimInstance�� ��� Property�� Delegate�� �����Ͽ�, �� ��ȭ�� ���� �ݿ��� �����Ѵ�
	GameplayTagPropertyMap.Initialize(this, ASC);
}
