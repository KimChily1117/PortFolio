// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraCloneCharacter.h"
#include "LyraClone/LyraLogSystem.h"
#include "GameFramework/PlayerState.h"
#include "GameFramework/Controller.h"
#include "LyraClonePawnExtensionComponent.h"
#include "LyraClone/AbilitySystem/LyraCloneAbilitySystemComponent.h"
#include "LyraClone/Camera/LyraCloneCameraComponent.h"
#include "LyraClone/Character/LyraCloneHealthComponent.h"
#include "LyraClone/Inventory/LyraCloneInventoryManagerComponent.h"
// Sets default values
ALyraCloneCharacter::ALyraCloneCharacter()
{
 	// Set this character to call Tick() every frame.  You can turn this off to improve performance if you don't need it.
	PrimaryActorTick.bCanEverTick = false;
	PrimaryActorTick.bStartWithTickEnabled = false;

	PawnExtComponent = CreateDefaultSubobject<ULyraClonePawnExtensionComponent>(TEXT("PawnExtensionComponent"));
	{
		PawnExtComponent->OnAbilitySystemInitialized_RegisterAndCall(FSimpleMulticastDelegate::FDelegate::CreateUObject(this, &ThisClass::OnAbilitySystemInitialized));
		PawnExtComponent->OnAbilitySystemUninitialized_Register(FSimpleMulticastDelegate::FDelegate::CreateUObject(this, &ThisClass::OnAbilitySystemUninitialized));
	}


	{
		CameraComponent = CreateDefaultSubobject<ULyraCloneCameraComponent>(TEXT("CameraComponent"));
		CameraComponent->SetRelativeLocation(FVector(-300.0f,0.0f,75.0f));
	}

	{
		HealthComponent = CreateDefaultSubobject<ULyraCloneHealthComponent>(TEXT("HealthComponent"));
	}

	{
		InventoryManagerComponent = CreateDefaultSubobject<ULyraCloneInventoryManagerComponent>(TEXT("InventoryManagerComponent"));
	}

}

void ALyraCloneCharacter::OnAbilitySystemInitialized()
{
	ULyraCloneAbilitySystemComponent* HakASC = Cast<ULyraCloneAbilitySystemComponent>(GetAbilitySystemComponent());
	check(HakASC);

	// HealthComponent의 ASC를 통한 초기화
	HealthComponent->InitializeWithAbilitySystem(HakASC);

}

void ALyraCloneCharacter::OnAbilitySystemUninitialized()
{
	HealthComponent->UninitializeWithAbilitySystem();
}

UAbilitySystemComponent* ALyraCloneCharacter::GetAbilitySystemComponent() const
{
	//UE_LOG(LogLyraClone, Warning, TEXT("[Char::GetASC] called. PawnExt=%s  PawnExtASC=%s"),
	//		*GetNameSafe(PawnExtComponent),
	//		*GetNameSafe(PawnExtComponent ? PawnExtComponent->GetLyraCloneAbilitySystemComponent() : nullptr));
	//// 앞서, 우리는 PawnExtensionComponent에 AbilitySystemComponent를 캐싱하였다
	//return PawnExtComponent->GetLyraCloneAbilitySystemComponent();

	auto* Asc = PawnExtComponent ? PawnExtComponent->GetLyraCloneAbilitySystemComponent() : nullptr;

	UE_LOG(LogLyraClone, Warning, TEXT("[Char::GetASC] %s  PawnExtASC=%s"),
		*GetNameSafe(this),
		*GetNameSafe(Asc));

	return Asc;
}

// Called when the game starts or when spawned
void ALyraCloneCharacter::BeginPlay()
{
	Super::BeginPlay();
	
}

// Called every frame
void ALyraCloneCharacter::Tick(float DeltaTime)
{
	Super::Tick(DeltaTime);

}

// Called to bind functionality to input
void ALyraCloneCharacter::SetupPlayerInputComponent(UInputComponent* PlayerInputComponent)
{
	Super::SetupPlayerInputComponent(PlayerInputComponent);

	// Pawn이 Possess로서, Controller와 PlayerState 정보 접근이 가능한 상태가 되었음:
	// - SetupPlayerInputComponent 확인
	PawnExtComponent->TryInitializeAbilitySystemFromPlayerState();

	PawnExtComponent->SetupPlayerInputComponent();
}

void ALyraCloneCharacter::PossessedBy(AController* NewController)
{
	Super::PossessedBy(NewController);

	if (PawnExtComponent)
	{
		PawnExtComponent->TryInitializeAbilitySystemFromPlayerState();
	}
}

void ALyraCloneCharacter::OnRep_PlayerState()
{
	Super::OnRep_PlayerState();

	if (PawnExtComponent)
	{
		PawnExtComponent->TryInitializeAbilitySystemFromPlayerState();
	}
}