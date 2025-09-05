// Fill out your copyright notice in the Description page of Project Settings.


#include "HakHealthComponent.h"
#include "HakGame/HakLogChannels.h"
#include "GameplayEffect.h"
#include "GameplayEffectExtension.h"
#include "HakGame/AbilitySystem/HakAbilitySystemComponent.h"
#include "HakGame/AbilitySystem/Attributes/HakHealthSet.h"

UHakHealthComponent::UHakHealthComponent(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
	// HealthComponent�� PlayerState�� HealthSet�� Character(Pawn)�� Bridge ��Ȱ�̶�� �����ϸ� �ȴ�:
	// - ���� ������ ������Ʈ �� �ʿ䰡 ���� �̺�Ʈ ������� �����ϴ� ������Ʈ�� �����ϸ� �ȴ�
	PrimaryComponentTick.bStartWithTickEnabled = false;
	PrimaryComponentTick.bCanEverTick = false;

	// InitializeWithAbilitySystem���� ASC�� �ʱ�ȭ�Ǳ� ������ HealthSet�� ASC�� null�̴�:
	AbilitySystemComponent = nullptr;
	HealthSet = nullptr;
}

void UHakHealthComponent::InitializeWithAbilitySystem(UHakAbilitySystemComponent* InASC)
{
	// AActor�� HakCharacter�� ��ӹް� �ִ� Ŭ������ ���̴�
	AActor* Owner = GetOwner();
	check(Owner);

	if (AbilitySystemComponent)
	{
		UE_LOG(LogHak, Error, TEXT("HakHealthComponent: Health component for owner [%s] has already been initialized with an ability system."), *GetNameSafe(Owner));
		return;
	}

	// ASC ĳ��
	AbilitySystemComponent = InASC;
	if (!AbilitySystemComponent)
	{
		UE_LOG(LogHak, Error, TEXT("HakHealthComponent: Cannot initialize health component for owner [%s] with NULL ability system."), *GetNameSafe(Owner));
		return;
	}

	// AbilitySystemComponent::GetSet�� SpawnedAttributes���� �����´�:
	// - �ٵ� PlayerState���� Subobject�� �����ϰ� ���� ASC�� ��������� ���µ� ��� ��ϵǾ�������?
	//   - AbilitySystemComponent::InitializeComponent()���� GetObjectsWithOuter�� SpawnedAttributes�� �߰��ȴ�:
	//   - �� �����غ��� HealthSet�� PlayerState�� Subobject�� �ְ�, ASC ���� PlayerState�� �ִ�:
	//     -> �̴� ASC���� GetObjectsWithOuter�� HealthSet�� ���ٵȴ�!!!
	// - �ѹ� AbilitySystemComponent::InitializeComponent()�� ����
	HealthSet = AbilitySystemComponent->GetSet<UHakHealthSet>();
	if (!HealthSet)
	{
		UE_LOG(LogHak, Error, TEXT("HakHealthComponent: Cannot initialize health component for owner [%s] with NULL health set on the ability system."), *GetNameSafe(Owner));
		return;
	}

	// HealthSet�� HealthAttribute�� ������Ʈ�� �Ͼ������ ȣ���� �ݹ����� ����޼��� HandleHealthChanged�� �������:
	AbilitySystemComponent->GetGameplayAttributeValueChangeDelegate(UHakHealthSet::GetHealthAttribute()).AddUObject(this, &ThisClass::HandleHealthChanged);

	// �ʱ�ȭ �ѹ� �������ϱ� Broadcast ������
	OnHealthChanged.Broadcast(this, 0, HealthSet->GetHealth(), nullptr);
}

void UHakHealthComponent::UninitializeWithAbilitySystem()
{
	AbilitySystemComponent = nullptr;
	HealthSet = nullptr;
}

static AActor* GetInstigatorFromAttrChangeData(const FOnAttributeChangeData& ChangeData)
{
	// GameEffectModifier�� Data�� ���� ��츸 ȣ��Ǵ°����� (��� �츮�� ũ�� ���ɾ���)
	if (ChangeData.GEModData != nullptr)
	{
		const FGameplayEffectContextHandle& EffectContext = ChangeData.GEModData->EffectSpec.GetEffectContext();
		return EffectContext.GetOriginalInstigator();
	}
	return nullptr;
}

void UHakHealthComponent::HandleHealthChanged(const FOnAttributeChangeData& ChangeData)
{
	OnHealthChanged.Broadcast(this, ChangeData.OldValue, ChangeData.NewValue, GetInstigatorFromAttrChangeData(ChangeData));
}

UHakHealthComponent* UHakHealthComponent::FindHealthComponent(const AActor* Actor)
{
	if (!Actor)
	{
		return nullptr;
	}

	UHakHealthComponent* HealthComponent = Actor->FindComponentByClass<UHakHealthComponent>();
	return HealthComponent;
}

float UHakHealthComponent::GetHealth() const
{
	return (HealthSet ? HealthSet->GetHealth() : 0.0f);
}

float UHakHealthComponent::GetMaxHealth() const
{
	return (HealthSet ? HealthSet->GetMaxHealth() : 0.0f);
}

float UHakHealthComponent::GetHealthNormalized() const
{
	if (HealthSet)
	{
		const float Health = HealthSet->GetHealth();
		const float MaxHealth = HealthSet->GetMaxHealth();
		return ((MaxHealth > 0.0f) ? (Health / MaxHealth) : 0.0f);
	}
	return 0.0f;
}
