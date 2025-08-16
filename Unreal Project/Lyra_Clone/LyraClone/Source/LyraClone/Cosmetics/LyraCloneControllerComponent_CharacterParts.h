// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "LyraCloneCharacterPartTypes.h"
#include "Components/ControllerComponent.h"
#include "LyraCloneControllerComponent_CharacterParts.generated.h"

class ULyraClonePawnComponent_CharacterParts;

/** ControllerComponent�� �����ϴ� Character Parts */
USTRUCT()
struct FLyraCloneControllerCharacterPartEntry
{
	GENERATED_BODY()

	/** Character Part�� ���� ����(��Ÿ ������ == MetaData) */
	UPROPERTY(EditAnywhere)
	FLyraCloneCharacterPart Part;

	/** Character Part �ڵ� (������) - Controller�� Possess�ϰ� �ִ� Pawn���� ������(�ν��Ͻ�) Character Part �ڵ鰪 */
	FLyraCloneCharacterPartHandle Handle;
};


UCLASS(meta = (BlueprintSpawnableComponent))
class ULyraCloneControllerComponent_CharacterParts : public UControllerComponent
{
	GENERATED_BODY()
public:
	ULyraCloneControllerComponent_CharacterParts(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

	ULyraClonePawnComponent_CharacterParts* GetPawnCustomizer() const;

	UFUNCTION(BlueprintCallable, Category = Cosmetics)
	void AddCharacterPart(const FLyraCloneCharacterPart& NewPart);

	void AddCharacterPartInternal(const FLyraCloneCharacterPart& NewPart);

	void RemoveAllCharacterParts();

	/** UFUNCTION���� �������� ������ AddDynamic�� ���� �ʴ´�! */
	UFUNCTION()
	void OnPossessedPawnChanged(APawn* OldPawn, APawn* NewPawn);

	UPROPERTY(EditAnywhere, Category = Cosmetics)
	TArray<FLyraCloneControllerCharacterPartEntry> CharacterParts;
};