#include "HakReticleWidgetBase.h"
#include "HakGame/Weapons/HakWeaponInstance.h"
#include "HakGame/Inventory/HakInventoryItemInstance.h"
#include UE_INLINE_GENERATED_CPP_BY_NAME(HakReticleWidgetBase)

UHakReticleWidgetBase::UHakReticleWidgetBase(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer)
{}

void UHakReticleWidgetBase::InitializeFromWeapon(UHakWeaponInstance* InWeapon)
{
	WeaponInstance = InWeapon;
	InventoryInstance = nullptr;
	if (WeaponInstance)
	{
		InventoryInstance = Cast<UHakInventoryItemInstance>(WeaponInstance->GetInstigator());
	}
}