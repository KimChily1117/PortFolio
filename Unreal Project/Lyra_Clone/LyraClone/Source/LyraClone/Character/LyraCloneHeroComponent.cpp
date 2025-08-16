// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraCloneHeroComponent.h"
#include "LyraClonePawnData.h"
#include "LyraClonePawnExtensionComponent.h"
#include "PlayerMappableInputConfig.h"
#include "LyraCLone/Input/LyraCloneMappableConfigPair.h"
#include "LyraClone/Input/LyraCloneInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "Components/GameFrameworkComponentManager.h"
#include "LyraClone/FLyraCloneGameplayTags.h"
#include "LyraClone/LyraLogSystem.h"
#include "LyraClone/Player/LyraClonePlayerState.h"
#include "LyraClone/Player/LyraClonePlayerController.h"
#include "LyraClone/Camera/LyraCloneCameraComponent.h"
#include "LyraClone/AbilitySystem/LyraCloneAbilitySystemComponent.h"

/** FeatureName ����: static member variable �ʱ�ȭ */
const FName ULyraCloneHeroComponent::NAME_ActorFeatureName("Hero");

/** InputConfig�� GameFeatureAction Ȱ��ȭ ExtensioEvent �̸� */
const FName ULyraCloneHeroComponent::NAME_BindInputsNow("BindInputsNow");



ULyraCloneHeroComponent::ULyraCloneHeroComponent(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
	PrimaryComponentTick.bStartWithTickEnabled = false;
	PrimaryComponentTick.bCanEverTick = false;
}

void ULyraCloneHeroComponent::OnRegister()
{
	Super::OnRegister();

	// �ùٸ� Actor�� ��ϵǾ����� Ȯ��:
	{
		if (!GetPawn<APawn>())
		{
			UE_LOG(LogLyraClone, Error, TEXT("this component has been added to a BP whose base class is not a Pawn!"));
			return;
		}
	}

	RegisterInitStateFeature();
}

void ULyraCloneHeroComponent::BeginPlay()
{
	Super::BeginPlay();

	// PawnExtensionComponent�� ���ؼ� (PawnExtension Feature) OnActorInitStateChanged() �����ϵ��� (Observing)
	BindOnActorInitStateChanged(ULyraClonePawnExtensionComponent::NAME_ActorFeatureName, FGameplayTag(), false);

	// InitState_Spawned�� �ʱ�ȭ
	ensure(TryToChangeInitState(FHakGameplayTags::Get().InitState_Spawned));

	// ForceUpdate ����
	CheckDefaultInitialization();
	
}

void ULyraCloneHeroComponent::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	UnregisterInitStateFeature();

	Super::EndPlay(EndPlayReason);
}

void ULyraCloneHeroComponent::OnActorInitStateChanged(const FActorInitStateChangedParams& Params)
{
	const FHakGameplayTags& InitTags = FHakGameplayTags::Get();

	if (Params.FeatureName == ULyraClonePawnExtensionComponent::NAME_ActorFeatureName)
	{
		// HakPawnExtensionComponent�� DataInitialized ���� ��ȭ ���� ��, HakHeroComponent�� DataInitialized ���·� ����
		// - CanChangeInitState Ȯ��
		if (Params.FeatureState == InitTags.InitState_DataInitialized)
		{
			CheckDefaultInitialization();
		}
	}
}

bool ULyraCloneHeroComponent::CanChangeInitState(UGameFrameworkComponentManager* Manager, FGameplayTag CurrentState, FGameplayTag DesiredState) const
{
	check(Manager);

	const FHakGameplayTags& InitTags = FHakGameplayTags::Get();
	APawn* Pawn = GetPawn<APawn>();
	ALyraClonePlayerState* HakPS = GetPlayerState<ALyraClonePlayerState>();

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
		return HakPS && Manager->HasFeatureReachedInitState(Pawn, ULyraClonePawnExtensionComponent::NAME_ActorFeatureName, InitTags.InitState_DataInitialized);
	}

	// DataInitialized -> GameplayReady
	if (CurrentState == InitTags.InitState_DataInitialized && DesiredState == InitTags.InitState_GameplayReady)
	{
		return true;
	}

	return false;
}

