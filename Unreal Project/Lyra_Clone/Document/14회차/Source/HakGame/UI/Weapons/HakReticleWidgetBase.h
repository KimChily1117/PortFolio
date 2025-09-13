#pragma once

#include "CommonUserWidget.h"
#include "UObject/ObjectPtr.h"
#include "UObject/UObjectGlobals.h"
#include "HakReticleWidgetBase.generated.h"

/** forward declarations */
class UHakWeaponInstance;
class UHakInventoryItemInstance;

UCLASS(Abstract)
class UHakReticleWidgetBase : public UCommonUserWidget
{
	GENERATED_BODY()
public:
	UHakReticleWidgetBase(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	UFUNCTION(BlueprintCallable)
	void InitializeFromWeapon(UHakWeaponInstance* InWeapon);

	UFUNCTION(BlueprintImplementableEvent)
	void OnWeaponInitialized();

	/**
	 * WeaponInstance/InventoryInstance를 상태 추적용으로 캐싱 목적
	 */
	UPROPERTY(BlueprintReadOnly)
	TObjectPtr<UHakWeaponInstance> WeaponInstance;

	UPROPERTY(BlueprintReadOnly)
	TObjectPtr<UHakInventoryItemInstance> InventoryInstance;
};