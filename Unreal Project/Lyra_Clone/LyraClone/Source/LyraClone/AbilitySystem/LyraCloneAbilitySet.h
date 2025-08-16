// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "GameplayAbilitySpec.h"
#include "LyraCloneAbilitySet.generated.h"


class ULyraCloneAbilitySystemComponent;
class ULyraCloneGameplayAbility;

/**
 * GameplayAbility�� Wrapper Ŭ����
 * - �߰����� Ŀ���͸���¡�� ������
 */
USTRUCT(BlueprintType)
struct FLyraCloneAbilitySet_GameplayAbility
{
	GENERATED_BODY()
public:
	/** ���� GameplayAbility */
	UPROPERTY(EditDefaultsOnly)
	TSubclassOf<ULyraCloneGameplayAbility> Ability = nullptr;

	/** Input ó���� ���� GameplayTag */
	UPROPERTY(EditDefaultsOnly)
	FGameplayTag InputTag;

	/** Ability�� ��� ���� (Level) */
	UPROPERTY(EditDefaultsOnly)
	int32 AbilityLevel = 1;
};

USTRUCT(BlueprintType)
struct FLyraCloneAbilitySet_GrantedHandles
{
	GENERATED_BODY()

	void AddAbilitySpecHandle(const FGameplayAbilitySpecHandle& Handle);
	void TakeFromAbilitySystem(ULyraCloneAbilitySystemComponent* HakASC);

protected:
	/** ���� GameplayAbilitySpecHandle(int32) */
	UPROPERTY()
	TArray<FGameplayAbilitySpecHandle> AbilitySpecHandles;
};


/**
 * 
 */
UCLASS()
class LYRACLONE_API ULyraCloneAbilitySet : public UPrimaryDataAsset
{
	GENERATED_BODY()
public:
	ULyraCloneAbilitySet(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/** ASC�� ��밡���� Ability�� �߰��Ѵ� */
	void GiveToAbilitySystem(ULyraCloneAbilitySystemComponent* HakASC, FLyraCloneAbilitySet_GrantedHandles* OutGrantedHandles, UObject* SourceObject = nullptr);

	/** ���� GameplayAbilities */
	UPROPERTY(EditDefaultsOnly, Category = "Gameplay Abilities")
	TArray<FLyraCloneAbilitySet_GameplayAbility> GrantedGameplayAbilities;
};
