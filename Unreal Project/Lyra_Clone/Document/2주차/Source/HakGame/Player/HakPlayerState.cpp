// Fill out your copyright notice in the Description page of Project Settings.


#include "HakPlayerState.h"
#include "HakGame/GameModes/HakExperienceManagerComponent.h"

void AHakPlayerState::PostInitializeComponents()
{
	Super::PostInitializeComponents();

	const AGameStateBase* GameState = GetWorld()->GetGameState();
	check(GameState);

	UHakExperienceManagerComponent* ExperienceManagerComponent = GameState->FindComponentByClass<UHakExperienceManagerComponent>();
	check(ExperienceManagerComponent);

	ExperienceManagerComponent->CallOrRegister_OnExperienceLoaded(FOnHakExperienceLoaded::FDelegate::CreateUObject(this, &ThisClass::OnExperienceLoaded));
}

void AHakPlayerState::OnExperienceLoaded(const UHakExperienceDefinition* CurrentExperience)
{

}
