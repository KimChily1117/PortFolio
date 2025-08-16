// Fill out your copyright notice in the Description page of Project Settings.


#include "ExperienceManagerComponent.h"
#include "LyraCloneExperienceDefinition.h"
#include "GameFeaturesSubsystem.h"
#include "GameFeatureAction.h"
#include "GameFeaturesSubsystemSettings.h"
#include "LyraCloneExperienceActionSet.h"
#include "LyraClone/System/LyraCloneAssetManager.h"


#include UE_INLINE_GENERATED_CPP_BY_NAME(ExperienceManagerComponent)


void UExperienceManagerComponent::CallOrRegister_OnExperienceLoaded(FOnLyraCloneExperienceLoaded::FDelegate&& Delegate)
{
	// �� ���� add ���ϰ� �Լ��� �߰�������?

	// IsExperienceLoaded() ����
	if (IsExperienceLoaded())
	{
		Delegate.Execute(CurrentExperience);
	}
	else
	{

		// ������ ���̱� ���� ���� ���� ���� ���� ���� ���� �˻� �غ���


		/**
		 * �����, �����е��� Delegate ��ü�� �ڼ��� ���캸��, ���������� �ʿ��� �������� �޸� �Ҵ��س��´�:
		 * TArray<int> a = {1, 2, 3, 4};
		 * delegate_type delegate = [a](){
		 *	return a.Num();
		 * }
		 * a�� delegate_type ���ο� new�� �Ҵ�Ǿ� �ִ�. ���� ����� ���߱� ���� Move�� ���� �ϴ� ���� ���� ����!
		 */
		OnExperienceLoaded.Add(MoveTemp(Delegate));
	}

}

void UExperienceManagerComponent::ServerSetCurrentExperience(FPrimaryAssetId ExperienceId)
{
	ULyraCloneAssetManager& AssetManager = ULyraCloneAssetManager::Get();

	TSubclassOf<ULyraCloneExperienceDefinition> AssetClass;
	{
		FSoftObjectPath AssetPath = AssetManager.GetPrimaryAssetPath(ExperienceId);
		AssetClass = Cast<UClass>(AssetPath.TryLoad());
	}

	// �� CDO�� �������� �ɱ�?
	const ULyraCloneExperienceDefinition* Experience = GetDefault<ULyraCloneExperienceDefinition>(AssetClass);
	check(Experience != nullptr);
	check(CurrentExperience == nullptr);
	{
		// �׸��� CDO�� CurrentExperience�� �����Ѵ�!
		// � �ǵ��� �̷��� �ڵ带 �ۼ������� �ڵ带 �� �о��(StartExperienceLoad���� �о��) �ٽ� �����غ���:
		CurrentExperience = Experience;
	}

	StartExperienceLoad();
}

