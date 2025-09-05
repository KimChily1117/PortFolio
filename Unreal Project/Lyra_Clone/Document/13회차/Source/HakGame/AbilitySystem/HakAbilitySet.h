// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "GameplayAbilitySpec.h"
#include "HakAbilitySet.generated.h"

class UHakAbilitySystemComponent;
class UHakGameplayAbility;

/**
 * GameplayAbility�� Wrapper Ŭ����
 * - �߰����� Ŀ���͸���¡�� ������
 */
USTRUCT(BlueprintType)
struct FHakAbilitySet_GameplayAbility
{
	GENERATED_BODY()
public:
	/** ���� GameplayAbility */
	UPROPERTY(EditDefaultsOnly)
	TSubclassOf<UHakGameplayAbility> Ability = nullptr;

	/** Input ó���� ���� GameplayTag */
	UPROPERTY(EditDefaultsOnly)
	FGameplayTag InputTag;

	/** Ability�� ��� ���� (Level) */
	UPROPERTY(EditDefaultsOnly)
	int32 AbilityLevel = 1;
};

USTRUCT(BlueprintType)
struct FHakAbilitySet_GrantedHandles
{
	GENERATED_BODY()

	void AddAbilitySpecHandle(const FGameplayAbilitySpecHandle& Handle);
	void TakeFromAbilitySystem(UHakAbilitySystemComponent* HakASC);

protected:
	/** ���� GameplayAbilitySpecHandle(int32) */
	UPROPERTY()
	TArray<FGameplayAbilitySpecHandle> AbilitySpecHandles;
};

/**
 * 
 */
UCLASS()
class HAKGAME_API UHakAbilitySet : public UPrimaryDataAsset
{
	GENERATED_BODY()
public:
	UHakAbilitySet(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/** ASC�� ��밡���� Ability�� �߰��Ѵ� */
	void GiveToAbilitySystem(UHakAbilitySystemComponent* HakASC, FHakAbilitySet_GrantedHandles* OutGrantedHandles, UObject* SourceObject = nullptr);

	/** ���� GameplayAbilities */
	UPROPERTY(EditDefaultsOnly, Category = "Gameplay Abilities")
	TArray<FHakAbilitySet_GameplayAbility> GrantedGameplayAbilities;
};
