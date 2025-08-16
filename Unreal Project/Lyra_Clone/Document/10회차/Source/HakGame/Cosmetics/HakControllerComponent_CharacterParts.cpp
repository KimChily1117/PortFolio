// Fill out your copyright notice in the Description page of Project Settings.

#include "HakControllerComponent_CharacterParts.h"
#include "HakPawnComponent_CharacterParts.h"

UHakControllerComponent_CharacterParts::UHakControllerComponent_CharacterParts(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
}

void UHakControllerComponent_CharacterParts::BeginPlay()
{
	Super::BeginPlay();

	if (HasAuthority())
	{
		if (AController* OwningController = GetController<AController>())
		{
			OwningController->OnPossessedPawnChanged.AddDynamic(this, &ThisClass::OnPossessedPawnChanged);
		}
	}
}

void UHakControllerComponent_CharacterParts::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	RemoveAllCharacterParts();
	Super::EndPlay(EndPlayReason);
}

PRAGMA_DISABLE_OPTIMIZATION
UHakPawnComponent_CharacterParts* UHakControllerComponent_CharacterParts::GetPawnCustomizer() const
{
	if (APawn* ControlledPawn = GetPawn<APawn>())
	{
		// �����غ���, �츮�� �ռ� HakPawnComponent_CharacterParts�� ��ӹ޴� B_MannequinPawnCosmetics�� �̹� B_Hero_ShooterMannequin�� �߰��Ͽ���.
		// B_MannequinPawnCosmetics�� ��ȯ�Ǳ� ����Ѵ�
		return ControlledPawn->FindComponentByClass<UHakPawnComponent_CharacterParts>();
	}
	return nullptr;
}
PRAGMA_ENABLE_OPTIMIZATION

void UHakControllerComponent_CharacterParts::AddCharacterPart(const FHakCharacterPart& NewPart)
{
	AddCharacterPartInternal(NewPart);
}

void UHakControllerComponent_CharacterParts::AddCharacterPartInternal(const FHakCharacterPart& NewPart)
{
	FHakControllerCharacterPartEntry& NewEntry = CharacterParts.AddDefaulted_GetRef();
	NewEntry.Part = NewPart;

	if (UHakPawnComponent_CharacterParts* PawnCustomizer = GetPawnCustomizer())
	{
		NewEntry.Handle = PawnCustomizer->AddCharacterPart(NewPart);
	}
}

void UHakControllerComponent_CharacterParts::RemoveAllCharacterParts()
{
	if (UHakPawnComponent_CharacterParts* PawnCustomizer = GetPawnCustomizer())
	{
		for (FHakControllerCharacterPartEntry& Entry : CharacterParts)
		{
			PawnCustomizer->RemoveCharacterPart(Entry.Handle);
		}
	}
	CharacterParts.Reset();
}

void UHakControllerComponent_CharacterParts::OnPossessedPawnChanged(APawn* OldPawn, APawn* NewPawn)
{
	// ���� OldPawn�� ���ؼ��� Character Parts�� ���� ����������
	if (UHakPawnComponent_CharacterParts* OldCustomizer = OldPawn ? OldPawn->FindComponentByClass<UHakPawnComponent_CharacterParts>() : nullptr)
	{
		for (FHakControllerCharacterPartEntry& Entry : CharacterParts)
		{
			OldCustomizer->RemoveCharacterPart(Entry.Handle);
			Entry.Handle.Reset();
		}
	}

	// ���ο� Pawn�� ���ؼ� ���� Controller�� ������ �ִ� Character Parts�� �߰�������
	if (UHakPawnComponent_CharacterParts* NewCustomizer = NewPawn ? NewPawn->FindComponentByClass<UHakPawnComponent_CharacterParts>() : nullptr)
	{
		for (FHakControllerCharacterPartEntry& Entry : CharacterParts)
		{
			check(!Entry.Handle.IsValid());
			Entry.Handle = NewCustomizer->AddCharacterPart(Entry.Part);
		}
	}
}
