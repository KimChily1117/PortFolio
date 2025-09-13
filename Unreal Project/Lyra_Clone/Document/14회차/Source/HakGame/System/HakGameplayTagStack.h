#pragma once

#include "GameplayTagContainer.h"
#include "HakGameplayTagStack.generated.h"

/**
 * Represents one stack of a gameplay tag (tag + count)
 * : for example, Ammo is representative example for GameplayTagStack
 */
USTRUCT(BlueprintType)
struct FHakGameplayTagStack
{
	GENERATED_BODY()

	FHakGameplayTagStack() {}
	FHakGameplayTagStack(FGameplayTag InTag, int32 InStackCount)
		: Tag(InTag)
		, StackCount(InStackCount)
	{}

	UPROPERTY()
	FGameplayTag Tag;

	UPROPERTY()
	int32 StackCount = 0;
};

/** container of HakGameplayTagStack */
USTRUCT(BlueprintType)
struct FHakGameplayTagStackContainer
{
	GENERATED_BODY()

	FHakGameplayTagStackContainer() {}

	/** add/remove stack count by gameplay-tag */
	void AddStack(FGameplayTag Tag, int32 StackCount);
	void RemoveStack(FGameplayTag Tag, int32 StackCount);

	/** get the count by the gameplay tag */
	int32 GetStackCount(FGameplayTag Tag) const
	{
		return TagToCountMap.FindRef(Tag);
	}

	/** whether gameplay tag exists in HakGameplayTagStackContainer */
	bool ContainsTag(FGameplayTag Tag) const
	{
		return TagToCountMap.Contains(Tag);
	}

	/** a list of gameplay tag stacks */
	UPROPERTY()
	TArray<FHakGameplayTagStack> Stacks;

	/**
	 * LUT(Look-up table) to accelerate gameplay tag stack to query [GameplayTag -> Count]
	 * - we also use this LUT to find existance for corresponding gameplay tag
	 */
	TMap<FGameplayTag, int32> TagToCountMap;
};
