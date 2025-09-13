// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "HakAttributeSet.h"
#include "AbilitySystemComponent.h"
#include "HakHealthSet.generated.h"

/**
 * 
 */
UCLASS()
class HAKGAME_API UHakHealthSet : public UHakAttributeSet
{
	GENERATED_BODY()
public:
	UHakHealthSet();

	/**
	* �ռ� HakAttributeSet���� �����ߴ�, ATTRIBUTE_ACCESSORS�� ����, �Ʒ� ������ �⺭������ �Ȱ��� �̸��� �����Ѵ�
	* - ATTRIBUTE_ACCESSORS�� Macro�� ���Ǻκ��� �ѹ� ���캸��	
	*/
	ATTRIBUTE_ACCESSORS(UHakHealthSet, Health);
	ATTRIBUTE_ACCESSORS(UHakHealthSet, MaxHealth);
	ATTRIBUTE_ACCESSORS(UHakHealthSet, Healing);

	/**
	* Attribute�� ���� ClampAttribute()�� Ȱ���Ͽ�, ���� ������ ���������ֱ� ����
	* PreAttributeBaseChange�� PreAttributeChange �������̵�
	*/
	void ClampAttribute(const FGameplayAttribute& Attribute, float& NewValue) const;
	virtual void PreAttributeBaseChange(const FGameplayAttribute& Attribute, float& NewValue) const override;
	virtual void PreAttributeChange(const FGameplayAttribute& Attribute, float& NewValue) override;

	/**
	* GameplayEffect�� HealthSet�� Attribute�� �����ϱ� ���� �Ҹ��� �ݹ��Լ��̴�:
	* - �̴� AttributeSet�� �ּ����� �� �����ֵ���, Healing�� ������Ʈ�Ǹ�, Health�� Healing�� �����Ͽ� ������Ʈ �����ϴ�
	*/
	virtual bool PreGameplayEffectExecute(FGameplayEffectModCallbackData& Data) override;
	virtual void PostGameplayEffectExecute(const FGameplayEffectModCallbackData& Data) override;

	/** ���� ü�� */
	UPROPERTY(BlueprintReadOnly, Category = "Hak|Health")
	FGameplayAttributeData Health;

	/** ü�� �ִ�ġ */
	UPROPERTY(BlueprintReadOnly, Category = "Hak|Health")
	FGameplayAttributeData MaxHealth;

	/** ü�� ȸ��ġ */
	UPROPERTY(BlueprintReadOnly, Category = "Hak|Health")
	FGameplayAttributeData Healing;
};
