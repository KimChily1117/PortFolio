// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Components/GameFrameworkComponent.h"
#include "Delegates/Delegate.h"
#include "HakHealthComponent.generated.h"

/** forward declarations */
class UHakAbilitySystemComponent;
class UHakHealthSet;
class UHakHealthComponent;
class AActor;
struct FOnAttributeChangeData;


/** Health ��ȭ �ݹ��� ���� ��������Ʈ */
DECLARE_DYNAMIC_MULTICAST_DELEGATE_FourParams(FHakHealth_AttributeChanged, UHakHealthComponent*, HealthComponent, float, OldValue, float, NewValue, AActor*, Instigator);


/**
 * Character(Pawn)�� ���� ü�°��� ó���� ����ϴ� Component�̴�
 * - ����� �ش� Ŭ������ Blueprintable�̴�:
 * - �̴� ��������� Delegate�� UI���� ���ε��ϱ� �����̴� (�ڼ��Ѱ� Ŭ���ϸ鼭 �˾ƺ���)
 */
UCLASS()
class HAKGAME_API UHakHealthComponent : public UGameFrameworkComponent
{
	GENERATED_BODY()
public:
	UHakHealthComponent(const FObjectInitializer& ObjectInitializer);

	/** Actor(���� ACharacter/APawn)�� HealthComponent�� ��ȯ */
	UFUNCTION(BlueprintPure, Category = "Hak|Health")
	static UHakHealthComponent* FindHealthComponent(const AActor* Actor);

	/** �Ʒ��� UFUNCTION�� HealthSet�� Attribute�� �����ϱ� ���� BP Accessor �Լ��� */
	UFUNCTION(BlueprintCallable, Category = "Hak|Health")
	float GetHealth() const;

	UFUNCTION(BlueprintCallable, Category = "Hak|Health")
	float GetMaxHealth() const;

	UFUNCTION(BlueprintCallable, Category = "Hak|Health")
	float GetHealthNormalized() const;

	/** ASC�� HealthSet �ʱ�ȭ */
	void InitializeWithAbilitySystem(UHakAbilitySystemComponent* InASC);
	void UninitializeWithAbilitySystem();

	/** ASC�� ����, HealthSet�� HealthAttribute ������ ������ ȣ���ϴ� �޼��� (���������� OnHealthChanged ȣ��) */
	void HandleHealthChanged(const FOnAttributeChangeData& ChangeData);

	/** HealthSet�� �����ϱ� ���� AbilitySystemComponent */
	UPROPERTY()
	TObjectPtr<UHakAbilitySystemComponent> AbilitySystemComponent;

	/** ĳ�̵� HealthSet ���۷��� */
	UPROPERTY()
	TObjectPtr<const UHakHealthSet> HealthSet;

	/** health ��ȭ�� ���� Delegate(Multicast) */
	UPROPERTY(BlueprintAssignable)
	FHakHealth_AttributeChanged OnHealthChanged;
};
