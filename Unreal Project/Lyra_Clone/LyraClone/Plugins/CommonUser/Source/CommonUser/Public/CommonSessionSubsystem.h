// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "CommonSessionSubsystem.generated.h"


/**
 * UCommonSession_HostSessionRequest를 만들어 CommonSessionSubsystem 전달하면, CommonSessionSubsystem에서 MapID와 CmdArgs로 최종적인 CmdArgs를 만들어 맵 로딩 진행
 */
UCLASS(BlueprintType)
class COMMONUSER_API UCommonSession_HostSessionRequest : public UObject
{
	GENERATED_BODY()
public:
	/** MapID -> Text 변환 */
	FString GetMapName() const;

	/** ServerTravel에 전달할 최종 URL을 생성한다 */
	FString ConstructTravelURL() const;

	/** 준비할 MapID (맵 경로) */
	UPROPERTY(BlueprintReadWrite, Category = Session)
	FPrimaryAssetId MapID;

	/** 전달할 CmdArgs (Experience 이름을 전달함) -> 곧 UserFacingExperienceDefinition에서 보게 될 것임 */
	UPROPERTY(BlueprintReadWrite, Category = Session)
	TMap<FString, FString> ExtraArgs;
};



/**
 * 
 */
UCLASS()
class COMMONUSER_API UCommonSessionSubsystem : public UGameInstanceSubsystem
{
	GENERATED_BODY()
public:
	UCommonSessionSubsystem() {}

	UFUNCTION(BlueprintCallable, Category = Session)
	void HostSession(APlayerController* HostingPlayer, UCommonSession_HostSessionRequest* Request);

	/**
	 * member variables
	 */
	 /** PendingTravelURL은 흔히 맵의 경로로 생각하면 된다 */
	FString PendingTravelURL;
};
