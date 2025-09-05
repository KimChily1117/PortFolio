// Fill out your copyright notice in the Description page of Project Settings.

#include "HakExperienceDefinition.h"
#include "GameFeatureAction.h"

UHakExperienceDefinition::UHakExperienceDefinition(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
	
}

#if WITH_EDITORONLY_DATA
void UHakExperienceDefinition::UpdateAssetBundleData()
{
	// �츮�� UpdateAssetBundleData() �ڵ带 �ѹ� �����Ѵ�
	Super::UpdateAssetBundleData();

	for (UGameFeatureAction* Action : Actions)
	{
		if (Action)
		{
			// AddAddditionalAssetBundleData()�� UGameFeatureAction�� �޼����̴� 
			// - �츮�� ���������� ȣ���� ���� AssetBundleData�� �߰����ش�
			Action->AddAdditionalAssetBundleData(AssetBundleData);
		}
	}
}
#endif