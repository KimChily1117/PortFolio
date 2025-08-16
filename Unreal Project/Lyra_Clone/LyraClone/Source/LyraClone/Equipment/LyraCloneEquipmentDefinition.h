// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "UObject/NoExportTypes.h"
#include "LyraCloneEquipmentDefinition.generated.h"

class ULyraCloneAbilitySet;
class ULyraCloneEquipmentInstance;

USTRUCT()
struct FLyraCloneEquipmentActorToSpawn
{
	GENERATED_BODY()

	/** Spawn�� ��� Actor Ŭ���� (== Actor�� ��ӹ��� Asset���� �����ص� ��) */
	UPROPERTY(EditAnywhere, Category = Equipment)
	TSubclassOf<AActor> ActorToSpawn;

	/** ��� Bone Socket�� ������ �����Ѵ� */
	UPROPERTY(EditAnywhere, Category = Equipment)
	FName AttachSocket;

	/** Socket���� ������� Transformation�� ���Ұ����� ����: (Rotation, Position, Scale) */
	UPROPERTY(EditAnywhere, Category = Equipment)
	FTransform AttachTransform;
};

/**
 * LyraCloneEquipementDefinition�� ���� �����ۿ� ���� ���� Ŭ����(��Ÿ ������)�̴�
 */
UCLASS(Blueprintable)
class LYRACLONE_API ULyraCloneEquipmentDefinition : public UObject
{
	GENERATED_BODY()
public:
	ULyraCloneEquipmentDefinition(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/** �ش� ��Ÿ �����͸� ����ϸ�, � �ν��Ͻ��� Spawn���� �����ϴ� Ŭ���� */
	UPROPERTY(EditDefaultsOnly, Category = Equipment)
	TSubclassOf<ULyraCloneEquipmentInstance> InstanceType;

	/** �ش� ���� �������� ����ϸ�, � Actor�� Spawn�� �Ǵ��� ������ ��� �ִ� */
	UPROPERTY(EditDefaultsOnly, Category = Equipment)
	TArray<FLyraCloneEquipmentActorToSpawn> ActorsToSpawn;

	/** ������ ���� �ο� ������ Ability Set */
	UPROPERTY(EditDefaultsOnly, Category = Equipment)
	TArray<TObjectPtr<ULyraCloneAbilitySet>> AbilitySetsToGrant;
};
