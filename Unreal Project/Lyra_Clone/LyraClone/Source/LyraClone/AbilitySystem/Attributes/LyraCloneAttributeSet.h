// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "AttributeSet.h"
#include "LyraCloneAttributeSet.generated.h"

/**
 * �Ʒ� ��ũ�δ� AttributeSet�� Attribute�� �߰��� ��, ���� �� �����ؾ��� �޼��忡 ���� ���������� �����Ѵ�:
 *
 * ATTRIBUTE_ACCESSORS(UHakHealthSet, Health):
 * �̴� �Ʒ��� �޼��带 ���� �� �������ش�
 *
 *   static FGameplayAttribute GetHealthAttribute() {...}
 *   float GetHealth() const {...}
 *   void SetHealth(float NewVal) {...}
 *   void InitHealth(float NewVal) {...}
 */

#define		ATTRIBUTE_ACCESSORS(ClassName, PropertyName) \
			GAMEPLAYATTRIBUTE_PROPERTY_GETTER(ClassName, PropertyName) \
			GAMEPLAYATTRIBUTE_VALUE_GETTER(PropertyName) \
			GAMEPLAYATTRIBUTE_VALUE_SETTER(PropertyName) \
			GAMEPLAYATTRIBUTE_VALUE_INITTER(PropertyName)

 /**
  * LyraCloneAttributeSet
  * - Lyra�� ���������� Hak���� ���� Attribute Set Class�̴�
  */
UCLASS()
class LYRACLONE_API ULyraCloneAttributeSet : public UAttributeSet
{
	GENERATED_BODY()
public:
	ULyraCloneAttributeSet();
};
