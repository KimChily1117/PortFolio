// Fill out your copyright notice in the Description page of Project Settings.

#include "HakGameModeBase.h"
#include "HakExperienceDefinition.h"
#include "HakExperienceManagerComponent.h"
#include "HakGameState.h"
#include "HakGame/HakLogChannels.h"
#include "HakGame/Character/HakCharacter.h"
#include "HakGame/Player/HakPlayerController.h"
#include "HakGame/Player/HakPlayerState.h"
#include "HakGame/Character/HakPawnData.h"

AHakGameModeBase::AHakGameModeBase()
{
	GameStateClass = AHakGameState::StaticClass();
	PlayerControllerClass = AHakPlayerController::StaticClass();
	PlayerStateClass = AHakPlayerState::StaticClass();
	DefaultPawnClass = AHakCharacter::StaticClass();
}

void AHakGameModeBase::InitGame(const FString& MapName, const FString& Options, FString& ErrorMessage)
{
	Super::InitGame(MapName, Options, ErrorMessage);

	// ���� GameInstance�� ����, �ʱ�ȭ �۾��� ����ǹǷ�, �� �����ӿ��� Lyra�� Concept�� Experience ó���� ������ �� ����:
	// - �̸� ó���ϱ� ����, �������� �ڿ� �̺�Ʈ�� �޾� ó���� �̾ �����Ѵ�
	GetWorld()->GetTimerManager().SetTimerForNextTick(this, &ThisClass::HandleMatchAssignmentIfNotExpectingOne);
}

void AHakGameModeBase::InitGameState()
{
	Super::InitGameState();

	// Experience �񵿱� �ε��� ���� Delegate�� �غ��Ѵ�:
	UHakExperienceManagerComponent* ExperienceManagerComponent = GameState->FindComponentByClass<UHakExperienceManagerComponent>();
	check(ExperienceManagerComponent);

	// OnExperienceLoaded ���
	ExperienceManagerComponent->CallOrRegister_OnExperienceLoaded(FOnHakExperienceLoaded::FDelegate::CreateUObject(this, &ThisClass::OnExperienceLoaded));
}

UClass* AHakGameModeBase::GetDefaultPawnClassForController_Implementation(AController* InController)
{
	// GetPawnDataForController�� Ȱ���Ͽ�, PawnData�κ��� PawnClass�� ��������
	if (const UHakPawnData* PawnData = GetPawnDataForController(InController))
	{
		if (PawnData->PawnClass)
		{
			return PawnData->PawnClass;
		}
	}

	return Super::GetDefaultPawnClassForController_Implementation(InController);
}

PRAGMA_DISABLE_OPTIMIZATION
void AHakGameModeBase::HandleStartingNewPlayer_Implementation(APlayerController* NewPlayer)
{
	if (IsExperienceLoaded())
	{
		Super::HandleStartingNewPlayer_Implementation(NewPlayer);
	}
}
PRAGMA_ENABLE_OPTIMIZATION

APawn* AHakGameModeBase::SpawnDefaultPawnAtTransform_Implementation(AController* NewPlayer,const FTransform& SpawnTransform)
{
	UE_LOG(LogHak, Log, TEXT("SpawnDefaultPawnAtTransform_Implementation is called!"));
	return Super::SpawnDefaultPawnAtTransform_Implementation(NewPlayer, SpawnTransform);
}

