// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraCloneExperienceDefinition.h"
#include "LyraCloneExperienceActionSet.h"
#include "GameFeatureAction.h"

ULyraCloneExperienceDefinition::ULyraCloneExperienceDefinition(const FObjectInitializer& ObjectInitializer /*= FObjectInitializer::Get()*/)
	: Super(ObjectInitializer)
{

}

#if WITH_EDITORONLY_DATA
void ULyraCloneExperienceDefinition::UpdateAssetBundleData()
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