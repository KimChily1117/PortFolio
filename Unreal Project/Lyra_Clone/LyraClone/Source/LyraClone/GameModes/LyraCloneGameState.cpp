// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraCloneGameState.h"
#include "LyraClone/GameModes/ExperienceManagerComponent.h"


ALyraCloneGameState::ALyraCloneGameState()
{
	ExperienceManagerComponent = CreateDefaultSubobject<UExperienceManagerComponent>(TEXT("ExperienceManagerComponent"));

	// �����Ҵ��� ���ִ� ����̶� �����Ѵ�. make_shared���� ����
}