void ULyraCloneHeroComponent::HandleChangeInitState(UGameFrameworkComponentManager* Manager, FGameplayTag CurrentState, FGameplayTag DesiredState)
{
	const FHakGameplayTags& InitTags = FHakGameplayTags::Get();

	// DataAvailable -> DataInitialized �ܰ�
	if (CurrentState == InitTags.InitState_DataAvailable && DesiredState == InitTags.InitState_DataInitialized)
	{
		APawn* Pawn = GetPawn<APawn>();
		ALyraClonePlayerState* HakPS = GetPlayerState<ALyraClonePlayerState>();
		if (!ensure(Pawn && HakPS))
		{
			return;
		}

	
		const bool bIsLocallyControlled = Pawn->IsLocallyControlled();
		const ULyraClonePawnData* PawnData = nullptr;

		if (ULyraClonePawnExtensionComponent* PawnExtComp = ULyraClonePawnExtensionComponent::FindPawnExtensionComponent(Pawn))
		{
			PawnData = PawnExtComp->GetPawnData<ULyraClonePawnData>();

			//DataInitialized �ܰ���� ����, Pawn�� Controller�� possess�Ǿ� �غ�� �����̴� : 
			// - InitAbilityActorInfo ȣ��� AvatarActor �缳���� �ʿ��ϴ�.
			PawnExtComp->InitializeAbilitySystem(HakPS->GetHakAbilitySystemComponent(), HakPS);
		}

		if (bIsLocallyControlled && PawnData)
		{
			//// ���� HakCharacter�� Attach�� CameraComponent�� ã��
			if (ULyraCloneCameraComponent* CameraComponent = ULyraCloneCameraComponent::FindCameraComponent(Pawn))
			{
				CameraComponent->DetermineCameraModeDelegate.BindUObject(this, &ThisClass::DetermineCameraMode);
			}
		}

		if (ALyraClonePlayerController* HakPC = GetController<ALyraClonePlayerController>())
		{
			if (Pawn->InputComponent != nullptr)
			{
				InitializePlayerInput(Pawn->InputComponent);
			}
		}
	}
}

void ULyraCloneHeroComponent::CheckDefaultInitialization()
{
	// �ռ� BindOnActorInitStateChanged���� ���ҵ��� Hero Feature�� Pawn Extension Feature�� ���ӵǾ� �����Ƿ�, CheckDefaultInitializationForImplementers ȣ������ ����:

	// ContinueInitStateChain�� �ռ� PawnExtComponent�� ����
	const FHakGameplayTags& InitTags = FHakGameplayTags::Get();
	static const TArray<FGameplayTag> StateChain = { InitTags.InitState_Spawned, InitTags.InitState_DataAvailable, InitTags.InitState_DataInitialized, InitTags.InitState_GameplayReady };
	ContinueInitStateChain(StateChain);
}

PRAGMA_DISABLE_OPTIMIZATION
TSubclassOf<ULyraCloneCameraMode> ULyraCloneHeroComponent::DetermineCameraMode() const
{
	const APawn* Pawn = GetPawn<APawn>();
	if (!Pawn)
	{
		return nullptr;
	}

	if (ULyraClonePawnExtensionComponent* PawnExtComp = ULyraClonePawnExtensionComponent::FindPawnExtensionComponent(Pawn))
	{
		if (const ULyraClonePawnData* PawnData = PawnExtComp->GetPawnData<ULyraClonePawnData>())
		{
			return PawnData->DefaultCameraMode;
		}
	}

	return nullptr;
}
PRAGMA_ENABLE_OPTIMIZATION



void ULyraCloneHeroComponent::InitializePlayerInput(UInputComponent* PlayerInputComponent)
{
	check(PlayerInputComponent);

	const APawn* Pawn = GetPawn<APawn>();
	if (!Pawn)
	{
		return;
	}

	// LocalPlayer�� �������� ����
	const APlayerController* PC = GetController<APlayerController>();
	check(PC);

	// EnhancedInputLocalPlayerSubsystem �������� ����
	const ULocalPlayer* LP = PC->GetLocalPlayer();
	check(LP);

	UEnhancedInputLocalPlayerSubsystem* Subsystem = LP->GetSubsystem<UEnhancedInputLocalPlayerSubsystem>();
	check(Subsystem);

	// EnhancedInputLocalPlayerSubsystem�� MappingContext�� ����ش�:
	Subsystem->ClearAllMappings();

	// PawnExtensionComponent -> PawnData -> InputConfig ���� ���� �Ǵ�:
	if (const ULyraClonePawnExtensionComponent* PawnExtComp = ULyraClonePawnExtensionComponent::FindPawnExtensionComponent(Pawn))
	{
		if (const ULyraClonePawnData* PawnData = PawnExtComp->GetPawnData<ULyraClonePawnData>())
		{
			if (const ULyraCloneInputConfig* InputConfig = PawnData->InputConfig)
			{
				const FHakGameplayTags& GameplayTags = FHakGameplayTags::Get();

				// HeroComponent ������ �ִ� Input Mapping Context�� ��ȸ�ϸ�, EnhancedInputLocalPlayerSubsystem�� �߰��Ѵ�
				for (const FLyraCloneMappableConfigPair& Pair : DefaultInputConfigs)
				{
					if (Pair.bShouldActivateAutomatically)
					{
						FModifyContextOptions Options = {};
						Options.bIgnoreAllPressedKeysUntilRelease = false;

						// ���������� Input Mapping Context�� �߰��Ѵ�:
						// - AddPlayerMappableConfig�� ������ ���� ���� ��õ
						Subsystem->AddPlayerMappableConfig(Pair.Config.LoadSynchronous(), Options);
					}
				}

				ULyraCloneInputComponent* HakIC = CastChecked<ULyraCloneInputComponent>(PlayerInputComponent);
				{
					// InputTag_Move�� InputTag_Look_Mouse�� ���� ���� Input_Move()�� Input_LookMouse() ��� �Լ��� ���ε���Ų��:
					// - ���ε��� ����, Input �̺�Ʈ�� ���� ��� �Լ��� Ʈ���ŵȴ�

					{
						TArray<uint32> BindHandles;
						HakIC->BindAbilityActions(InputConfig, this, &ThisClass::Input_AbilityInputTagPressed, &ThisClass::Input_AbilityInputTagReleased, BindHandles);
					}

					HakIC->BindNativeAction(InputConfig, GameplayTags.InputTag_Move, ETriggerEvent::Triggered, this, &ThisClass::Input_Move, false);
					HakIC->BindNativeAction(InputConfig, GameplayTags.InputTag_Look_Mouse, ETriggerEvent::Triggered, this, &ThisClass::Input_LookMouse, false);
				}
			}
		}
	}

	// GameFeatureAction_AddInputConfig�� HandlePawnExtension �ݹ� �Լ� ����
	UGameFrameworkComponentManager::SendGameFrameworkComponentExtensionEvent(const_cast<APawn*>(Pawn), NAME_BindInputsNow);
}

