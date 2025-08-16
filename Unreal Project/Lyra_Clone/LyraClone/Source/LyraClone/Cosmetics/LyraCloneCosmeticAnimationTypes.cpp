// Fill out your copyright notice in the Description page of Project Settings.

#include "LyraCloneCosmeticAnimationTypes.h"

TSubclassOf<UAnimInstance> FLyraCloneAnimLayerSelectionSet::SelectBestLayer(const FGameplayTagContainer& CosmeticTags) const
{
	for (const FLyraCloneAnimLayerSelectionEntry& Rule : LayerRules)
	{
		if ((Rule.Layer != nullptr) && CosmeticTags.HasAll(Rule.RequiredTags))
		{
			return Rule.Layer;
		}
	}
	return DefaultLayer;
}

USkeletalMesh* FLyraCloneAnimBodyStyleSelectionSet::SelectBestBodyStyle(const FGameplayTagContainer& CosmeticTags) const
{
	// MeshRule�� ��ȸ�ϸ�, CosmeticTags �䱸 ���ǿ� �´� MeshRule�� ã�� SkeletalMesh�� ��ȯ�Ѵ�
	for (const FLyraCloneAnimBodyStyleSelectionEntry& Rule : MeshRules)
	{
		if ((Rule.Mesh) && CosmeticTags.HasAll(Rule.RequiredTags))
		{
			return Rule.Mesh;
		}
	}

	return DefaultMesh;
}