PRAGMA_DISABLE_OPTIMIZATION
void UExperienceManagerComponent::StartExperienceLoad()
{
	check(CurrentExperience);
	check(LoadState == ELyraCloneExperienceLoadState::Unloaded);

	LoadState = ELyraCloneExperienceLoadState::Loading;

	ULyraCloneAssetManager& AssetManager = ULyraCloneAssetManager::Get();

	// �̹� �ռ�, ServerSetCurrentExperience���� �츮�� ExperienceId�� �Ѱ��־��µ�, ���⼭ CDO�� Ȱ���Ͽ�, GetPrimaryAssetId�� 
	// �ε��� ������� �ִ´�!
	// - �� �̷��� �ϴ°ɱ�?
	// - GetPrimaryAssetId�� �� �� �ڼ�������:
	// - GetPrimaryAssetId�� ���캽���ν�, �Ʒ��� �ΰ����� �� �� �ִ�:
	//   1. �츮�� B_HakDefaultExperience�� BP�� ���� ����
	//   2. CDO�� �����ͼ�, GetPrimaryAssetId�� ȣ���� ����

	// �츮�� �ռ� �̹� CDO�� �ε��Ͽ�, CDO�� ������� �ʰ� CDO�� ����Ͽ� �ε��� ������ �����Ͽ�, BundleAssetList�� �߰����ش�!

	TSet<FPrimaryAssetId> BundleAssetList;
	BundleAssetList.Add(CurrentExperience->GetPrimaryAssetId());

	// load assets associated with the experience
	// �Ʒ��� �츮�� ���� GameFeature�� ����Ͽ�, Experience�� ���ε��� GameFeature Plugin�� �ε��� Bundle �̸��� �߰��Ѵ�:
	// - Bundle�̶�°� ���� �츮�� �ε��� ������ ī�װ� �̸��̶�� �����ϸ� �ȴ� (�ϴ� ������ �Ѿ�� ����, �� �ٷ� ���̴�!)
	TArray<FName> BundlesToLoad;
	{
		// ���⼭ �ָ��ؾ� �� �κ��� OwnerNetMode�� NM_Standalone�̸�? Client/Server �Ѵ� �ε��� �߰��ȴ�!
		const ENetMode OwnerNetMode = GetOwner()->GetNetMode();
		bool bLoadClient = GIsEditor || (OwnerNetMode != NM_DedicatedServer);
		bool bLoadServer = GIsEditor || (OwnerNetMode != NM_Client);
		if (bLoadClient)
		{
			BundlesToLoad.Add(UGameFeaturesSubsystemSettings::LoadStateClient);
		}
		if (bLoadServer)
		{
			BundlesToLoad.Add(UGameFeaturesSubsystemSettings::LoadStateServer);
		}
	}

	FStreamableDelegate OnAssetsLoadedDelegate = FStreamableDelegate::CreateUObject(this, &ThisClass::OnExperienceLoadComplete);

	// �Ʒ���, ���� Bundle�� �츮�� GameFeature�� �����ϸ鼭 �� ��� �˾ƺ���� �ϰ�, ������ �ռ� B_HakDefaultExperience�� �ε����ִ� �Լ��� ��������
	TSharedPtr<FStreamableHandle> Handle = AssetManager.ChangeBundleStateForPrimaryAssets(
		BundleAssetList.Array(),
		BundlesToLoad,
		{}, false, FStreamableDelegate(), FStreamableManager::AsyncLoadHighPriority);

	if (!Handle.IsValid() || Handle->HasLoadCompleted())
	{
		// �ε��� �Ϸ�Ǿ�����, ExecuteDelegate�� ���� OnAssetsLoadedDelegate�� ȣ������:
		// - �Ʒ��� �Լ��� Ȯ���غ���:
		FStreamableHandle::ExecuteDelegate(OnAssetsLoadedDelegate);
	}
	else
	{
		Handle->BindCompleteDelegate(OnAssetsLoadedDelegate);
		Handle->BindCancelDelegate(FStreamableDelegate::CreateLambda([OnAssetsLoadedDelegate]()
			{
				OnAssetsLoadedDelegate.ExecuteIfBound();
			}));
	}

	// FrameNumber�� �ָ��ؼ� ����
	static int32 StartExperienceLoad_FrameNumber = GFrameNumber;
}
PRAGMA_ENABLE_OPTIMIZATION

