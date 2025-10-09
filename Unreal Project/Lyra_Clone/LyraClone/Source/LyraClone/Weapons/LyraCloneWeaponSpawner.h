// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "LyraCloneWeaponSpawner.generated.h"


namespace EEndPlayReason { enum Type : int; }

class APawn;
class UCapsuleComponent;
class ULyraCloneInventoryItemDefinition;
class ULyraCloneWeaponPickupDefinition;
class UObject;
class UPrimitiveComponent;
class UStaticMeshComponent;
struct FFrame;
struct FGameplayTag;
struct FHitResult;

UCLASS(Blueprintable, BlueprintType)
class LYRACLONE_API ALyraCloneWeaponSpawner : public AActor
{
	GENERATED_BODY()
	
public:	
	// Sets default values for this actor's properties
	ALyraCloneWeaponSpawner();

protected:
	// Called when the game starts or when spawned
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

public:	
	// Called every frame
	virtual void Tick(float DeltaTime) override;

	void OnConstruction(const FTransform& Transform) override;

protected:
	//Data asset used to configure a Weapon Spawner
	UPROPERTY(EditInstanceOnly, BlueprintReadOnly, Category = "Lyra|WeaponPickup")
	TObjectPtr<ULyraCloneWeaponPickupDefinition> WeaponDefinition;

	UPROPERTY(EditDefaultsOnly, BlueprintReadWrite, ReplicatedUsing = OnRep_WeaponAvailability, Category = "Lyra|WeaponPickup")
	bool bIsWeaponAvailable;

	//The amount of time between weapon pickup and weapon spawning in seconds
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Lyra|WeaponPickup")
	float CoolDownTime;

	//Delay between when the weapon is made available and when we check for a pawn standing in the spawner. Used to give the bIsWeaponAvailable OnRep time to fire and play FX. 
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Lyra|WeaponPickup")
	float CheckExistingOverlapDelay;

	//Used to drive weapon respawn time indicators 0-1
	UPROPERTY(BlueprintReadOnly, Transient, Category = "Lyra|WeaponPickup")
	float CoolDownPercentage;

public:

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Lyra|WeaponPickup")
	TObjectPtr<UCapsuleComponent> CollisionVolume;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Lyra|WeaponPickup")
	TObjectPtr<UStaticMeshComponent> PadMesh;

	UPROPERTY(BlueprintReadOnly, Category = "Lyra|WeaponPickup")
	TObjectPtr<UStaticMeshComponent> WeaponMesh;

	UPROPERTY(EditDefaultsOnly, BlueprintReadWrite, Category = "Lyra|WeaponPickup")
	float WeaponMeshRotationSpeed;

	FTimerHandle CoolDownTimerHandle;

	FTimerHandle CheckOverlapsDelayTimerHandle;

	UFUNCTION()
	void OnOverlapBegin(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepHitResult);

	//Check for pawns standing on pad when the weapon is spawned. 
	void CheckForExistingOverlaps();

	void AttemptPickUpWeapon(APawn* Pawn);

	bool GiveWeapon(TSubclassOf<ULyraCloneInventoryItemDefinition> WeaponItemClass, APawn* ReceivingPawn);

	void StartCoolDown();

	UFUNCTION(BlueprintCallable, Category = "Lyra|WeaponPickup")
	void ResetCoolDown();

	UFUNCTION()
	void OnCoolDownTimerComplete();

	void SetWeaponPickupVisibility(bool bShouldBeVisible);

	UFUNCTION(BlueprintNativeEvent, Category = "Lyra|WeaponPickup")
	void PlayPickupEffects();

	UFUNCTION(BlueprintNativeEvent, Category = "Lyra|WeaponPickup")
	void PlayRespawnEffects();

	UFUNCTION()
	void OnRep_WeaponAvailability();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	/** Searches an item definition type for a matching stat and returns the value, or 0 if not found */
	UFUNCTION(BlueprintCallable, BlueprintPure, Category = "Lyra|WeaponPickup")
	static int32 GetDefaultStatFromItemDef(const TSubclassOf<ULyraCloneInventoryItemDefinition> WeaponItemClass, FGameplayTag StatTag);
};

