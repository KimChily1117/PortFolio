// Fill out your copyright notice in the Description page of Project Settings.

#include "HakCharacter.h"
#include "HakPawnExtensionComponent.h"
#include "HakGame/AbilitySystem/HakAbilitySystemComponent.h"
#include "HakGame/Camera/HakCameraComponent.h"
#include "HakHealthComponent.h"

// Sets default values
AHakCharacter::AHakCharacter()
{
 	// Set this character to call Tick() every frame.  You can turn this off to improve performance if you don't need it.
	// Tick을 비활성화
	PrimaryActorTick.bCanEverTick = false;
	PrimaryActorTick.bStartWithTickEnabled = false;

	// PawnExtComponent 생성
	PawnExtComponent = CreateDefaultSubobject<UHakPawnExtensionComponent>(TEXT("PawnExtensionComponent"));
	{
		PawnExtComponent->OnAbilitySystemInitialized_RegisterAndCall(FSimpleMulticastDelegate::FDelegate::CreateUObject(this, &ThisClass::OnAbilitySystemInitialized));
		PawnExtComponent->OnAbilitySystemUninitialized_Register(FSimpleMulticastDelegate::FDelegate::CreateUObject(this, &ThisClass::OnAbilitySystemUninitialized));
	}

	// CameraComponent 생성
	{
		CameraComponent = CreateDefaultSubobject<UHakCameraComponent>(TEXT("CameraComponent"));
		CameraComponent->SetRelativeLocation(FVector(-300.0f, 0.0f, 75.0f));
	}

	// HealthComponent 생성
	{
		HealthComponent = CreateDefaultSubobject<UHakHealthComponent>(TEXT("HealthComponent"));
	}
}

UAbilitySystemComponent* AHakCharacter::GetAbilitySystemComponent() const
{
	// 앞서, 우리는 PawnExtensionComponent에 AbilitySystemComponent를 캐싱하였다
	return PawnExtComponent->GetHakAbilitySystemComponent();
}

// Called when the game starts or when spawned
void AHakCharacter::BeginPlay()
{
	Cast<UObject>(this);
	Super::BeginPlay();
}

// Called every frame
void AHakCharacter::Tick(float DeltaTime)
{
	Super::Tick(DeltaTime);

}

// Called to bind functionality to input
void AHakCharacter::SetupPlayerInputComponent(UInputComponent* PlayerInputComponent)
{
	Super::SetupPlayerInputComponent(PlayerInputComponent);

	// Pawn이 Possess로서, Controller와 PlayerState 정보 접근이 가능한 상태가 되었음:
	// - SetupPlayerInputComponent 확인
	PawnExtComponent->SetupPlayerInputComponent();
}


void AHakCharacter::OnAbilitySystemInitialized()
{
	UHakAbilitySystemComponent* HakASC = Cast<UHakAbilitySystemComponent>(GetAbilitySystemComponent());
	check(HakASC);

	// HealthComponent의 ASC를 통한 초기화
	HealthComponent->InitializeWithAbilitySystem(HakASC);
}

void AHakCharacter::OnAbilitySystemUninitialized()
{
	HealthComponent->UninitializeWithAbilitySystem();
}