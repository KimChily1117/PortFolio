// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraCloneHealthComponent.h"
#include "LyraClone/LyraLogSystem.h"
#include "GameplayEffect.h"
#include "GameplayEffectExtension.h"
#include "LyraClone/AbilitySystem/LyraCloneAbilitySystemComponent.h"
#include "LyraClone/AbilitySystem/Attributes/LyraCloneHealthSet.h"


ULyraCloneHealthComponent::ULyraCloneHealthComponent(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
	// HealthComponent�� PlayerState�� HealthSet�� Character(Pawn)�� Bridge ��Ȱ�̶�� �����ϸ� �ȴ�:
	// - ���� ������ ������Ʈ �� �ʿ䰡 ���� �̺�Ʈ ������� �����ϴ� ������Ʈ�� �����ϸ� �ȴ�
	PrimaryComponentTick.bStartWithTickEnabled = false;
	PrimaryComponentTick.bCanEverTick = false;

	// InitializeWithAbilitySystem���� ASC�� �ʱ�ȭ�Ǳ� ������ HealthSet�� ASC�� null�̴�:
	AbilitySystemComponent = nullptr;
	HealthSet = nullptr;
}

void ULyraCloneHealthComponent::InitializeWithAbilitySystem(ULyraCloneAbilitySystemComponent* InASC)
{
	// AActor�� HakCharacter�� ��ӹް� �ִ� Ŭ������ ���̴�
	AActor* Owner = GetOwner();
	check(Owner);

	if (AbilitySystemComponent)
	{
		UE_LOG(LogLyraClone, Error, TEXT("HealthComponent: Health component for owner [%s] has already been initialized with an ability system."), *GetNameSafe(Owner));
		return;
	}

	// ASC ĳ��
	AbilitySystemComponent = InASC;
	if (!AbilitySystemComponent)
	{
		UE_LOG(LogLyraClone, Error, TEXT("HealthComponent: Cannot initialize health component for owner [%s] with NULL ability system."), *GetNameSafe(Owner));
		return;
	}

	// AbilitySystemComponent::GetSet�� SpawnedAttributes���� �����´�:
	// - �ٵ� PlayerState���� Subobject�� �����ϰ� ���� ASC�� ��������� ���µ� ��� ��ϵǾ�������?
	//   - AbilitySystemComponent::InitializeComponent()���� GetObjectsWithOuter�� SpawnedAttributes�� �߰��ȴ�:
	//   - �� �����غ��� HealthSet�� PlayerState�� Subobject�� �ְ�, ASC ���� PlayerState�� �ִ�:
	//     -> �̴� ASC���� GetObjectsWithOuter�� HealthSet�� ���ٵȴ�!!!
	// - �ѹ� AbilitySystemComponent::InitializeComponent()�� ����
	HealthSet = AbilitySystemComponent->GetSet<ULyraCloneHealthSet>();
	if (!HealthSet)
	{
		UE_LOG(LogLyraClone, Error, TEXT("HealthComponent: Cannot initialize health component for owner [%s] with NULL health set on the ability system."), *GetNameSafe(Owner));
		return;
	}

	// HealthSet�� HealthAttribute�� ������Ʈ�� �Ͼ������ ȣ���� �ݹ����� ����޼��� HandleHealthChanged�� �������:
	AbilitySystemComponent->GetGameplayAttributeValueChangeDelegate(ULyraCloneHealthSet::GetHealthAttribute()).AddUObject(this, &ThisClass::HandleHealthChanged);

	// �ʱ�ȭ �ѹ� �������ϱ� Broadcast ������
	OnHealthChanged.Broadcast(this, 0, HealthSet->GetHealth(), nullptr);
}


void ULyraCloneHealthComponent::UninitializeWithAbilitySystem()
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


void ULyraCloneHealthComponent::HandleHealthChanged(const FOnAttributeChangeData& ChangeData)
{
	OnHealthChanged.Broadcast(this, ChangeData.OldValue, ChangeData.NewValue, GetInstigatorFromAttrChangeData(ChangeData));
}



ULyraCloneHealthComponent* ULyraCloneHealthComponent::FindHealthComponent(const AActor* Actor)
{
	if (!Actor)
	{
		return nullptr;
	}

	ULyraCloneHealthComponent* HealthComponent = Actor->FindComponentByClass<ULyraCloneHealthComponent>();
	return HealthComponent;
}

float ULyraCloneHealthComponent::GetHealth() const
{
	return (HealthSet ? HealthSet->GetHealth() : 0.0f);
}

float ULyraCloneHealthComponent::GetMaxHealth() const
{
	return (HealthSet ? HealthSet->GetMaxHealth() : 0.0f);
}

float ULyraCloneHealthComponent::GetHealthNormalized() const
{
	if (HealthSet)
	{
		const float Health = HealthSet->GetHealth();
		const float MaxHealth = HealthSet->GetMaxHealth();
		return ((MaxHealth > 0.0f) ? (Health / MaxHealth) : 0.0f);
	}
	return 0.0f;
}
