// Fill out your copyright notice in the Description page of Project Settings.

#include "HakCharacter.h"
#include "HakPawnExtensionComponent.h"
#include "HakGame/Camera/HakCameraComponent.h"

// Sets default values
AHakCharacter::AHakCharacter()
{
 	// Set this character to call Tick() every frame.  You can turn this off to improve performance if you don't need it.
	// Tick�� ��Ȱ��ȭ
	PrimaryActorTick.bCanEverTick = false;
	PrimaryActorTick.bStartWithTickEnabled = false;

	// PawnExtComponent ����
	PawnExtComponent = CreateDefaultSubobject<UHakPawnExtensionComponent>(TEXT("PawnExtensionComponent"));
	
	// CameraComponent ����
	{
		CameraComponent = CreateDefaultSubobject<UHakCameraComponent>(TEXT("CameraComponent"));
		CameraComponent->SetRelativeLocation(FVector(-300.0f, 0.0f, 75.0f));
	}
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

