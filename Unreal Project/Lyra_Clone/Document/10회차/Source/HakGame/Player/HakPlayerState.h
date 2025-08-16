// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/PlayerState.h"
#include "HakPlayerState.generated.h"

class UHakAbilitySystemComponent;
class UHakPawnData;
class UHakExperienceDefinition;

/**
 * 
 */
UCLASS()
class HAKGAME_API AHakPlayerState : public APlayerState
{
	GENERATED_BODY()
public:
	AHakPlayerState(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/**
	 * AActor's interface
	 */
	virtual void PostInitializeComponents() final;

	/**
	 * member methods
	 */
	template <class T>
	const T* GetPawnData() const { return Cast<T>(PawnData); }
	void OnExperienceLoaded(const UHakExperienceDefinition* CurrentExperience);
	void SetPawnData(const UHakPawnData* InPawnData);
	UHakAbilitySystemComponent* GetHakAbilitySystemComponent() const { return AbilitySystemComponent; }

	UPROPERTY()
	TObjectPtr<const UHakPawnData> PawnData;

	UPROPERTY(VisibleAnywhere, Category = "Hak|PlayerState")
	TObjectPtr<UHakAbilitySystemComponent> AbilitySystemComponent;
};
