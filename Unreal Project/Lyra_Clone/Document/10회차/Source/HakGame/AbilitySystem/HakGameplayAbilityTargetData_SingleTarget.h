#pragma once

#include "Abilities/GameplayAbilityTargetTypes.h"
#include "HakGameplayAbilityTargetData_SingleTarget.generated.h"

USTRUCT()
struct FHakGameplayAbilityTargetData_SingleTargetHit : public FGameplayAbilityTargetData_SingleTargetHit
{
	GENERATED_BODY()
public:
	FHakGameplayAbilityTargetData_SingleTargetHit()
		: CartridgeID(-1)
	{}

	virtual UScriptStruct* GetScriptStruct() const override
	{
		return FHakGameplayAbilityTargetData_SingleTargetHit::StaticStruct();
	}

	/** ź�� ID (īƮ����) */
	UPROPERTY()
	int32 CartridgeID;
};