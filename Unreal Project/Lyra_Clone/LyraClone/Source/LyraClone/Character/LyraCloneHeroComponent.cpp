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

#include "AbilitySystemGlobals.h"
#include <LyraClone\Equipment\LyraCloneQuickBarComponent.h>

/** FeatureName 정의: static member variable 초기화 */
const FName ULyraCloneHeroComponent::NAME_ActorFeatureName("Hero");

/** InputConfig의 GameFeatureAction 활성화 ExtensioEvent 이름 */
const FName ULyraCloneHeroComponent::NAME_BindInputsNow("BindInputsNow");


ULyraCloneHeroComponent::ULyraCloneHeroComponent(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
	PrimaryComponentTick.bStartWithTickEnabled = false;
	PrimaryComponentTick.bCanEverTick = false;
}

void ULyraCloneHeroComponent::OnRegister()
{
	Super::OnRegister();

	// 올바른 Actor에 등록되었는지 확인:
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

	// PawnExtensionComponent에 대해서 (PawnExtension Feature) OnActorInitStateChanged() 관찰하도록 (Observing)
	BindOnActorInitStateChanged(ULyraClonePawnExtensionComponent::NAME_ActorFeatureName, FGameplayTag(), false);

	// InitState_Spawned로 초기화
	ensure(TryToChangeInitState(FHakGameplayTags::Get().InitState_Spawned));

	// ForceUpdate 진행
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
		// HakPawnExtensionComponent의 DataInitialized 상태 변화 관찰 후, HakHeroComponent도 DataInitialized 상태로 변경
		// - CanChangeInitState 확인
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

	// Spawned 초기화
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
		// PawnExtensionComponent가 DataInitialized될 때까지 기다림 (== 모든 Feature Component가 DataAvailable인 상태)
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

	// DataAvailable -> DataInitialized 단계
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

			//DataInitialized 단계까지 오면, Pawn이 Controller에 possess되어 준비된 상태이다 : 
			// - InitAbilityActorInfo 호출로 AvatarActor 재설정이 필요하다.
			PawnExtComp->InitializeAbilitySystem(HakPS->GetHakAbilitySystemComponent(), HakPS);
		}

		if (bIsLocallyControlled && PawnData)
		{
			//// 현재 HakCharacter에 Attach된 CameraComponent를 찾음
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
	// 앞서 BindOnActorInitStateChanged에서 보았듯이 Hero Feature는 Pawn Extension Feature에 종속되어 있으므로, CheckDefaultInitializationForImplementers 호출하지 않음:

	// ContinueInitStateChain은 앞서 PawnExtComponent와 같음
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

	// LocalPlayer를 가져오기 위해
	const APlayerController* PC = GetController<APlayerController>();
	check(PC);

	// EnhancedInputLocalPlayerSubsystem 가져오기 위해
	const ULocalPlayer* LP = PC->GetLocalPlayer();
	check(LP);

	UEnhancedInputLocalPlayerSubsystem* Subsystem = LP->GetSubsystem<UEnhancedInputLocalPlayerSubsystem>();
	check(Subsystem);

	// EnhancedInputLocalPlayerSubsystem에 MappingContext를 비워준다:
	Subsystem->ClearAllMappings();

	// PawnExtensionComponent -> PawnData -> InputConfig 존재 유무 판단:
	if (const ULyraClonePawnExtensionComponent* PawnExtComp = ULyraClonePawnExtensionComponent::FindPawnExtensionComponent(Pawn))
	{
		if (const ULyraClonePawnData* PawnData = PawnExtComp->GetPawnData<ULyraClonePawnData>())
		{
			if (const ULyraCloneInputConfig* InputConfig = PawnData->InputConfig)
			{
				const FHakGameplayTags& GameplayTags = FHakGameplayTags::Get();

				// HeroComponent 가지고 있는 Input Mapping Context를 순회하며, EnhancedInputLocalPlayerSubsystem에 추가한다
				for (const FLyraCloneMappableConfigPair& Pair : DefaultInputConfigs)
				{
					if (Pair.bShouldActivateAutomatically)
					{
						FModifyContextOptions Options = {};
						Options.bIgnoreAllPressedKeysUntilRelease = false;

						// 내부적으로 Input Mapping Context를 추가한다:
						// - AddPlayerMappableConfig를 간단히 보는 것을 추천
						Subsystem->AddPlayerMappableConfig(Pair.Config.LoadSynchronous(), Options);
					}
				}

				ULyraCloneInputComponent* HakIC = CastChecked<ULyraCloneInputComponent>(PlayerInputComponent);
				{
					// InputTag_Move와 InputTag_Look_Mouse에 대해 각각 Input_Move()와 Input_LookMouse() 멤버 함수에 바인딩시킨다:
					// - 바인딩한 이후, Input 이벤트에 따라 멤버 함수가 트리거된다

					{
						TArray<uint32> BindHandles;
						HakIC->BindAbilityActions(InputConfig, this, &ThisClass::Input_AbilityInputTagPressed, &ThisClass::Input_AbilityInputTagReleased, BindHandles);
					}

					HakIC->BindNativeAction(InputConfig, GameplayTags.InputTag_Move, ETriggerEvent::Triggered, this, &ThisClass::Input_Move, false);
					HakIC->BindNativeAction(InputConfig, GameplayTags.InputTag_Look_Mouse, ETriggerEvent::Triggered, this, &ThisClass::Input_LookMouse, false);


					HakIC->BindNativeAction(InputConfig, GameplayTags.InputTag_QuickSlot1, ETriggerEvent::Started, this, &ThisClass::Input_QuickSlot1, false);
					HakIC->BindNativeAction(InputConfig, GameplayTags.InputTag_QuickSlot2, ETriggerEvent::Started, this, &ThisClass::Input_QuickSlot2, false);
					HakIC->BindNativeAction(InputConfig, GameplayTags.InputTag_QuickSlot3, ETriggerEvent::Started, this, &ThisClass::Input_QuickSlot3, false);
					HakIC->BindNativeAction(InputConfig, GameplayTags.InputTag_DropWeapon, ETriggerEvent::Started, this, &ThisClass::Input_DropWeapon, false);

				}
			}
		}
	}

	// GameFeatureAction_AddInputConfig의 HandlePawnExtension 콜백 함수 전달
	UGameFrameworkComponentManager::SendGameFrameworkComponentExtensionEvent(const_cast<APawn*>(Pawn), NAME_BindInputsNow);
}

