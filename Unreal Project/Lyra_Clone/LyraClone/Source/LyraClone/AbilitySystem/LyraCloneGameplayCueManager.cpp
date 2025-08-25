// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraCloneGameplayCueManager.h"
#include "AbilitySystemGlobals.h"
#include "GameplayCueSet.h"
#include "Engine/AssetManager.h"

ULyraCloneGameplayCueManager::ULyraCloneGameplayCueManager(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
}


ULyraCloneGameplayCueManager* ULyraCloneGameplayCueManager::Get()
{
	return Cast<ULyraCloneGameplayCueManager>(UAbilitySystemGlobals::Get().GetGameplayCueManager());
}

const FPrimaryAssetType UFortAssetManager_GameplayCueRefsType = TEXT("GameplayCueRefs");
const FName UFortAssetManager_GameplayCueRefsName = TEXT("GameplayCueReferences");
const FName UFortAssetManager_LoadStateClient = FName(TEXT("Client"));

void ULyraCloneGameplayCueManager::RefreshGameplayCuePrimaryAsset()
{
	TArray<FSoftObjectPath> CuePaths;
	UGameplayCueSet* RuntimeGameplayCueSet = GetRuntimeCueSet();
	if (RuntimeGameplayCueSet)
	{
		RuntimeGameplayCueSet->GetSoftObjectPaths(CuePaths);
	}

	// ���� �߰��� ������ ��θ� CuePaths�� �߰��Ͽ�!
	FAssetBundleData BundleData;
	BundleData.AddBundleAssetsTruncated(UFortAssetManager_LoadStateClient, CuePaths);

	// PrimaryAssetId�� ������ �̸����� �����Ͽ� (const�� �����Ǿ� ����)
	FPrimaryAssetId PrimaryAssetId = FPrimaryAssetId(UFortAssetManager_GameplayCueRefsType, UFortAssetManager_GameplayCueRefsName);

	// ���� �Ŵ����� �߰� ����
	UAssetManager::Get().AddDynamicAsset(PrimaryAssetId, FSoftObjectPath(), BundleData);
}