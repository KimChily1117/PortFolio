// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraClonePlayerState.h"
#include "Abilities/GameplayAbilityTypes.h"

#include "LyraClone/AbilitySystem/LyraCloneAbilitySystemComponent.h"
#include "LyraClone/AbilitySystem/LyraCloneAbilitySet.h"
#include "LyraClone/AbilitySystem/Abilities/LyraCloneGameplayAbility.h"

#include "LyraClone/Character/LyraClonePawnData.h"


#include "LyraClone/GameModes/ExperienceManagerComponent.h"
#include "LyraClone/GameModes/LyraCloneGameModeBase.h"


ALyraClonePlayerState::ALyraClonePlayerState(const FObjectInitializer& ObjectInitializer)
{
	AbilitySystemComponent = ObjectInitializer.CreateDefaultSubobject<ULyraCloneAbilitySystemComponent>(this, TEXT("AbilitySystemComponent"));
}

void ALyraClonePlayerState::PostInitializeComponents()
{
	Super::PostInitializeComponents();

	check(AbilitySystemComponent)
	// �Ʒ��� �ڵ�� �츮�� InitAbilityActorInfo�� ��ȣ���� ���ϴ� ������ �����ϴ� �ڵ��̴�:
	{
		// ó�� InitAbilityActorInfo�� ȣ�� ���, OwnerActor�� AvatarActo�� ���� Actor�� ����Ű�� ������, �̴� PlayerState�̴�
		// - OwnerActor�� PlayerState�� �ǵ��ϴ°� ������, AvatarActor�� PlayerController�� �����ϴ� ����� Pawn�� �Ǿ�� �Ѵ�!
		// - �̸� ���� ��-������ ���ش�
		FGameplayAbilityActorInfo* ActorInfo = AbilitySystemComponent->AbilityActorInfo.Get();
		check(ActorInfo->OwnerActor == this);
		check(ActorInfo->OwnerActor == ActorInfo->AvatarActor);
	}

	AbilitySystemComponent->InitAbilityActorInfo(this, GetPawn());


	const AGameStateBase* GameState = GetWorld()->GetGameState();
	check(GameState);

	UExperienceManagerComponent* ExperienceManagerComponent = GameState->FindComponentByClass<UExperienceManagerComponent>();
	check(ExperienceManagerComponent);

	ExperienceManagerComponent->CallOrRegister_OnExperienceLoaded(FOnLyraCloneExperienceLoaded::FDelegate::CreateUObject(this, &ThisClass::OnExperienceLoaded));
}

void ALyraClonePlayerState::SetPawnData(const ULyraClonePawnData* InPawnData)
{
	check(InPawnData);

	// PawnData�� �ι� �����Ǵ� ���� ������ ����!
	check(!PawnData);

	PawnData = InPawnData;
	// PawnData�� AbilitySet�� ��ȸ�ϸ�, ASC�� Ability�� �Ҵ�(Give)�Ѵ�
	// - �� �������� ASC�� ActivatableAbilities�� �߰��ȴ�
	for (ULyraCloneAbilitySet* AbilitySet : PawnData->AbilitySets)
	{
		if (AbilitySet)
		{
			AbilitySet->GiveToAbilitySystem(AbilitySystemComponent, nullptr);
		}
	}
}



void ALyraClonePlayerState::OnExperienceLoaded(const ULyraCloneExperienceDefinition* CurrentExperience)
{
	if (ALyraCloneGameModeBase* GameMode = GetWorld()->GetAuthGameMode<ALyraCloneGameModeBase>())
	{
		// AHakGameMode���� GetPawnDataForController�� �����ؾ� ��
		// - GetPawnDataForController���� �츮�� ���� PawnData�� �������� �ʾ����Ƿ�, ExperienceMangerComponent�� DefaultPawnData�� �����Ѵ�
		const ULyraClonePawnData* NewPawnData = GameMode->GetPawnDataForController(GetOwningController());
		check(NewPawnData);

		SetPawnData(NewPawnData);
	}
}