void ULyraCloneHeroComponent::Input_Move(const FInputActionValue& InputActionValue)
{
	APawn* Pawn = GetPawn<APawn>();
	AController* Controller = Pawn ? Pawn->GetController() : nullptr;

	FGameplayTag TAG_BlockMove = FGameplayTag::RequestGameplayTag(TEXT("State.Movement.Blocked"));


	if (UAbilitySystemComponent* ASC = UAbilitySystemGlobals::GetAbilitySystemComponentFromActor(Pawn))
	{
		if (ASC->HasMatchingGameplayTag(TAG_BlockMove))
		{
			return;
		}
	} 


	if (Controller)
	{
		const FVector2D Value = InputActionValue.Get<FVector2D>();
		const FRotator MovementRotation(0.0f, Controller->GetControlRotation().Yaw, 0.0f);

		if (Value.X != 0.0f)
		{
			// Left/Right -> X 값에 들어있음:
			// MovementDirection은 현재 카메라의 RightVector를 의미함 (World-Space)
			const FVector MovementDirection = MovementRotation.RotateVector(FVector::RightVector);

			// AddMovementInput 함수를 한번 보자:
			// - 내부적으로 MovementDirection * Value.X를 MovementComponent에 적용(더하기)해준다
			Pawn->AddMovementInput(MovementDirection, Value.X);
		}

		if (Value.Y != 0.0f) // 앞서 우리는 Forward 적용을 위해 swizzle input modifier를 사용했다~
		{
			// 앞서 Left/Right와 마찬가지로 Forward/Backward를 적용한다
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
		// X에는 Yaw 값이 있음:
		// - Camera에 대해 Yaw 적용
		Pawn->AddControllerYawInput(Value.X);
	}

	if (Value.Y != 0.0f)
	{
		// Y에는 Pitch 값!
		double AimInversionValue = -Value.Y;
		Pawn->AddControllerPitchInput(AimInversionValue);
	}
}

void ULyraCloneHeroComponent::Input_QuickSlot1(const FInputActionValue& InputActionValue) { HandleQuickSlotInput(0); }
void ULyraCloneHeroComponent::Input_QuickSlot2(const FInputActionValue& InputActionValue) { HandleQuickSlotInput(1); }
void ULyraCloneHeroComponent::Input_QuickSlot3(const FInputActionValue& InputActionValue) { HandleQuickSlotInput(2); }

void ULyraCloneHeroComponent::Input_DropWeapon(const FInputActionValue& InputActionValue)
{
	if (ALyraClonePlayerController* HakPC = GetController<ALyraClonePlayerController>())
	{
		if (ULyraCloneQuickBarComponent* QuickBar = HakPC->FindComponentByClass<ULyraCloneQuickBarComponent>())
		{
			QuickBar->DropItemInActiveSlot();
		}
	}
}

void ULyraCloneHeroComponent::HandleQuickSlotInput(int32 SlotIndex)
{
	if (ALyraClonePlayerController* HakPC = GetController<ALyraClonePlayerController>())
	{
		if (ULyraCloneQuickBarComponent* QuickBar = HakPC->FindComponentByClass<ULyraCloneQuickBarComponent>())
		{
			QuickBar->SelectSlotByIndex(SlotIndex);
		}
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
