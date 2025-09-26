// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "HakCharacterPartTypes.h"
#include "HakCosmeticAnimationTypes.h"
#include "Components/PawnComponent.h"
#include "HakPawnComponent_CharacterParts.generated.h"

class UHakPawnComponent_CharacterParts;

/** 인스턴스화 된 Character Part의 단위 */
USTRUCT()
struct FHakAppliedCharacterPartEntry
{
	GENERATED_BODY()

	/** Character Part의 정의(메타 데이터) */
	UPROPERTY()
	FHakCharacterPart Part;

	/** HakCharacterPartList에서 할당 받은 Part 핸들값 (FHakControllerCharacterPartEntry의 Handle 값과 같아야 함 -> 같으면 같은 Part) */
	UPROPERTY()
	int32 PartHandle = INDEX_NONE;

	/** 인스턴스화 된 Character Part용 Actor */
	UPROPERTY()
	TObjectPtr<UChildActorComponent> SpawnedComponent = nullptr;
};

/** HakPawnComponent_CharacterParts에서 실질적 Character Parts를 관리하는 클래스 */
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

	/** 현재 인스턴스화된 Character Part */
	UPROPERTY()
	TArray<FHakAppliedCharacterPartEntry> Entries;

	/** 해당 HakCharacterPartList의 Owner인 PawnComponent */
	UPROPERTY()
	TObjectPtr<UHakPawnComponent_CharacterParts> OwnerComponent;

	/** 앞서 보았던 PartHandle의 값을 할당 및 관리하는 변수 */
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

	/** 인스턴스화 된 Character Parts */
	UPROPERTY()
	FHakCharacterPartList CharacterPartList;

	/** 애니메이션 적용을 위한 메시와 연결고리 */
	UPROPERTY(EditAnywhere, Category = Cosmetics)
	FHakAnimBodyStyleSelectionSet BodyMeshes;
};
