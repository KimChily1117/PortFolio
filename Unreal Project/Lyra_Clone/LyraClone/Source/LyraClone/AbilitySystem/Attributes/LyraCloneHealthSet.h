// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "LyraCloneAttributeSet.h"
#include "AbilitySystemComponent.h"
#include "LyraCloneHealthSet.generated.h"


class AActor;
struct FGameplayEffectSpec;
struct FGameplayEffectModCallbackData;

DECLARE_MULTICAST_DELEGATE_FourParams(
	FLyraCloneOutOfHealthEvent,
	AActor* /*Instigator*/,
	AActor* /*Causer*/,
	const FGameplayEffectSpec& /*EffectSpec*/,
	float /*EffectMagnitude*/
);


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
	* 앞서 HakAttributeSet에서 정의했던, ATTRIBUTE_ACCESSORS를 통해, 아래 정의함 멤벼변수와 똑같이 이름을 설정한다
	* - ATTRIBUTE_ACCESSORS의 Macro의 정의부분을 한번 실펴보자	
	*/
	ATTRIBUTE_ACCESSORS(ULyraCloneHealthSet, Health);
	ATTRIBUTE_ACCESSORS(ULyraCloneHealthSet, MaxHealth);
	ATTRIBUTE_ACCESSORS(ULyraCloneHealthSet, Healing);
	ATTRIBUTE_ACCESSORS(ULyraCloneHealthSet, Damage);


	// Health가 0이 되는 순간 1회 Broadcast
	mutable FLyraCloneOutOfHealthEvent OnOutOfHealth;




	// Replication
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;


	/**
	* Attribute의 값을 ClampAttribute()를 활용하여, 값의 범위를 유지시켜주기 위해
	* PreAttributeBaseChange와 PreAttributeChange 오버라이드
	*/
	void ClampAttribute(const FGameplayAttribute& Attribute, float& NewValue) const;
	virtual void PreAttributeBaseChange(const FGameplayAttribute& Attribute, float& NewValue) const override;
	virtual void PreAttributeChange(const FGameplayAttribute& Attribute, float& NewValue) override;

	/**
	* GameplayEffect가 HealthSet의 Attribute를 수정하기 전에 불리는 콜백함수이다:
	* - 이는 AttributeSet의 주석에도 잘 나와있듯이, Healing이 업데이트되면, Health를 Healing을 적용하여 업데이트 가능하다
	*/
	virtual bool PreGameplayEffectExecute(FGameplayEffectModCallbackData& Data) override;
	virtual void PostGameplayEffectExecute(const FGameplayEffectModCallbackData& Data) override;

	/** 현재 체력 */
	UPROPERTY(BlueprintReadOnly, Category = "LyraClone|Health")
	FGameplayAttributeData Health;

	/** 체력 최대치 */
	UPROPERTY(BlueprintReadOnly, Category = "LyraClone|Health")
	FGameplayAttributeData MaxHealth;

	/** 체력 회복치 */
	UPROPERTY(BlueprintReadOnly, Category = "LyraClone|Health")
	FGameplayAttributeData Healing;

	/** 데미지 */
	UPROPERTY(BlueprintReadOnly, Category = "LyraClone|Health")
	FGameplayAttributeData Damage;
};
