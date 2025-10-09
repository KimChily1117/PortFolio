// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "ModularCharacter.h"
#include "GameFramework/Character.h"
#include "AbilitySystemInterface.h"
#include "LyraCloneCharacter.generated.h"

class ULyraClonePawnExtensionComponent;
class ULyraCloneCameraComponent;
class ULyraCloneHealthComponent;
class ULyraCloneInventoryManagerComponent;

UCLASS()
class LYRACLONE_API ALyraCloneCharacter : public AModularCharacter, public IAbilitySystemInterface
{
	GENERATED_BODY()

public:
	// Sets default values for this character's properties
	ALyraCloneCharacter();




	void OnAbilitySystemInitialized();
	void OnAbilitySystemUninitialized();




	/**
 * IAbilitySystemInterface
 */
	virtual UAbilitySystemComponent* GetAbilitySystemComponent() const override;


protected:
	// Called when the game starts or when spawned
	virtual void BeginPlay() override;

public:	
	// Called every frame
	virtual void Tick(float DeltaTime) override;

	// Called to bind functionality to input
	virtual void SetupPlayerInputComponent(class UInputComponent* PlayerInputComponent) override;



	//variable
	UPROPERTY(VisibleAnywhere , BlueprintReadOnly , Category = "Hak|Character")
	TObjectPtr<ULyraClonePawnExtensionComponent> PawnExtComponent;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Hak|Character")
	TObjectPtr<ULyraCloneCameraComponent> CameraComponent;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Hak|Character")
	TObjectPtr<ULyraCloneHealthComponent> HealthComponent;


	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Hak|Character")
	TObjectPtr<ULyraCloneInventoryManagerComponent> InventoryManagerComponent = nullptr;
};
