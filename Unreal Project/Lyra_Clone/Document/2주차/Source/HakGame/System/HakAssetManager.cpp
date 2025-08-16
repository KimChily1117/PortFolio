// Fill out your copyright notice in the Description page of Project Settings.

#include "HakAssetManager.h"
#include "HakGame/HakLogChannels.h"

UHakAssetManager::UHakAssetManager()
{
}

UHakAssetManager& UHakAssetManager::Get()
{
	check(GEngine);

	// �츮�� AssetManager�� UEngine�� GEngine�� AssetManager�� Class�� �������̵� �߱� ������, GEngine�� Asset Manager�� ����
	if (UHakAssetManager* Singleton = Cast<UHakAssetManager>(GEngine->AssetManager))
	{
		return *Singleton;
	}

	UE_LOG(LogHak, Fatal, TEXT("invalid AssetManagerClassname in DefaultEngine.ini(project settings); it must be HakAssetManager"));

	// ���� UE_LOG�� Fatal�� ����, Crash ���� ������ �������� ������ �������� ���� ���̷� ����
	return *NewObject<UHakAssetManager>();
}

PRAGMA_DISABLE_OPTIMIZATION
void UHakAssetManager::StartInitialLoading()
{
	Super::StartInitialLoading();
}
PRAGMA_ENABLE_OPTIMIZATION

bool UHakAssetManager::ShouldLogAssetLoads()
{
	const TCHAR* CommandLineContent = FCommandLine::Get();
	static bool bLogAssetLoads = FParse::Param(CommandLineContent, TEXT("LogAssetLoads"));
	return bLogAssetLoads;
}

UObject* UHakAssetManager::SynchronousLoadAsset(const FSoftObjectPath& AssetPath)
{
	// �ش� �Լ��� ���� ���� ������ 'synchronous load asset�� ���ʿ��ϰ� �ϴ� ���� ������ Ȯ���ϱ� ����'
	if (AssetPath.IsValid())
	{
		// FScopeLogTime�� Ȯ���غ���:
		TUniquePtr<FScopeLogTime> LogTimePtr;
		if (ShouldLogAssetLoads())
		{
			// �ܼ��� �α��ϸ鼭, �ʴ����� �α� ����
			LogTimePtr = MakeUnique<FScopeLogTime>(*FString::Printf(TEXT("synchronous loaded assets [%s]"), *AssetPath.ToString()), nullptr, FScopeLogTime::ScopeLog_Seconds);
		}

		// ���⼭ �ΰ��� �б�:
		// 1. AssetManager�� ������, AssetManager�� StreamableManager�� ���� ���� �ε�
		// 2. �ƴϸ�, FSoftObjectPath�� ���� �ٷ� ���� �ε�
		if (UAssetManager::IsValid())
		{
			return UAssetManager::GetStreamableManager().LoadSynchronous(AssetPath);
		}

		// if asset manager is not ready, use LoadObject()
		// - �� ����, StaticLoadObject�� ���δ�: 
		// - �����, �׻� StaticLoadObject�ϱ� ���� StaticFindObject�� ���� Ȯ���ϰ� �����ϸ� ��¥ �ε���
		return AssetPath.TryLoad();
	}

	return nullptr;
}

void UHakAssetManager::AddLoadedAsset(const UObject* Asset)
{
	if (ensureAlways(Asset))
	{
		FScopeLock Lock(&SyncObject);
		LoadedAssets.Add(Asset);
	}
}


