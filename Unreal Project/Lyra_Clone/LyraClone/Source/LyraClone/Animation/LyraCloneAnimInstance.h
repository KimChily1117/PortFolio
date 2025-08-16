// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameplayEffectTypes.h"
#include "Animation/AnimInstance.h"
#include "LyraCloneAnimInstance.generated.h"

/**
 *
 */
UCLASS()
class LYRACLONE_API ULyraCloneAnimInstance : public UAnimInstance

{
	GENERATED_BODY()
public:

	ULyraCloneAnimInstance(const FObjectInitializer& Fobj = FObjectInitializer::Get());

	/**
	* UAnimInstance's interface
	*/
	virtual void NativeInitializeAnimation() override;

	/**
	 * member methods
	 */
	void InitializeWithAbilitySystem(UAbilitySystemComponent* ASC);

	/** �ش� �Ӽ����� Lyra�� AnimBP���� ���Ǵ� ���̹Ƿ� ���������� */
	UPROPERTY(BlueprintReadOnly, Category = "Character State Data")
	float GroundDistance = -1.0f;

	/** GameplayTag�� AnimInstance�� �Ӽ����� �������ش� */
	UPROPERTY(EditDefaultsOnly, Category = "GameplayTags")
	FGameplayTagBlueprintPropertyMap GameplayTagPropertyMap;
};

