#pragma once

#include "HakInventoryItemDefinition.h"
#include "Templates/SubclassOf.h"
#include "HakInventoryFragment_EquippableItem.generated.h"

/** forward declaration */
class UHakEquipmentDefinition;

UCLASS()
class UHakInventoryFragment_EquippableItem : public UHakInventoryItemFragment
{
	GENERATED_BODY()
public:
	UPROPERTY(EditAnywhere, Category = Hak)
	TSubclassOf<UHakEquipmentDefinition> EquipmentDefinition;
};