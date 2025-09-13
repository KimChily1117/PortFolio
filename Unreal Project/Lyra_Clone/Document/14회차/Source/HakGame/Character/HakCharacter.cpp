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
	// Tick�� ��Ȱ��ȭ
	PrimaryActorTick.bCanEverTick = false;
	PrimaryActorTick.bStartWithTickEnabled = false;

	// PawnExtComponent ����
	PawnExtComponent = CreateDefaultSubobject<UHakPawnExtensionComponent>(TEXT("PawnExtensionComponent"));
	{
		PawnExtComponent->OnAbilitySystemInitialized_RegisterAndCall(FSimpleMulticastDelegate::FDelegate::CreateUObject(this, &ThisClass::OnAbilitySystemInitialized));
		PawnExtComponent->OnAbilitySystemUninitialized_Register(FSimpleMulticastDelegate::FDelegate::CreateUObject(this, &ThisClass::OnAbilitySystemUninitialized));
	}

	// CameraComponent ����
	{
		CameraComponent = CreateDefaultSubobject<UHakCameraComponent>(TEXT("CameraComponent"));
		CameraComponent->SetRelativeLocation(FVector(-300.0f, 0.0f, 75.0f));
	}

	// HealthComponent ����
	{
		HealthComponent = CreateDefaultSubobject<UHakHealthComponent>(TEXT("HealthComponent"));
	}
}

UAbilitySystemComponent* AHakCharacter::GetAbilitySystemComponent() const
{
	// �ռ�, �츮�� PawnExtensionComponent�� AbilitySystemComponent�� ĳ���Ͽ���
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

	// Pawn�� Possess�μ�, Controller�� PlayerState ���� ������ ������ ���°� �Ǿ���:
	// - SetupPlayerInputComponent Ȯ��
	PawnExtComponent->SetupPlayerInputComponent();
}


void AHakCharacter::OnAbilitySystemInitialized()
{
	UHakAbilitySystemComponent* HakASC = Cast<UHakAbilitySystemComponent>(GetAbilitySystemComponent());
	check(HakASC);

	// HealthComponent�� ASC�� ���� �ʱ�ȭ
	HealthComponent->InitializeWithAbilitySystem(HakASC);
}

void AHakCharacter::OnAbilitySystemUninitialized()
{
	HealthComponent->UninitializeWithAbilitySystem();
}