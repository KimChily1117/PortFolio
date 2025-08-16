// Copyright Epic Games, Inc. All Rights Reserved.

#include "HakGame.h"
#include "HakLogChannels.h"
#include "Modules/ModuleManager.h"

class FHakGameModule : public FDefaultGameModuleImpl
{
public:
	virtual void StartupModule() override;
	virtual void ShutdownModule() override;
};

void FHakGameModule::StartupModule()
{
	FDefaultGameModuleImpl::StartupModule();
	UE_LOG(LogHak, Warning, TEXT("FHakGameModule"));
}

void FHakGameModule::ShutdownModule()
{
	FDefaultGameModuleImpl::ShutdownModule();
}

IMPLEMENT_PRIMARY_GAME_MODULE(FHakGameModule, HakGame, "HakGame");
