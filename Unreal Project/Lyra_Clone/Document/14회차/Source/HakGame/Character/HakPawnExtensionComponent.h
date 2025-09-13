// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Components/PawnComponent.h"
#include "Components/GameFrameworkInitStateInterface.h"
#include "HakPawnExtensionComponent.generated.h"

class UHakAbilitySystemComponent;
class UHakPawnData;
/**
 * 초기화 전반을 조정하는 컴포넌트
 */
UCLASS()
class HAKGAME_API UHakPawnExtensionComponent : public UPawnComponent, public IGameFrameworkInitStateInterface
{
	GENERATED_BODY()
public:
	UHakPawnExtensionComponent(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/** FeatureName 정의 */
	static const FName NAME_ActorFeatureName;

	/**
	* member methods
	*/
	static UHakPawnExtensionComponent* FindPawnExtensionComponent(const AActor* Actor) { return (Actor ? Actor->FindComponentByClass<UHakPawnExtensionComponent>() : nullptr); }
	template <class T>
	const T* GetPawnData() const { return Cast<T>(PawnData); }
	void SetPawnData(const UHakPawnData* InPawnData);
	void SetupPlayerInputComponent();
	UHakAbilitySystemComponent* GetHakAbilitySystemComponent() const { return AbilitySystemComponent; }

	/** AbilitySystemComponent의 AvatorActor 대상 초기화/해제 호출 */
	void InitializeAbilitySystem(UHakAbilitySystemComponent* InASC, AActor* InOwnerActor);
	void UninitializeAbilitySystem();

	/** OnAbilitySystem[Initialized|Uninitialized] Delegate에 추가: */
	void OnAbilitySystemInitialized_RegisterAndCall(FSimpleMulticastDelegate::FDelegate Delegate);
	void OnAbilitySystemUninitialized_Register(FSimpleMulticastDelegate::FDelegate Delegate);

	/**
	* UPawnComponent interfaces
	 */
	virtual void OnRegister() final;
	virtual void BeginPlay() final;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) final;

	/**
	 * IGameFrameworkInitStateInterface
	 */
	virtual FName GetFeatureName() const final { return NAME_ActorFeatureName; }
	virtual void OnActorInitStateChanged(const FActorInitStateChangedParams& Params) final;
	virtual bool CanChangeInitState(UGameFrameworkComponentManager* Manager, FGameplayTag CurrentState, FGameplayTag DesiredState) const final;
	virtual void CheckDefaultInitialization() final;

	/**
	 * Pawn을 생성한 데이터를 캐싱
	 */
	UPROPERTY(EditInstanceOnly, Category = "Hak|Pawn")
	TObjectPtr<const UHakPawnData> PawnData;

	/** AbilitySystemComponent 캐싱 */
	UPROPERTY()
	TObjectPtr<UHakAbilitySystemComponent> AbilitySystemComponent;

	/** ASC Init과 Uninit의 Delegate 추가 */
	FSimpleMulticastDelegate OnAbilitySystemInitialized;
	FSimpleMulticastDelegate OnAbilitySystemUninitialized;
};
