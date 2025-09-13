// Fill out your copyright notice in the Description page of Project Settings.


#include "HakEquipmentInstance.h"
#include "HakEquipmentDefinition.h"
#include "GameFramework/Character.h"

UHakEquipmentInstance::UHakEquipmentInstance(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
}

APawn* UHakEquipmentInstance::GetPawn() const
{
	return Cast<APawn>(GetOuter());
}

void UHakEquipmentInstance::SpawnEquipmentActors(const TArray<FHakEquipmentActorToSpawn>& ActorsToSpawn)
{
	if (APawn* OwningPawn = GetPawn())
	{
		// ���� Owner�� Pawn�� RootComponent�� AttachTarget ������� �Ѵ�
		USceneComponent* AttachTarget = OwningPawn->GetRootComponent();
		if (ACharacter* Char = Cast<ACharacter>(OwningPawn))
		{
			// ���� ĳ���Ͷ��, SkeletalMeshComponent�� ������ GetMesh�� ��ȯ�Ͽ�, ���⿡ ���δ�
			AttachTarget = Char->GetMesh();
		}

		for (const FHakEquipmentActorToSpawn& SpawnInfo : ActorsToSpawn)
		{
			// SpawnActorDeferred�� FinishSpawning�� ȣ���ؾ� Spawn�� �ϼ��ȴ� (��, �ۼ��ڿ��� �ڵ�μ� Ownership�� �ִٴ� �ǹ�)
			AActor* NewActor = GetWorld()->SpawnActorDeferred<AActor>(SpawnInfo.ActorToSpawn, FTransform::Identity, OwningPawn);
			NewActor->FinishSpawning(FTransform::Identity, /*bIsDefaultTransform=*/true);

			// Actor�� RelativeTransform�� AttachTransform���� ����
			NewActor->SetActorRelativeTransform(SpawnInfo.AttachTransform);

			// AttachTarget�� ������ (Actor -> Actor)
			NewActor->AttachToComponent(AttachTarget, FAttachmentTransformRules::KeepRelativeTransform, SpawnInfo.AttachSocket);

			SpawnedActors.Add(NewActor);
		}
	}
}

void UHakEquipmentInstance::DestroyEquipmentActors()
{
	// ����� �������� �������� Actor Mesh�� �����Ǿ� ���� ���� �ִ�
	// - ���� Lv10�̾�����, ��ü�� ��ü�� ���� �����Ǿ����� ���� �����ϱ�?
	for (AActor* Actor : SpawnedActors)
	{
		if (Actor)
		{
			Actor->Destroy();
		}
	}
}

APawn* UHakEquipmentInstance::GetTypedPawn(TSubclassOf<APawn> PawnType) const
{
	APawn* Result = nullptr;
	if (UClass* ActualPawnType = PawnType)
	{
		if (GetOuter()->IsA(ActualPawnType))
		{
			Result = Cast<APawn>(GetOuter());
		}
	}
	return Result;
}

void UHakEquipmentInstance::OnEquipped()
{
	K2_OnEquipped();
}

void UHakEquipmentInstance::OnUnequipped()
{
	K2_OnUnequipped();
}
