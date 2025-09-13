// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Uobject/object.h"
#include "Uobject/UobjectGlobals.h"
#include "Containers/Array.h"
#include "UObject/NoExportTypes.h"
#include "LyraCloneEquipmentInstance.generated.h"

struct FLyraCloneEquipmentActorToSpawn;

/**
 * 
 */
UCLASS(BlueprintType , Blueprintable)
class LYRACLONE_API ULyraCloneEquipmentInstance : public UObject
{
	GENERATED_BODY()

public:
	ULyraCloneEquipmentInstance(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());
	
	//BP에서 호출 할 Equip / unequip 함수

	UFUNCTION(BlueprintImplementableEvent, Category = Equipment, meta = (DisplayName = "OnEquipped"))
	void K2_OnEquipped();


	UFUNCTION(BlueprintImplementableEvent, Category = Equipment, meta = (DisplayName = "OnUNEquipped"))
	void K2_OnUnEquipped();

	// Interfaces 

	virtual void OnEquipped();
	virtual void OnUnequipped();


	UFUNCTION(BlueprintPure, Category = Equipment)
	APawn* GetPawn() const;


	UFUNCTION(BlueprintPure, Category = Equipment)
	TArray<AActor*> GetSpawnedActors() const { return SpawnedActors; }


	UFUNCTION(BlueprintPure, Category = Equipment)
	UObject* GetInstigator() const { return Instigator; }


	/**
 * DeterminesOutputType은 C++ 정의에는 APawn* 반환하지만, BP에서는 PawnType에 따라 OutputType이 결정되도록 리다이렉트(Redirect)한다
 */
	UFUNCTION(BlueprintPure, Category = Equipment, meta = (DeterminesOutputType = PawnType))
	APawn* GetTypedPawn(TSubclassOf<APawn> PawnType) const;




	void SpawnEquipmentActors(const TArray<FLyraCloneEquipmentActorToSpawn>& ActorsToSpawn);
	void DestroyEquipmentActors();


	/** 어떤 InventoryItemInstance에 의해 활성화되었는지 (추후, QuickBarComponent에서 보게 될것이다) */
	UPROPERTY()
	TObjectPtr<UObject> Instigator;

	/** HakEquipementDefinition에 맞게 Spawn된 Actor Instance들 */
	UPROPERTY()
	TArray<TObjectPtr<AActor>> SpawnedActors;
};
