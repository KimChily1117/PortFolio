// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "HakCharacterPartTypes.h"
#include "HakCosmeticAnimationTypes.h"
#include "Components/PawnComponent.h"
#include "HakPawnComponent_CharacterParts.generated.h"

class UHakPawnComponent_CharacterParts;

/** �ν��Ͻ�ȭ �� Character Part�� ���� */
USTRUCT()
struct FHakAppliedCharacterPartEntry
{
	GENERATED_BODY()

	/** Character Part�� ����(��Ÿ ������) */
	UPROPERTY()
	FHakCharacterPart Part;

	/** HakCharacterPartList���� �Ҵ� ���� Part �ڵ鰪 (FHakControllerCharacterPartEntry�� Handle ���� ���ƾ� �� -> ������ ���� Part) */
	UPROPERTY()
	int32 PartHandle = INDEX_NONE;

	/** �ν��Ͻ�ȭ �� Character Part�� Actor */
	UPROPERTY()
	TObjectPtr<UChildActorComponent> SpawnedComponent = nullptr;
};

/** HakPawnComponent_CharacterParts���� ������ Character Parts�� �����ϴ� Ŭ���� */
USTRUCT(BlueprintType)
struct FHakCharacterPartList
{
	GENERATED_BODY()

	FHakCharacterPartList()
		: OwnerComponent(nullptr)
	{}

	FHakCharacterPartList(UHakPawnComponent_CharacterParts* InOwnerComponent)
		: OwnerComponent(InOwnerComponent)
	{}

	bool SpawnActorForEntry(FHakAppliedCharacterPartEntry& Entry);
	void DestroyActorForEntry(FHakAppliedCharacterPartEntry& Entry);

	FHakCharacterPartHandle AddEntry(FHakCharacterPart NewPart);
	void RemoveEntry(FHakCharacterPartHandle Handle);

	FGameplayTagContainer CollectCombinedTags() const;

	/** ���� �ν��Ͻ�ȭ�� Character Part */
	UPROPERTY()
	TArray<FHakAppliedCharacterPartEntry> Entries;

	/** �ش� HakCharacterPartList�� Owner�� PawnComponent */
	UPROPERTY()
	TObjectPtr<UHakPawnComponent_CharacterParts> OwnerComponent;

	/** �ռ� ���Ҵ� PartHandle�� ���� �Ҵ� �� �����ϴ� ���� */
	int32 PartHandleCounter = 0;
};

/**
 * 
 */
UCLASS()
class HAKGAME_API UHakPawnComponent_CharacterParts : public UPawnComponent
{
	GENERATED_BODY()
public:
	UHakPawnComponent_CharacterParts(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	USkeletalMeshComponent* GetParentMeshComponent() const;
	USceneComponent* GetSceneComponentToAttachTo() const;
	FGameplayTagContainer GetCombinedTags(FGameplayTag RequiredPrefix) const;
	void BroadcastChanged();

	FHakCharacterPartHandle AddCharacterPart(const FHakCharacterPart& NewPart);
	void RemoveCharacterPart(FHakCharacterPartHandle Handle);

	/** �ν��Ͻ�ȭ �� Character Parts */
	UPROPERTY()
	FHakCharacterPartList CharacterPartList;

	/** �ִϸ��̼� ������ ���� �޽ÿ� ����� */
	UPROPERTY(EditAnywhere, Category = Cosmetics)
	FHakAnimBodyStyleSelectionSet BodyMeshes;
};
