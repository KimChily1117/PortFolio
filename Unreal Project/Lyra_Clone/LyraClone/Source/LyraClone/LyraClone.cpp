// Copyright Epic Games, Inc. All Rights Reserved.

#include "LyraClone.h"
#include "Modules/ModuleManager.h"

class FLyraCloneModule : public FDefaultGameModuleImpl
{
public:
	virtual void StartupModule() override;
	virtual void ShutdownModule() override;
}; 

void FLyraCloneModule::StartupModule()
{
	FDefaultGameModuleImpl::StartupModule();
}

void FLyraCloneModule::ShutdownModule()
{
	FDefaultGameModuleImpl::ShutdownModule();
}

IMPLEMENT_PRIMARY_GAME_MODULE(FLyraCloneModule, LyraClone, "LyraClone" );