void UExperienceManagerComponent::OnExperienceLoadComplete()
{
	// FrameNumber�� �ָ��ؼ� ����
	static int32 OnExperienceLoadComplete_FrameNumber = GFrameNumber;

	check(LoadState == ELyraCloneExperienceLoadState::Loading);
	check(CurrentExperience);

	// ���� Ȱ��ȭ�� GameFeature Plugin�� URL�� Ŭ�������ش�
	GameFeaturePluginURLs.Reset();

	auto CollectGameFeaturePluginURLs = [This = this](const UPrimaryDataAsset* Context, const TArray<FString>& FeaturePluginList)
		{
			// FeaturePluginList�� ��ȸ�ϸ�, PluginURL�� ExperienceManagerComponent�� GameFeaturePluginURLS�� �߰����ش�
			for (const FString& PluginName : FeaturePluginList)
			{
				FString PluginURL;
				if (UGameFeaturesSubsystem::Get().GetPluginURLByName(PluginName, PluginURL))
				{
					This->GameFeaturePluginURLs.AddUnique(PluginURL);
				}
			}
		};

	// GameFeaturesToEnable�� �ִ� Plugin�� �ϴ� Ȱ��ȭ�� GameFeature Plugin �ĺ������� ���
	CollectGameFeaturePluginURLs(CurrentExperience, CurrentExperience->GameFeaturesToEnable);

	// GameFeaturePluginURLs�� ��ϵ� Plugin�� �ε� �� Ȱ��ȭ:
	NumGameFeaturePluginsLoading = GameFeaturePluginURLs.Num();
	if (NumGameFeaturePluginsLoading)
	{
		LoadState = ELyraCloneExperienceLoadState::LoadingGameFeatures;
		for (const FString& PluginURL : GameFeaturePluginURLs)
		{
			// �� Plugin�� �ε� �� Ȱ��ȭ ����, OnGameFeaturePluginLoadComplete �ݹ� �Լ� ���
			// �ش� �Լ��� ���캸���� ����
			UGameFeaturesSubsystem::Get().LoadAndActivateGameFeaturePlugin(PluginURL, FGameFeaturePluginLoadComplete::CreateUObject(this, &ThisClass::OnGameFeaturePluginLoadComplete));
		}
	}
	else
	{
		// �ش� �Լ��� �Ҹ��� ���� �ռ� ���Ҵ� StreamableDelegateDelayHelper�� ���� �Ҹ�
		OnExperienceFullLoadCompleted();
	}
}

void UExperienceManagerComponent::OnGameFeaturePluginLoadComplete(const UE::GameFeatures::FResult& Result)
{
	// �� GameFeature Plugin�� �ε��� ��, �ش� �Լ��� �ݹ����� �Ҹ���
	NumGameFeaturePluginsLoading--;
	if (NumGameFeaturePluginsLoading == 0)
	{
		// GameFeaturePlugin �ε��� �� ������ ���, ������� Loaded�μ�, OnExperienceFullLoadCompleted ȣ���Ѵ�
		// GameFeaturePlugin �ε��� Ȱ��ȭ�� �����ٸ�? UGameFeatureAction�� Ȱ��ȭ�ؾ߰��� (���ݸ� �ִٰ� ����)
		OnExperienceFullLoadCompleted();
	}
}

void UExperienceManagerComponent::OnExperienceFullLoadCompleted()
{
	check(LoadState != ELyraCloneExperienceLoadState::Loaded);

	// GameFeature Plugin�� �ε��� Ȱ��ȭ ����, GameFeature Action���� Ȱ��ȭ ��Ű��:
	{
		LoadState = ELyraCloneExperienceLoadState::ExecutingActions;

		// GameFeatureAction Ȱ��ȭ�� ���� Context �غ�
		FGameFeatureActivatingContext Context;
		{
			// ������ �ڵ��� �������ش�
			const FWorldContext* ExistingWorldContext = GEngine->GetWorldContextFromWorld(GetWorld());
			if (ExistingWorldContext)
			{
				Context.SetRequiredWorldContextHandle(ExistingWorldContext->ContextHandle);
			}
		}

		auto ActivateListOfActions = [&Context](const TArray<UGameFeatureAction*>& ActionList)
			{
				for (UGameFeatureAction* Action : ActionList)
				{
					// ��������� GameFeatureAction�� ���� Registering -> Loading -> Activating ������ ȣ���Ѵ�
					if (Action)
					{
						Action->OnGameFeatureRegistering();
						Action->OnGameFeatureLoading();
						Action->OnGameFeatureActivating(Context);
					}
				}
			};

		// 1. Experience�� Actions
		ActivateListOfActions(CurrentExperience->Actions);

		// 2. Experience�� ActionSets
		for (const TObjectPtr<ULyraCloneExperienceActionSet>& ActionSet : CurrentExperience->ActionSets)
		{
			ActivateListOfActions(ActionSet->Actions);
		}
	}


	LoadState = ELyraCloneExperienceLoadState::Loaded;
	OnExperienceLoaded.Broadcast(CurrentExperience);
	OnExperienceLoaded.Clear();
}

const ULyraCloneExperienceDefinition* UExperienceManagerComponent::GetCurrentExperienceChecked() const
{
	check(LoadState == ELyraCloneExperienceLoadState::Loaded);
	check(CurrentExperience != nullptr);
	return CurrentExperience;
}
