// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraClonePlayerState.h"
#include "Abilities/GameplayAbilityTypes.h"

#include "LyraClone/AbilitySystem/LyraCloneAbilitySystemComponent.h"
#include "LyraClone/AbilitySystem/LyraCloneAbilitySet.h"
#include "LyraClone/AbilitySystem/Abilities/LyraCloneGameplayAbility.h"

#include "LyraClone/Character/LyraClonePawnData.h"


#include "LyraClone/GameModes/ExperienceManagerComponent.h"
#include "LyraClone/GameModes/LyraCloneGameModeBase.h"

#include "LyraClone/AbilitySystem/Attributes/LyraCloneHealthSet.h"
#include "LyraClone/AbilitySystem/Attributes/LyraCloneCombatSet.h"

ALyraClonePlayerState::ALyraClonePlayerState(const FObjectInitializer& ObjectInitializer)
{
	AbilitySystemComponent = ObjectInitializer.CreateDefaultSubobject<ULyraCloneAbilitySystemComponent>(this, TEXT("AbilitySystemComponent"));

	CreateDefaultSubobject<ULyraCloneHealthSet>(TEXT("HealthSet"));
	CreateDefaultSubobject<ULyraCloneCombatSet>(TEXT("CombatSet"));
}

void ALyraClonePlayerState::PostInitializeComponents()
{
	Super::PostInitializeComponents();

	check(AbilitySystemComponent)
	// 아래의 코드는 우리가 InitAbilityActorInfo를 재호출을 통하는 이유를 설명하는 코드이다:
	{
		// 처음 InitAbilityActorInfo를 호출 당시, OwnerActor와 AvatarActo가 같은 Actor를 가르키고 있으며, 이는 PlayerState이다
		// - OwnerActor는 PlayerState가 의도하는게 맞지만, AvatarActor는 PlayerController가 소유하는 대상인 Pawn이 되어야 한다!
		// - 이를 위해 재-세팅을 해준다
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

	// PawnData가 두번 설정되는 것은 원하지 않음!
	check(!PawnData);

	PawnData = InPawnData;
	// PawnData의 AbilitySet을 순회하며, ASC에 Ability를 할당(Give)한다
	// - 이 과정에서 ASC의 ActivatableAbilities에 추가된다
	for (ULyraCloneAbilitySet* AbilitySet : PawnData->AbilitySets)
	{
		if (AbilitySet)
		{
			AbilitySet->GiveToAbilitySystem(AbilitySystemComponent, nullptr);
		}
	}
}

UAbilitySystemComponent* ALyraClonePlayerState::GetAbilitySystemComponent() const
{
	return AbilitySystemComponent;;
}



void ALyraClonePlayerState::OnExperienceLoaded(const ULyraCloneExperienceDefinition* CurrentExperience)
{
	if (ALyraCloneGameModeBase* GameMode = GetWorld()->GetAuthGameMode<ALyraCloneGameModeBase>())
	{
		// AHakGameMode에서 GetPawnDataForController를 구현해야 함
		// - GetPawnDataForController에서 우리는 아직 PawnData를 설정하지 않았으므로, ExperienceMangerComponent의 DefaultPawnData로 설정한다
		const ULyraClonePawnData* NewPawnData = GameMode->GetPawnDataForController(GetOwningController());
		check(NewPawnData);

		SetPawnData(NewPawnData);
	}
}

