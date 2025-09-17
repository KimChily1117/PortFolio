// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Components/GameFrameworkComponent.h"
#include "Delegates/Delegate.h"
#include "LyraCloneHealthComponent.generated.h"


/** forward declarations */
class ULyraCloneAbilitySystemComponent;
class ULyraCloneHealthSet;
class ULyraCloneHealthComponent;
class AActor;
struct FOnAttributeChangeData;


/** Health ��ȭ �ݹ��� ���� ��������Ʈ */
DECLARE_DYNAMIC_MULTICAST_DELEGATE_FourParams(FLyraCloneHealth_AttributeChanged,
	ULyraCloneHealthComponent*, HealthComponent, float, OldValue, float, NewValue, AActor*, Instigator);


/**
 * Character(Pawn)�� ���� ü�°��� ó���� ����ϴ� Component�̴�
 * - ����� �ش� Ŭ������ Blueprintable�̴�:
 * - �̴� ��������� Delegate�� UI���� ���ε��ϱ� �����̴� (�ڼ��Ѱ� Ŭ���ϸ鼭 �˾ƺ���)
 */


UCLASS()
class LYRACLONE_API ULyraCloneHealthComponent : public UGameFrameworkComponent
{
	GENERATED_BODY()
public:

	ULyraCloneHealthComponent(const FObjectInitializer& ObjectInitializer);

	/** Actor(���� ACharacter/APawn)�� HealthComponent�� ��ȯ */
	UFUNCTION(BlueprintPure, Category = "LyraClone|Health")
	static ULyraCloneHealthComponent* FindHealthComponent(const AActor* Actor);

	/** �Ʒ��� UFUNCTION�� HealthSet�� Attribute�� �����ϱ� ���� BP Accessor �Լ��� */
	UFUNCTION(BlueprintCallable, Category = "LyraClone|Health")
	float GetHealth() const;

	UFUNCTION(BlueprintCallable, Category = "LyraClone|Health")
	float GetMaxHealth() const;

	UFUNCTION(BlueprintCallable, Category = "LyraClone|Health")
	float GetHealthNormalized() const;

	/** ASC�� HealthSet �ʱ�ȭ */
	void InitializeWithAbilitySystem(ULyraCloneAbilitySystemComponent* InASC);
	void UninitializeWithAbilitySystem();

	/** ASC�� ����, HealthSet�� HealthAttribute ������ ������ ȣ���ϴ� �޼��� (���������� OnHealthChanged ȣ��) */
	void HandleHealthChanged(const FOnAttributeChangeData& ChangeData);

	/** HealthSet�� �����ϱ� ���� AbilitySystemComponent */
	UPROPERTY()
	TObjectPtr<ULyraCloneAbilitySystemComponent> AbilitySystemComponent;

	/** ĳ�̵� HealthSet ���۷��� */
	UPROPERTY()
	TObjectPtr<const ULyraCloneHealthSet> HealthSet;

	/** health ��ȭ�� ���� Delegate(Multicast) */
	UPROPERTY(BlueprintAssignable)
	FLyraCloneHealth_AttributeChanged OnHealthChanged;
};