void AHakGameModeBase::HandleMatchAssignmentIfNotExpectingOne()
{
	// �ش� �Լ������� �츮�� �ε��� Experience�� ���� PrimaryAssetId�� �����Ͽ�, OnMatchAssignmentGiven���� �Ѱ��ش�

	FPrimaryAssetId ExperienceId;

	// precedence order (highest wins)
	// - matchmaking assignment (if present)
	// - default experience

	UWorld* World = GetWorld();

	// fall back to the default experience
	// �ϴ� �⺻ �ɼ����� default�ϰ� B_HakDefaultExperience�� ��������
	if (!ExperienceId.IsValid())
	{
		ExperienceId = FPrimaryAssetId(FPrimaryAssetType("HakExperienceDefinition"), FName("B_HakDefaultExperience"));
	}

	// ���ڰ� ������ HandleMatchAssignmentIfNotExpectingOne�� OnMatchAssignmentGiven()�� ���� ���������� �̸��� �ʹ��� �ʴ´ٰ� �����Ѵ�
	// - ����, ������� Lyra�� �����Ǹ�, �ش� �Լ��� ���� �� ������ �� ���� ������ �����Ѵ�
	OnMatchAssignmentGiven(ExperienceId);
}

void AHakGameModeBase::OnMatchAssignmentGiven(FPrimaryAssetId ExperienceId)
{
	// �ش� �Լ��� ExperienceManagerComponent�� Ȱ���Ͽ� Experience�� �ε��ϱ� ����, ExperienceManagerComponent�� ServerSetCurrentExperience�� ȣ���Ѵ�

	check(ExperienceId.IsValid());

	UHakExperienceManagerComponent* ExperienceManagerComponent = GameState->FindComponentByClass<UHakExperienceManagerComponent>();
	check(ExperienceManagerComponent);
	ExperienceManagerComponent->ServerSetCurrentExperience(ExperienceId);
}

bool AHakGameModeBase::IsExperienceLoaded() const
{
	check(GameState);
	UHakExperienceManagerComponent* ExperienceManagerComponent = GameState->FindComponentByClass<UHakExperienceManagerComponent>();
	check(ExperienceManagerComponent);

	return ExperienceManagerComponent->IsExperienceLoaded();
}

void AHakGameModeBase::OnExperienceLoaded(const UHakExperienceDefinition* CurrentExperience)
{
	// PlayerController�� ��ȸ�ϸ�
	for (FConstPlayerControllerIterator Iterator = GetWorld()->GetPlayerControllerIterator(); Iterator; ++Iterator)
	{
		APlayerController* PC = Cast<APlayerController>(*Iterator);

		// PlayerController�� Pawn�� Possess���� �ʾҴٸ�, RestartPlayer�� ���� Pawn�� �ٽ� Spawn�Ѵ�
		// - �ѹ� OnPossess�� ������ ����:
		if (PC && PC->GetPawn() == nullptr)
		{
			if (PlayerCanRestart(PC))
			{
				RestartPlayer(PC);
			}
		}
	}
}

const UHakPawnData* AHakGameModeBase::GetPawnDataForController(const AController* InController) const
{
	// ���� ���߿� PawnData�� �������̵� �Ǿ��� ���, PawnData�� PlayerState���� �������� ��
	if (InController)
	{
		if (const AHakPlayerState* HakPS = InController->GetPlayerState<AHakPlayerState>())
		{
			// GetPawnData ����
			if (const UHakPawnData* PawnData = HakPS->GetPawnData<UHakPawnData>())
			{
				return PawnData;
			}
		}
	}

	// fall back to the default for the current experience
	// ���� PlayerState�� PawnData�� �����Ǿ� ���� ���� ���, ExperienceManagerComponent�� CurrentExperience�κ��� �����ͼ� ����
	check(GameState);
	UHakExperienceManagerComponent* ExperienceManagerComponent = GameState->FindComponentByClass<UHakExperienceManagerComponent>();
	check(ExperienceManagerComponent);

	if (ExperienceManagerComponent->IsExperienceLoaded())
	{
		// GetExperienceChecked ����
		const UHakExperienceDefinition* Experience = ExperienceManagerComponent->GetCurrentExperienceChecked();
		if (Experience->DefaultPawnData)
		{
			return Experience->DefaultPawnData;
		}
	}

	// ��� ���̽����� �ڵ鸵 �ȵǾ����� nullptr
	return nullptr;
}
