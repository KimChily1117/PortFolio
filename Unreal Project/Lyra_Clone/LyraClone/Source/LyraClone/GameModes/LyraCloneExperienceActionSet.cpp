// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraCloneExperienceActionSet.h"
#include "GameFeatureAction.h"
#include UE_INLINE_GENERATED_CPP_BY_NAME(LyraCloneExperienceActionSet)

ULyraCloneExperienceActionSet::ULyraCloneExperienceActionSet() : Super()
{
}

#if WITH_EDITORONLY_DATA
void ULyraCloneExperienceActionSet::UpdateAssetBundleData()
{
	Super::UpdateAssetBundleData();

	//UGameFeatureAction*ÀÌ¶û °°À½

	for (UGameFeatureAction* Action : Actions)
	{
		if (Action)
		{
			Action->AddAdditionalAssetBundleData(AssetBundleData);
		}
	}
}
#endif