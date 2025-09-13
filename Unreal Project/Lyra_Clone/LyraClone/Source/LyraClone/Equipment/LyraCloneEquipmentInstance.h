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
	
	//BP���� ȣ�� �� Equip / unequip �Լ�

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
 * DeterminesOutputType�� C++ ���ǿ��� APawn* ��ȯ������, BP������ PawnType�� ���� OutputType�� �����ǵ��� �����̷�Ʈ(Redirect)�Ѵ�
 */
	UFUNCTION(BlueprintPure, Category = Equipment, meta = (DeterminesOutputType = PawnType))
	APawn* GetTypedPawn(TSubclassOf<APawn> PawnType) const;




	void SpawnEquipmentActors(const TArray<FLyraCloneEquipmentActorToSpawn>& ActorsToSpawn);
	void DestroyEquipmentActors();


	/** � InventoryItemInstance�� ���� Ȱ��ȭ�Ǿ����� (����, QuickBarComponent���� ���� �ɰ��̴�) */
	UPROPERTY()
	TObjectPtr<UObject> Instigator;

	/** HakEquipementDefinition�� �°� Spawn�� Actor Instance�� */
	UPROPERTY()
	TArray<TObjectPtr<AActor>> SpawnedActors;
};
