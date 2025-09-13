#include "LyraCloneReticleWidgetBase.h"
#include "LyraClone/Weapons/LyraCloneWeaponInstance.h"
#include "LyraClone/Inventory/LyraCloneInventoryItemInstance.h"
#include UE_INLINE_GENERATED_CPP_BY_NAME(LyraCloneReticleWidgetBase)

ULyraCloneReticleWidgetBase::ULyraCloneReticleWidgetBase(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer)
{}

void ULyraCloneReticleWidgetBase::InitializeFromWeapon(ULyraCloneWeaponInstance* InWeapon)
{
	WeaponInstance = InWeapon;
	InventoryInstance = nullptr;
	if (WeaponInstance)
	{
		InventoryInstance = Cast<ULyraCloneInventoryItemInstance>(WeaponInstance->GetInstigator());
	}

	OnWeaponInitialized();
}