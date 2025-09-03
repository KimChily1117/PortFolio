#include "LyraCloneHUD.h"
#include "Components/GameFrameworkComponentManager.h"


ALyraCloneHUD::ALyraCloneHUD(const FObjectInitializer& obj) : Super(obj)
{
}

void ALyraCloneHUD::PreInitializeComponents()
{
	Super::PreInitializeComponents();


	UGameFrameworkComponentManager::AddGameFrameworkComponentReceiver(this);

}

void ALyraCloneHUD::BeginPlay()
{

	UGameFrameworkComponentManager::SendGameFrameworkComponentExtensionEvent(this,
		UGameFrameworkComponentManager::NAME_GameActorReady);

	Super::BeginPlay();

}

void ALyraCloneHUD::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	UGameFrameworkComponentManager::RemoveGameFrameworkComponentReceiver(this);
	Super::EndPlay(EndPlayReason);
}
