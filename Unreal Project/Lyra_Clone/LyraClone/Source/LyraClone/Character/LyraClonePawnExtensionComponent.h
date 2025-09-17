// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Components/PawnComponent.h"
#include "Components/GameFrameworkInitStateInterface.h"
#include "LyraClonePawnExtensionComponent.generated.h"

class ULyraClonePawnData;
class ULyraCloneAbilitySystemComponent;

/**
 * �ʱ�ȭ ������ �����ϴ� ������Ʈ
 */



UCLASS()
class LYRACLONE_API ULyraClonePawnExtensionComponent : public UPawnComponent , public IGameFrameworkInitStateInterface
{
	GENERATED_BODY()	

public:
	ULyraClonePawnExtensionComponent(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());



	/** FeatureName ���� */
	static const FName NAME_ActorFeatureName;

	/**
	* member methods
	*/
	static ULyraClonePawnExtensionComponent* FindPawnExtensionComponent(const AActor* Actor) { return (Actor ? Actor->FindComponentByClass<ULyraClonePawnExtensionComponent>() : nullptr); }
	template <class T>
	const T* GetPawnData() const { return Cast<T>(PawnData); }
	void SetPawnData(const ULyraClonePawnData* InPawnData);
	void SetupPlayerInputComponent();
	ULyraCloneAbilitySystemComponent* GetLyraCloneAbilitySystemComponent() const { return AbilitySystemComponent; }

	// AbilitySystemComponent�� AvatorActor ��� �ʱ�ȭ/��ü ȣ��
	void InitializeAbilitySystem(ULyraCloneAbilitySystemComponent* InASC, AActor* InOwnerActor);
	void UninitializeAbilitySystem();


	/** OnAbilitySystem[Initialized|Uninitialized] Delegate�� �߰�: */
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
	 * Pawn�� ������ �����͸� ĳ��
	 */
	UPROPERTY(EditInstanceOnly, Category = "Hak|Pawn")
	TObjectPtr<const ULyraClonePawnData> PawnData;

	// AbilitySystemComponent
	UPROPERTY()
	TObjectPtr<ULyraCloneAbilitySystemComponent> AbilitySystemComponent;

	/** ASC Init�� Uninit�� Delegate �߰� */
	FSimpleMulticastDelegate OnAbilitySystemInitialized;
	FSimpleMulticastDelegate OnAbilitySystemUninitialized;
};