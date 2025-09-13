// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "LyraCloneAttributeSet.h"
#include "AbilitySystemComponent.h"
#include "LyraCloneHealthSet.generated.h"

/**
 * 
 */
UCLASS()
class LYRACLONE_API ULyraCloneHealthSet : public ULyraCloneAttributeSet
{
	GENERATED_BODY()
public:
	ULyraCloneHealthSet();

	/**
	* �ռ� HakAttributeSet���� �����ߴ�, ATTRIBUTE_ACCESSORS�� ����, �Ʒ� ������ �⺭������ �Ȱ��� �̸��� �����Ѵ�
	* - ATTRIBUTE_ACCESSORS�� Macro�� ���Ǻκ��� �ѹ� ���캸��	
	*/
	ATTRIBUTE_ACCESSORS(ULyraCloneHealthSet, Health);
	ATTRIBUTE_ACCESSORS(ULyraCloneHealthSet, MaxHealth);
	ATTRIBUTE_ACCESSORS(ULyraCloneHealthSet, Healing);

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
	UPROPERTY(BlueprintReadOnly, Category = "LyraClone|Health")
	FGameplayAttributeData Health;

	/** ü�� �ִ�ġ */
	UPROPERTY(BlueprintReadOnly, Category = "LyraClone|Health")
	FGameplayAttributeData MaxHealth;

	/** ü�� ȸ��ġ */
	UPROPERTY(BlueprintReadOnly, Category = "LyraClone|Health")
	FGameplayAttributeData Healing;
};
