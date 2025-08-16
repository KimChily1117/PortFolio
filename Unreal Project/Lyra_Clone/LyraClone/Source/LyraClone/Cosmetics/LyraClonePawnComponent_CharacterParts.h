// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "LyraCloneCharacterPartTypes.h"
#include "LyraCloneCosmeticAnimationTypes.h"
#include "Components/PawnComponent.h"
#include "LyraClonePawnComponent_CharacterParts.generated.h"

class ULyraClonePawnComponent_CharacterParts;

/** �ν��Ͻ�ȭ �� Character Part�� ���� */
USTRUCT()
struct FLyraCloneAppliedCharacterPartEntry
{
	GENERATED_BODY()

	/** Character Part�� ����(��Ÿ ������) */
	UPROPERTY()
	FLyraCloneCharacterPart Part;

	/** HakCharacterPartList���� �Ҵ� ���� Part �ڵ鰪 (FHakControllerCharacterPartEntry�� Handle ���� ���ƾ� �� -> ������ ���� Part) */
	UPROPERTY()
	int32 PartHandle = INDEX_NONE;

	/** �ν��Ͻ�ȭ �� Character Part�� Actor */
	UPROPERTY()
	TObjectPtr<UChildActorComponent> SpawnedComponent = nullptr;
};

/** HakPawnComponent_CharacterParts���� ������ Character Parts�� �����ϴ� Ŭ���� */
USTRUCT(BlueprintType)
struct FLyraCloneCharacterPartList
{
	GENERATED_BODY()

	FLyraCloneCharacterPartList()
		: OwnerComponent(nullptr)
	{}

	FLyraCloneCharacterPartList(ULyraClonePawnComponent_CharacterParts* InOwnerComponent)
		: OwnerComponent(InOwnerComponent)
	{}

	bool SpawnActorForEntry(FLyraCloneAppliedCharacterPartEntry& Entry);
	void DestroyActorForEntry(FLyraCloneAppliedCharacterPartEntry& Entry);

	FLyraCloneCharacterPartHandle AddEntry(FLyraCloneCharacterPart NewPart);
	void RemoveEntry(FLyraCloneCharacterPartHandle Handle);

	FGameplayTagContainer CollectCombinedTags() const;

	/** ���� �ν��Ͻ�ȭ�� Character Part */
	UPROPERTY()
	TArray<FLyraCloneAppliedCharacterPartEntry> Entries;

	/** �ش� HakCharacterPartList�� Owner�� PawnComponent */
	UPROPERTY()
	TObjectPtr<ULyraClonePawnComponent_CharacterParts> OwnerComponent;

	/** �ռ� ���Ҵ� PartHandle�� ���� �Ҵ� �� �����ϴ� ���� */
	int32 PartHandleCounter = 0;
};

/**
 * 
 */
UCLASS(meta=(BlueprintSpawnableComponent))
class LYRACLONE_API ULyraClonePawnComponent_CharacterParts : public UPawnComponent
{
	GENERATED_BODY()
public:
	ULyraClonePawnComponent_CharacterParts(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	USkeletalMeshComponent* GetParentMeshComponent() const;
	USceneComponent* GetSceneComponentToAttachTo() const;

	UFUNCTION(BlueprintCallable, BlueprintPure = false, Category = Cosmetics)
	FGameplayTagContainer GetCombinedTags(FGameplayTag RequiredPrefix) const;
	void BroadcastChanged();

	FLyraCloneCharacterPartHandle AddCharacterPart(const FLyraCloneCharacterPart& NewPart);
	void RemoveCharacterPart(FLyraCloneCharacterPartHandle Handle);

	/** �ν��Ͻ�ȭ �� Character Parts */
	UPROPERTY()
	FLyraCloneCharacterPartList CharacterPartList;

	/** �ִϸ��̼� ������ ���� �޽ÿ� ����� */
	UPROPERTY(EditAnywhere, Category = Cosmetics)
	FLyraCloneAnimBodyStyleSelectionSet BodyMeshes;
};
