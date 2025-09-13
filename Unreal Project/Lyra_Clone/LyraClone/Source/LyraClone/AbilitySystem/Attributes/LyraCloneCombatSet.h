// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "AbilitySystemComponent.h"
#include "LyraCloneAttributeSet.h"
#include "LyraCloneCombatSet.generated.h"

/**
 * CombatSet�� �̸� �״��, ������ ���õ� Attribute�� ��� �ִ� Set�̴�:
 * - ����� BaseHeal�� ������, BaseDamage�� �߰��Ͽ�, ������ CombatSet�� �ʿ��� AttributeSet�� ���� �����ϴ�
 */
UCLASS()
class LYRACLONE_API ULyraCloneCombatSet : public ULyraCloneAttributeSet
{
	GENERATED_BODY()
public:
	ULyraCloneCombatSet();

	ATTRIBUTE_ACCESSORS(ULyraCloneCombatSet, BaseHeal);

	/**
	 * FGameplayAttribute�� �����ϴ� ���� AttributeSet�� �ִ� �������̴� (float���� �ش� Struct�� ����ϴ� ���� ��õ)
	 * - Healing�� ������ �ǹ��Ѵ�
	 * - e.g. 5.0f���, Period�� 5�� Healing�ȴٴ� �ǹ�
	 */
	UPROPERTY(BlueprintReadOnly, Category = "LyraClone|Combat")
	FGameplayAttributeData BaseHeal;
};
