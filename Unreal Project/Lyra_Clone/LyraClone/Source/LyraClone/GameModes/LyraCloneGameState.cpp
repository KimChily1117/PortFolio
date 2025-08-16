// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraCloneGameState.h"
#include "LyraClone/GameModes/ExperienceManagerComponent.h"


ALyraCloneGameState::ALyraCloneGameState()
{
	ExperienceManagerComponent = CreateDefaultSubobject<UExperienceManagerComponent>(TEXT("ExperienceManagerComponent"));

	// 동적할당을 해주는 모습이라 봐야한다. make_shared같은 느낌
}
