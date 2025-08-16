// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "HakCharacterPartTypes.h"
#include "Components/ControllerComponent.h"
#include "HakControllerComponent_CharacterParts.generated.h"

class UHakPawnComponent_CharacterParts;

/** ControllerComponent가 소유하는 Character Parts */
USTRUCT()
struct FHakControllerCharacterPartEntry
{
	GENERATED_BODY()

	/** Character Part에 대한 정의(메타 데이터 == MetaData) */
	UPROPERTY(EditAnywhere)
	FHakCharacterPart Part;

	/** Character Part 핸들 (고유값) - Controller가 Possess하고 있는 Pawn에서 생성한(인스턴스) Character Part 핸들값 */
	FHakCharacterPartHandle Handle;
};


UCLASS(meta = (BlueprintSpawnableComponent))
class UHakControllerComponent_CharacterParts : public UControllerComponent
{
	GENERATED_BODY()
public:
	UHakControllerComponent_CharacterParts(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

	UHakPawnComponent_CharacterParts* GetPawnCustomizer() const;

	UFUNCTION(BlueprintCallable, Category = Cosmetics)
	void AddCharacterPart(const FHakCharacterPart& NewPart);

	void AddCharacterPartInternal(const FHakCharacterPart& NewPart);

	void RemoveAllCharacterParts();

	/** UFUNCTION으로 정의하지 않으면 AddDynamic이 되지 않는다! */
	UFUNCTION()
	void OnPossessedPawnChanged(APawn* OldPawn, APawn* NewPawn);

	UPROPERTY(EditAnywhere, Category = Cosmetics)
	TArray<FHakControllerCharacterPartEntry> CharacterParts;
};