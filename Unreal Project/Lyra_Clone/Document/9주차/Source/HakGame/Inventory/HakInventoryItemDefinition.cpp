// Fill out your copyright notice in the Description page of Project Settings.


#include "HakInventoryItemDefinition.h"

UHakInventoryItemDefinition::UHakInventoryItemDefinition(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
}

const UHakInventoryItemFragment* UHakInventoryItemDefinition::FindFragmentByClass(TSubclassOf<UHakInventoryItemFragment> FragmentClass) const
{
	if (FragmentClass)
	{
		// Fragments�� ��ȸ�ϸ�, IsA()�� ���� �ش� Ŭ������ ������ �ִ��� Ȯ���Ѵ�:
		for (UHakInventoryItemFragment* Fragment : Fragments)
		{
			if (Fragment && Fragment->IsA(FragmentClass))
			{
				return Fragment;
			}
		}
	}

	return nullptr;
}
