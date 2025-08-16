// Fill out your copyright notice in the Description page of Project Settings.

#include "HakGameInstance.h"
#include "Components/GameFrameworkComponentManager.h"
#include "HakGame/HakGameplayTags.h"

void UHakGameInstance::Init()
{
	Super::Init(); 

	// 앞서 정의한 InitState의 GameplayTags 등록:
	UGameFrameworkComponentManager* ComponentManager = GetSubsystem<UGameFrameworkComponentManager>(this);
	if (ensure(ComponentManager))
	{
		const FHakGameplayTags& GameplayTags = FHakGameplayTags::Get();

		ComponentManager->RegisterInitState(GameplayTags.InitState_Spawned, false, FGameplayTag());
		ComponentManager->RegisterInitState(GameplayTags.InitState_DataAvailable, false, GameplayTags.InitState_Spawned);
		ComponentManager->RegisterInitState(GameplayTags.InitState_DataInitialized, false, GameplayTags.InitState_DataAvailable);
		ComponentManager->RegisterInitState(GameplayTags.InitState_GameplayReady, false, GameplayTags.InitState_DataInitialized);
	}
}

void UHakGameInstance::Shutdown()
{
	Super::Shutdown();
}