void ULyraCloneHeroComponent::Input_Move(const FInputActionValue& InputActionValue)
{
	APawn* Pawn = GetPawn<APawn>();
	AController* Controller = Pawn ? Pawn->GetController() : nullptr;

	if (Controller)
	{
		const FVector2D Value = InputActionValue.Get<FVector2D>();
		const FRotator MovementRotation(0.0f, Controller->GetControlRotation().Yaw, 0.0f);

		if (Value.X != 0.0f)
		{
			// Left/Right -> X ���� �������:
			// MovementDirection�� ���� ī�޶��� RightVector�� �ǹ��� (World-Space)
			const FVector MovementDirection = MovementRotation.RotateVector(FVector::RightVector);

			// AddMovementInput �Լ��� �ѹ� ����:
			// - ���������� MovementDirection * Value.X�� MovementComponent�� ����(���ϱ�)���ش�
			Pawn->AddMovementInput(MovementDirection, Value.X);
		}

		if (Value.Y != 0.0f) // �ռ� �츮�� Forward ������ ���� swizzle input modifier�� ����ߴ�~
		{
			// �ռ� Left/Right�� ���������� Forward/Backward�� �����Ѵ�
			const FVector MovementDirection = MovementRotation.RotateVector(FVector::ForwardVector);
			Pawn->AddMovementInput(MovementDirection, Value.Y);
		}
	}
}

void ULyraCloneHeroComponent::Input_LookMouse(const FInputActionValue& InputActionValue)
{
	APawn* Pawn = GetPawn<APawn>();
	if (!Pawn)
	{
		return;
	}

	const FVector2D Value = InputActionValue.Get<FVector2D>();
	if (Value.X != 0.0f)
	{
		// X���� Yaw ���� ����:
		// - Camera�� ���� Yaw ����
		Pawn->AddControllerYawInput(Value.X);
	}

	if (Value.Y != 0.0f)
	{
		// Y���� Pitch ��!
		double AimInversionValue = -Value.Y;
		Pawn->AddControllerPitchInput(AimInversionValue);
	}
}

void ULyraCloneHeroComponent::Input_AbilityInputTagPressed(FGameplayTag InputTag)
{
	if (const APawn* Pawn = GetPawn<APawn>())
	{
		if (const ULyraClonePawnExtensionComponent* PawnExtComp = ULyraClonePawnExtensionComponent::FindPawnExtensionComponent(Pawn))
		{
			if (ULyraCloneAbilitySystemComponent* HakASC = PawnExtComp->GetLyraCloneAbilitySystemComponent())
			{
				HakASC->AbilityInputTagPressed(InputTag);
			}
		}
	}
}

void ULyraCloneHeroComponent::Input_AbilityInputTagReleased(FGameplayTag InputTag)
{
	if (const APawn* Pawn = GetPawn<APawn>())
	{
		if (const ULyraClonePawnExtensionComponent* PawnExtComp = ULyraClonePawnExtensionComponent::FindPawnExtensionComponent(Pawn))
		{
			if (ULyraCloneAbilitySystemComponent* HakASC = PawnExtComp->GetLyraCloneAbilitySystemComponent())
			{
				HakASC->AbilityInputTagReleased(InputTag);
			}
		}
	}
}
