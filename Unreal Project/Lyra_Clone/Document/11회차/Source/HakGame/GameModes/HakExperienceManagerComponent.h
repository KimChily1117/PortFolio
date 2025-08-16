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
 * - �� �״��, �ش� component�� game state�� owner�� �����鼭, experience�� ���� ������ ������ �ִ� component�̴�
 * - �Ӹ� �ƴ϶�, manager��� �ܾ ���ԵǾ� �ֵ���, experience �ε� ���� ������Ʈ �� �̺�Ʈ�� �����Ѵ�
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
	 * �Ʒ��� OnExperienceLoaded�� ���ε��ϰų�, �̹� Experience �ε��� �Ϸ�Ǿ��ٸ� �ٷ� ȣ����
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

	/** Experience�� �ε� ���¸� ����͸� */
	EHakExperienceLoadState LoadState = EHakExperienceLoadState::Unloaded;

	/** Experience �ε��� �Ϸ�� ����, Broadcasting Delegate */
	FOnHakExperienceLoaded OnExperienceLoaded;

	/** Ȱ��ȭ�� GameFeature Plugin�� */
	int32 NumGameFeaturePluginsLoading = 0;
	TArray<FString> GameFeaturePluginURLs;
};
