#pragma once

#include "Containers/Array.h"
#include "Templates/SubclassOf.h"
#include "HakGame/Inventory/HakInventoryItemDefinition.h"
#include "InventoryFragment_ReticleConfig.generated.h"

/** forward declaration */
class UHakReticleWidgetBase;

UCLASS()
class UHakInventoryFragment_ReticleConfig : public UHakInventoryItemFragment
{
	GENERATED_BODY()
public:
	/** ���⿡ ���յ� ReticleWidget ������ ������ �ִ� Fragment */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = Reticle)
	TArray<TSubclassOf<UHakReticleWidgetBase>> ReticleWidgets;
};