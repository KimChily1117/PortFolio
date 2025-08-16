// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "LyraCloneUserFacingExperience.generated.h"

// forward declarations
class UCommonSession_HostSessionRequest;

/**
 * 이 클래스를 활용하여 aos 모드 라던지 fps모드라던지 GameMode를 여러개 사용하지않고. DA(DataAsset을 활용하여) 게임모드를 변경함.
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
