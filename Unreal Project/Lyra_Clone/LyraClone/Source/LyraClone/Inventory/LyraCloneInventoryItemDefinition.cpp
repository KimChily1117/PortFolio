// Fill out your copyright notice in the Description page of Project Settings.
#include "LyraCloneInventoryItemDefinition.h"

ULyraCloneInventoryItemDefinition::ULyraCloneInventoryItemDefinition(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
}

const ULyraCloneInventoryItemFragment* ULyraCloneInventoryItemDefinition::FindFragmentByClass(TSubclassOf<ULyraCloneInventoryItemFragment> FragmentClass) const
{
	if (FragmentClass)
	{
		// Fragments�� ��ȸ�ϸ�, IsA()�� ���� �ش� Ŭ������ ������ �ִ��� Ȯ���Ѵ�:
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
