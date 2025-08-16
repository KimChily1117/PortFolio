// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "LyraCloneUserFacingExperience.generated.h"

// forward declarations
class UCommonSession_HostSessionRequest;

/**
 * �� Ŭ������ Ȱ���Ͽ� aos ��� ����� fps������� GameMode�� ������ ��������ʰ�. DA(DataAsset�� Ȱ���Ͽ�) ���Ӹ�带 ������.
 */
UCLASS()			
class LYRACLONE_API ULyraCloneUserFacingExperience : public UPrimaryDataAsset
{
	GENERATED_BODY()

public:

	UFUNCTION(BlueprintCallable, BlueprintPure = false)
	UCommonSession_HostSessionRequest* CreateHostingRequest() const;

	/** the specific map to load */
	UPROPERTY(BlueprintReadWrite, EditAnywhere, Category = Experience, meta = (AllowedTypes = "Map"))
	FPrimaryAssetId MapID;

	/** the gameplay expierence to load */ 
	UPROPERTY(BlueprintReadWrite, EditAnywhere, Category = Experience, meta = (AllowedTypes = "LyraCloneExperienceDefinition"))
	FPrimaryAssetId ExperienceID;
};
