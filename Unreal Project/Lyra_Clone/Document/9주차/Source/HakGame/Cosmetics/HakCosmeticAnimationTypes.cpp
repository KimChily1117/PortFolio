// Fill out your copyright notice in the Description page of Project Settings.

#include "HakCosmeticAnimationTypes.h"

TSubclassOf<UAnimInstance> FHakAnimLayerSelectionSet::SelectBestLayer(const FGameplayTagContainer& CosmeticTags) const
{
	for (const FHakAnimLayerSelectionEntry& Rule : LayerRules)
	{
		if ((Rule.Layer != nullptr) && CosmeticTags.HasAll(Rule.RequiredTags))
		{
			return Rule.Layer;
		}
	}
	return DefaultLayer;
}

USkeletalMesh* FHakAnimBodyStyleSelectionSet::SelectBestBodyStyle(const FGameplayTagContainer& CosmeticTags) const
{
	// MeshRule�� ��ȸ�ϸ�, CosmeticTags �䱸 ���ǿ� �´� MeshRule�� ã�� SkeletalMesh�� ��ȯ�Ѵ�
	for (const FHakAnimBodyStyleSelectionEntry& Rule : MeshRules)
	{
		if ((Rule.Mesh) && CosmeticTags.HasAll(Rule.RequiredTags))
		{
			return Rule.Mesh;
		}
	}

	return DefaultMesh;
}
