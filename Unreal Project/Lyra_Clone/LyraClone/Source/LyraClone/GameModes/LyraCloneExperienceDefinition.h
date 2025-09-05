// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "LyraCloneExperienceDefinition.generated.h"

// forward declarations
class ULyraClonePawnData;
class ULyraCloneExperienceActionSet;
class UGameFeatureAction;

/**
 * 
 */
UCLASS()
class LYRACLONE_API ULyraCloneExperienceDefinition : public UPrimaryDataAsset
{
	GENERATED_BODY()
public:
	ULyraCloneExperienceDefinition(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

#if WITH_EDITORONLY_DATA
	virtual void UpdateAssetBundleData() override;
#endif

	/**
	 * member variables
	 */
	UPROPERTY(EditDefaultsOnly, Category = Gameplay)
	TObjectPtr<ULyraClonePawnData> DefaultPawnData;

	/**
	 * �ش� property�� �ܼ��� ��ŷ �� �������� ���ܵд�
	 * - ���� ��忡 ���� GameFeature plugin�� �ε��ϴµ� �̿� ���� ������� �����ϸ� �ȴ�
	 * - ���� ShooterCore ���� Plugin �۾��� ���, �����ϰ� �� �����̴�
	 */
	UPROPERTY(EditDefaultsOnly, Category = Gameplay)
	TArray<FString> GameFeaturesToEnable;


	/** ExperienceActionSet�� UGameFeatureAction�� Set�̸�, Gameplay �뵵�� �°� �з��� �������� ����Ѵ� */
	UPROPERTY(EditDefaultsOnly, Category = Gameplay)
	TArray<TObjectPtr<ULyraCloneExperienceActionSet>> ActionSets;

	/** �Ϲ����� GameFeatureAction���μ� �߰� */
	UPROPERTY(EditDefaultsOnly, Instanced, Category = "Actions")
	TArray<TObjectPtr<UGameFeatureAction>> Actions;

};
