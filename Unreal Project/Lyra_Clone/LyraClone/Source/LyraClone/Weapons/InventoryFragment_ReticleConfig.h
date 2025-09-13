#pragma once

#include "Containers/Array.h"
#include "Templates/SubclassOf.h"
#include "LyraClone/Inventory/LyraCloneInventoryItemDefinition.h"
#include "InventoryFragment_ReticleConfig.generated.h"

/** forward declaration */
class ULyraCloneReticleWidgetBase;

UCLASS()
class ULyraCloneInventoryFragment_ReticleConfig : public ULyraCloneInventoryItemFragment
{
	GENERATED_BODY()
public:
	/** 무기에 결합된 ReticleWidget 정보를 가지고 있는 Fragment */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = Reticle)
	TArray<TSubclassOf<ULyraCloneReticleWidgetBase>> ReticleWidgets;
};