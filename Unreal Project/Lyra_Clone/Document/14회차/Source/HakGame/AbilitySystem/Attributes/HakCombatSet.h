// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "AbilitySystemComponent.h"
#include "HakAttributeSet.h"
#include "HakCombatSet.generated.h"

/**
 * CombatSet�� �̸� �״��, ������ ���õ� Attribute�� ��� �ִ� Set�̴�:
 * - ����� BaseHeal�� ������, BaseDamage�� �߰��Ͽ�, ������ CombatSet�� �ʿ��� AttributeSet�� ���� �����ϴ�
 */
UCLASS()
class HAKGAME_API UHakCombatSet : public UHakAttributeSet
{
	GENERATED_BODY()
public:
	UHakCombatSet();

	ATTRIBUTE_ACCESSORS(UHakCombatSet, BaseHeal);

	/**
	 * FGameplayAttribute�� �����ϴ� ���� AttributeSet�� �ִ� �������̴� (float���� �ش� Struct�� ����ϴ� ���� ��õ)
	 * - Healing�� ������ �ǹ��Ѵ�
	 * - e.g. 5.0f���, Period�� 5�� Healing�ȴٴ� �ǹ�
	 */
	UPROPERTY(BlueprintReadOnly, Category = "Hak|Combat")
	FGameplayAttributeData BaseHeal;
};
