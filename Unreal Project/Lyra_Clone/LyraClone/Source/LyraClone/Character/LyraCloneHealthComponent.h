// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Components/GameFrameworkComponent.h"
#include "Delegates/Delegate.h"
#include "GameplayEffectTypes.h"
#include "LyraCloneHealthComponent.generated.h"

/** forward declarations */
class ULyraCloneAbilitySystemComponent;
class ULyraCloneHealthSet;
class ULyraCloneHealthComponent;
class AActor;
struct FOnAttributeChangeData;


/** Health 변화 콜백을 위한 델레게이트 */
DECLARE_DYNAMIC_MULTICAST_DELEGATE_FourParams(FLyraCloneHealth_AttributeChanged,
	ULyraCloneHealthComponent*, HealthComponent, float, OldValue, float, NewValue, AActor*, Instigator);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FLyraCloneDamagedSig, float, DamageAmount);

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FLyraCloneDeathStartedSig, AActor*, OwningActor);

/**
 * Character(Pawn)에 대해 체력관련 처리를 담당하는 Component이다
 * - 참고로 해당 클래스는 Blueprintable이다:
 * - 이는 멤버변수인 Delegate를 UI에서 바인딩하기 위함이다 (자세한건 클론하면서 알아보자)
 */


UCLASS()
class LYRACLONE_API ULyraCloneHealthComponent : public UGameFrameworkComponent
{
	GENERATED_BODY()
public:

	ULyraCloneHealthComponent(const FObjectInitializer& ObjectInitializer);

	/** Actor(보통 ACharacter/APawn)의 HealthComponent를 반환 */
	UFUNCTION(BlueprintPure, Category = "LyraClone|Health")
	static ULyraCloneHealthComponent* FindHealthComponent(const AActor* Actor);

	/** 아래의 UFUNCTION은 HealthSet의 Attribute에 접근하기 위한 BP Accessor 함수들 */
	UFUNCTION(BlueprintCallable, Category = "LyraClone|Health")
	float GetHealth() const;

	UFUNCTION(BlueprintCallable, Category = "LyraClone|Health")
	float GetMaxHealth() const;

	UFUNCTION(BlueprintCallable, Category = "LyraClone|Health")
	float GetHealthNormalized() const;

	/** ASC와 HealthSet 초기화 */
	void InitializeWithAbilitySystem(ULyraCloneAbilitySystemComponent* InASC);
	void UninitializeWithAbilitySystem();

	/** ASC를 통해, HealthSet의 HealthAttribute 변경이 있을때 호출하는 메서드 (내부적으로 OnHealthC	   hanged 호출) */
	void HandleHealthChanged(const FOnAttributeChangeData& ChangeData);

	void HandleMaxHealthChanged(const FOnAttributeChangeData& ChangeData);

	void HandleOutOfHealth(
		AActor* DamageInstigator,
		AActor* DamageCauser,
		const FGameplayEffectSpec& DamageEffectSpec,
		float DamageMagnitude
	);
	/** HealthSet을 접근하기 위한 AbilitySystemComponent */
	UPROPERTY()
	TObjectPtr<ULyraCloneAbilitySystemComponent> AbilitySystemComponent;

	/** 캐싱된 HealthSet 레퍼런스 */
	UPROPERTY()
	TObjectPtr<const ULyraCloneHealthSet> HealthSet;


	UPROPERTY(BlueprintReadOnly, Category = "LyraClone|Health")
	bool bIsDead = false;

	UFUNCTION(BlueprintPure, Category = "LyraClone|Health")
	bool IsDead() const { return bIsDead; }

	//UI
	/** health 변화에 따른 Delegate(Multicast) */
	UPROPERTY(BlueprintAssignable)
	FLyraCloneHealth_AttributeChanged OnHealthChanged;

	UPROPERTY(BlueprintAssignable)
	FLyraCloneHealth_AttributeChanged OnMaxHealthChanged;


	UPROPERTY(BlueprintAssignable, Category = "LyraClone|Health")
	FLyraCloneDamagedSig OnDamaged;

	UPROPERTY(BlueprintAssignable, Category = "LyraClone|Health")
	FLyraCloneDeathStartedSig OnDeathStarted;

};

