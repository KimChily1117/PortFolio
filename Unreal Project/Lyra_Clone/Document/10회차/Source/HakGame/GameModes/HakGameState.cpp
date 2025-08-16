// Fill out your copyright notice in the Description page of Project Settings.

#include "HakGameState.h"
#include "HakGame/GameModes/HakExperienceManagerComponent.h"

AHakGameState::AHakGameState()
{
	ExperienceManagerComponent = CreateDefaultSubobject<UHakExperienceManagerComponent>(TEXT("ExperienceManagerComponent"));
}
