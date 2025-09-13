#include "HakHUD.h"
#include "Components/GameFrameworkComponentManager.h"
#include UE_INLINE_GENERATED_CPP_BY_NAME(HakHUD)

AHakHUD::AHakHUD(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer)
{}

void AHakHUD::PreInitializeComponents()
{
	Super::PreInitializeComponents();

	// HakHUD�� Receiver�� Actor�� �߰�����
	UGameFrameworkComponentManager::AddGameFrameworkComponentReceiver(this);
}

void AHakHUD::BeginPlay()
{
	UGameFrameworkComponentManager::SendGameFrameworkComponentExtensionEvent(this, UGameFrameworkComponentManager::NAME_GameActorReady);
	Super::BeginPlay();
}

void AHakHUD::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	UGameFrameworkComponentManager::RemoveGameFrameworkComponentReceiver(this);
	Super::EndPlay(EndPlayReason);
}
