// Fill out your copyright notice in the Description page of Project Settings.

#include "HakPlayerState.h"
#include "Abilities/GameplayAbilityTypes.h"
#include "HakGame/AbilitySystem/HakAbilitySet.h"
#include "HakGame/AbilitySystem/HakAbilitySystemComponent.h"
#include "HakGame/AbilitySystem/Attributes/HakCombatSet.h"
#include "HakGame/AbilitySystem/Attributes/HakHealthSet.h"
#include "HakGame/Character/HakPawnData.h"
#include "HakGame/GameModes/HakExperienceManagerComponent.h"
#include "HakGame/GameModes/HakGameModeBase.h"

AHakPlayerState::AHakPlayerState(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
	AbilitySystemComponent = ObjectInitializer.CreateDefaultSubobject<UHakAbilitySystemComponent>(this, TEXT("AbilitySystemComponent"));

	CreateDefaultSubobject<UHakHealthSet>(TEXT("HealthSet"));
	CreateDefaultSubobject<UHakCombatSet>(TEXT("CombatSet"));
}

void AHakPlayerState::PostInitializeComponents()
{
	Super::PostInitializeComponents();

	check(AbilitySystemComponent);
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

	UHakExperienceManagerComponent* ExperienceManagerComponent = GameState->FindComponentByClass<UHakExperienceManagerComponent>();
	check(ExperienceManagerComponent);

	ExperienceManagerComponent->CallOrRegister_OnExperienceLoaded(FOnHakExperienceLoaded::FDelegate::CreateUObject(this, &ThisClass::OnExperienceLoaded));
}

void AHakPlayerState::OnExperienceLoaded(const UHakExperienceDefinition* CurrentExperience)
{
	if (AHakGameModeBase* GameMode = GetWorld()->GetAuthGameMode<AHakGameModeBase>())
	{
		// AHakGameMode���� GetPawnDataForController�� �����ؾ� ��
		// - GetPawnDataForController���� �츮�� ���� PawnData�� �������� �ʾ����Ƿ�, ExperienceMangerComponent�� DefaultPawnData�� �����Ѵ�
		const UHakPawnData* NewPawnData = GameMode->GetPawnDataForController(GetOwningController());
		check(NewPawnData);

		SetPawnData(NewPawnData);
	}
}

void AHakPlayerState::SetPawnData(const UHakPawnData* InPawnData)
{
	check(InPawnData);

	// PawnData�� �ι� �����Ǵ� ���� ������ ����!
	check(!PawnData);

	PawnData = InPawnData;

	// PawnData�� AbilitySet�� ��ȸ�ϸ�, ASC�� Ability�� �Ҵ�(Give)�Ѵ�
	// - �� �������� ASC�� ActivatableAbilities�� �߰��ȴ�
	for (UHakAbilitySet* AbilitySet : PawnData->AbilitySets)
	{
		if (AbilitySet)
		{
			AbilitySet->GiveToAbilitySystem(AbilitySystemComponent, nullptr);
		}
	}
}
