// Fill out your copyright notice in the Description page of Project Settings.

#include "HakHeroComponent.h"
#include "HakPawnData.h"
#include "HakPawnExtensionComponent.h"
#include "Components/GameFrameworkComponentManager.h"
#include "HakGame/HakGameplayTags.h"
#include "HakGame/HakLogChannels.h"
#include "HakGame/Camera/HakCameraComponent.h"
#include "HakGame/Player/HakPlayerState.h"

/** FeatureName ����: static member variable �ʱ�ȭ */
const FName UHakHeroComponent::NAME_ActorFeatureName("Hero");

UHakHeroComponent::UHakHeroComponent(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
	PrimaryComponentTick.bStartWithTickEnabled = false;
	PrimaryComponentTick.bCanEverTick = false;
}

void UHakHeroComponent::OnRegister()
{
	Super::OnRegister();

	// �ùٸ� Actor�� ��ϵǾ����� Ȯ��:
	{
		if (!GetPawn<APawn>())
		{
			UE_LOG(LogHak, Error, TEXT("this component has been added to a BP whose base class is not a Pawn!"));
			return;
		}
	}

	RegisterInitStateFeature();
}

void UHakHeroComponent::BeginPlay()
{
	Super::BeginPlay();

	// PawnExtensionComponent�� ���ؼ� (PawnExtension Feature) OnActorInitStateChanged() �����ϵ��� (Observing)
	BindOnActorInitStateChanged(UHakPawnExtensionComponent::NAME_ActorFeatureName, FGameplayTag(), false);

	// InitState_Spawned�� �ʱ�ȭ
	ensure(TryToChangeInitState(FHakGameplayTags::Get().InitState_Spawned));

	// ForceUpdate ����
	CheckDefaultInitialization();
}

void UHakHeroComponent::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	UnregisterInitStateFeature();

	Super::EndPlay(EndPlayReason);
}

void UHakHeroComponent::OnActorInitStateChanged(const FActorInitStateChangedParams& Params)
{
	const FHakGameplayTags& InitTags = FHakGameplayTags::Get();

	if (Params.FeatureName == UHakPawnExtensionComponent::NAME_ActorFeatureName)
	{
		// HakPawnExtensionComponent�� DataInitialized ���� ��ȭ ���� ��, HakHeroComponent�� DataInitialized ���·� ����
		// - CanChangeInitState Ȯ��
		if (Params.FeatureState == InitTags.InitState_DataInitialized)
		{
			CheckDefaultInitialization();
		}
	}
}

bool UHakHeroComponent::CanChangeInitState(UGameFrameworkComponentManager* Manager, FGameplayTag CurrentState,FGameplayTag DesiredState) const
{
	check(Manager);

	const FHakGameplayTags& InitTags = FHakGameplayTags::Get();
	APawn* Pawn = GetPawn<APawn>();
	AHakPlayerState* HakPS = GetPlayerState<AHakPlayerState>();

	// Spawned �ʱ�ȭ
	if (!CurrentState.IsValid() && DesiredState == InitTags.InitState_Spawned)
	{
		if (Pawn)
		{
			return true;
		}
	}

	// Spawned -> DataAvailable
	if (CurrentState == InitTags.InitState_Spawned && DesiredState == InitTags.InitState_DataAvailable)
	{
		if (!HakPS)
		{
			return false;
		}

		return true;
	}

	// DataAvailable -> DataInitialized
	if (CurrentState == InitTags.InitState_DataAvailable && DesiredState == InitTags.InitState_DataInitialized)
	{
		// PawnExtensionComponent�� DataInitialized�� ������ ��ٸ� (== ��� Feature Component�� DataAvailable�� ����)
		return HakPS && Manager->HasFeatureReachedInitState(Pawn, UHakPawnExtensionComponent::NAME_ActorFeatureName, InitTags.InitState_DataInitialized);
	}

	// DataInitialized -> GameplayReady
	if (CurrentState == InitTags.InitState_DataInitialized && DesiredState == InitTags.InitState_GameplayReady)
	{
		return true;
	}

	return false;
}

void UHakHeroComponent::HandleChangeInitState(UGameFrameworkComponentManager* Manager, FGameplayTag CurrentState,FGameplayTag DesiredState)
{
	const FHakGameplayTags& InitTags = FHakGameplayTags::Get();

	// DataAvailable -> DataInitialized �ܰ�
	if (CurrentState == InitTags.InitState_DataAvailable && DesiredState == InitTags.InitState_DataInitialized)
	{
		APawn* Pawn = GetPawn<APawn>();
		AHakPlayerState* HakPS = GetPlayerState<AHakPlayerState>();
		if (!ensure(Pawn && HakPS))
		{
			return;
		}

		// Input�� Camera�� ���� �ڵ鸵... (TODO)

		const bool bIsLocallyControlled = Pawn->IsLocallyControlled();
		const UHakPawnData* PawnData = nullptr;
		if (UHakPawnExtensionComponent* PawnExtComp = UHakPawnExtensionComponent::FindPawnExtensionComponent(Pawn))
		{
			PawnData = PawnExtComp->GetPawnData<UHakPawnData>();
		}

		if (bIsLocallyControlled && PawnData)
		{
			// ���� HakCharacter�� Attach�� CameraComponent�� ã��
			if (UHakCameraComponent* CameraComponent = UHakCameraComponent::FindCameraComponent(Pawn))
			{
				CameraComponent->DetermineCameraModeDelegate.BindUObject(this, &ThisClass::DetermineCameraMode);
			}
		}
	}
}

void UHakHeroComponent::CheckDefaultInitialization()
{
	// �ռ� BindOnActorInitStateChanged���� ���ҵ��� Hero Feature�� Pawn Extension Feature�� ���ӵǾ� �����Ƿ�, CheckDefaultInitializationForImplementers ȣ������ ����:

	// ContinueInitStateChain�� �ռ� PawnExtComponent�� ����
	const FHakGameplayTags& InitTags = FHakGameplayTags::Get();
	static const TArray<FGameplayTag> StateChain = { InitTags.InitState_Spawned, InitTags.InitState_DataAvailable, InitTags.InitState_DataInitialized, InitTags.InitState_GameplayReady };
	ContinueInitStateChain(StateChain);
}

PRAGMA_DISABLE_OPTIMIZATION
TSubclassOf<UHakCameraMode> UHakHeroComponent::DetermineCameraMode() const
{
	const APawn* Pawn = GetPawn<APawn>();
	if (!Pawn)
	{
		return nullptr;
	}

	if (UHakPawnExtensionComponent* PawnExtComp = UHakPawnExtensionComponent::FindPawnExtensionComponent(Pawn))
	{
		if (const UHakPawnData* PawnData = PawnExtComp->GetPawnData<UHakPawnData>())
		{
			return PawnData->DefaultCameraMode;
		}
	}

	return nullptr;
}
PRAGMA_ENABLE_OPTIMIZATION