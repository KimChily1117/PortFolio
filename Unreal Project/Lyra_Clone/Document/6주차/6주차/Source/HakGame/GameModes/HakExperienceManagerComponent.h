// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Components/GameStateComponent.h"
#include "GameFeaturePluginOperationResult.h"
#include "HakExperienceManagerComponent.generated.h"

class UHakExperienceDefinition;

enum class EHakExperienceLoadState
{
	Unloaded,
	Loading,
	LoadingGameFeatures,
	ExecutingActions,
	Loaded,
	Deactivating,
};

DECLARE_MULTICAST_DELEGATE_OneParam(FOnHakExperienceLoaded, const UHakExperienceDefinition*);

/**
 * HakExperienceManagerComponent
 * - 말 그대로, 해당 component는 game state를 owner로 가지면서, experience의 상태 정보를 가지고 있는 component이다
 * - 뿐만 아니라, manager라는 단어가 포함되어 있듯이, experience 로딩 상태 업데이트 및 이벤트를 관리한다
 */
UCLASS()
class HAKGAME_API UHakExperienceManagerComponent : public UGameStateComponent
{
	GENERATED_BODY()

public:
	/**
	 * member methods
	 */
	bool IsExperienceLoaded() { return (LoadState == EHakExperienceLoadState::Loaded) && (CurrentExperience != nullptr); }

	/**
	 * 아래의 OnExperienceLoaded에 바인딩하거나, 이미 Experience 로딩이 완료되었다면 바로 호출함
	 */
	void CallOrRegister_OnExperienceLoaded(FOnHakExperienceLoaded::FDelegate&& Delegate);

	void ServerSetCurrentExperience(FPrimaryAssetId ExperienceId);
	void StartExperienceLoad();
	void OnExperienceLoadComplete();
	void OnGameFeaturePluginLoadComplete(const UE::GameFeatures::FResult& Result);
	void OnExperienceFullLoadCompleted();
	const UHakExperienceDefinition* GetCurrentExperienceChecked() const;

public:
	UPROPERTY()
	TObjectPtr<const UHakExperienceDefinition> CurrentExperience;

	/** Experience의 로딩 상태를 모니터링 */
	EHakExperienceLoadState LoadState = EHakExperienceLoadState::Unloaded;

	/** Experience 로딩이 완료된 이후, Broadcasting Delegate */
	FOnHakExperienceLoaded OnExperienceLoaded;

	/** 활성화된 GameFeature Plugin들 */
	int32 NumGameFeaturePluginsLoading = 0;
	TArray<FString> GameFeaturePluginURLs;
};
