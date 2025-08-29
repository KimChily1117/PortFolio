// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "CommonGameInstance.h"
#include "Engine/GameInstance.h"
#include "LyraCloneGameInstance.generated.h"

/**
 * 
 */
UCLASS()
class LYRACLONE_API ULyraCloneGameInstance : public UCommonGameInstance
{
	GENERATED_BODY()	
	/**
	 * UGameInstance's interfaces
	 */
	virtual void Init() override;
	virtual void Shutdown() override;
};