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
	/** ���⿡ ���յ� ReticleWidget ������ ������ �ִ� Fragment */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = Reticle)
	TArray<TSubclassOf<ULyraCloneReticleWidgetBase>> ReticleWidgets;
};