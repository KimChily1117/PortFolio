// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "HakExperienceDefinition.generated.h"

class UHakExperienceActionSet;
class UGameFeatureAction;
class UHakPawnData;

/**
 * 
 */
UCLASS()
class HAKGAME_API UHakExperienceDefinition : public UPrimaryDataAsset
{
	GENERATED_BODY()
public:
	UHakExperienceDefinition(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/**
	 * member variables
	 */
	UPROPERTY(EditDefaultsOnly, Category=Gameplay)
	TObjectPtr<UHakPawnData> DefaultPawnData;

	/**
	 * �ش� property�� �ܼ��� ��ŷ �� �������� ���ܵд�
	 * - ���� ��忡 ���� GameFeature plugin�� �ε��ϴµ� �̿� ���� ������� �����ϸ� �ȴ�
	 * - ���� ShooterCore ���� Plugin �۾��� ���, �����ϰ� �� �����̴�
	 */
	UPROPERTY(EditDefaultsOnly, Category=Gameplay)
	TArray<FString> GameFeaturesToEnable;

	/** ExperienceActionSet�� UGameFeatureAction�� Set�̸�, Gameplay �뵵�� �°� �з��� �������� ����Ѵ� */
	UPROPERTY(EditDefaultsOnly, Category = Gameplay)
	TArray<TObjectPtr<UHakExperienceActionSet>> ActionSets;

	/** �Ϲ����� GameFeatureAction���μ� �߰� */
	UPROPERTY(EditDefaultsOnly, Category = "Actions")
	TArray<TObjectPtr<UGameFeatureAction>> Actions;
};
