// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/PlayerState.h"
#include "AbilitySystemInterface.h"  
#include "LyraClonePlayerState.generated.h"

class ULyraClonePawnData;
class ULyraCloneExperienceDefinition;
class ULyraCloneAbilitySystemComponent;
/**
 * 
 */
UCLASS()
class LYRACLONE_API ALyraClonePlayerState : public APlayerState , public IAbilitySystemInterface
{
	GENERATED_BODY()

public:
	ALyraClonePlayerState(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());


	/*
	 * AActor's interface
	 */
	virtual void PostInitializeComponents() final;

	/*
	 * member methods
	 */
	template <class T>
	const T* GetPawnData() const { return Cast<T>(PawnData); }
	void OnExperienceLoaded(const ULyraCloneExperienceDefinition* CurrentExperience);
	void SetPawnData(const ULyraClonePawnData* InPawnData);

	// IAbilitySystemInterface 필수 구현
	virtual UAbilitySystemComponent* GetAbilitySystemComponent() const override;

	ULyraCloneAbilitySystemComponent* GetHakAbilitySystemComponent() const { return AbilitySystemComponent; }
	

	UPROPERTY()
	TObjectPtr<const ULyraClonePawnData> PawnData;

	UPROPERTY(VisibleAnywhere, Category = "LyraClone|PlayerState")
	TObjectPtr<ULyraCloneAbilitySystemComponent> AbilitySystemComponent;
	
};
