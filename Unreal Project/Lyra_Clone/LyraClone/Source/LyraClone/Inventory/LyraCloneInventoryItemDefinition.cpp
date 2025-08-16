// Fill out your copyright notice in the Description page of Project Settings.
#include "LyraCloneInventoryItemDefinition.h"

ULyraCloneInventoryItemDefinition::ULyraCloneInventoryItemDefinition(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
}

const ULyraCloneInventoryItemFragment* ULyraCloneInventoryItemDefinition::FindFragmentByClass(TSubclassOf<ULyraCloneInventoryItemFragment> FragmentClass) const
{
	if (FragmentClass)
	{
		// Fragments를 순회하며, IsA()를 통해 해당 클래스를 가지고 있는지 확인한다:
		for (ULyraCloneInventoryItemFragment* Fragment : Fragments)
		{
			if (Fragment && Fragment->IsA(FragmentClass))
			{
				return Fragment;
			}
		}
	}

	return nullptr;
}
