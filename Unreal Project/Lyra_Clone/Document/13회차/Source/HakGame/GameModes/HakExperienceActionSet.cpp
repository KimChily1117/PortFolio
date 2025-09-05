// Fill out your copyright notice in the Description page of Project Settings.


#include "HakExperienceActionSet.h"
#include "GameFeatureAction.h"

UHakExperienceActionSet::UHakExperienceActionSet()
{
}

#if WITH_EDITORONLY_DATA
void UHakExperienceActionSet::UpdateAssetBundleData()
{
	Super::UpdateAssetBundleData();

	for (UGameFeatureAction* Action : Actions)
	{
		if (Action)
		{
			Action->AddAdditionalAssetBundleData(AssetBundleData);
		}
	}
}
#endif