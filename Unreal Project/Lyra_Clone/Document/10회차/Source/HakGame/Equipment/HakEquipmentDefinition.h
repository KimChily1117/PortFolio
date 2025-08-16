// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "UObject/NoExportTypes.h"
#include "HakEquipmentDefinition.generated.h"

class UHakAbilitySet;
class UHakEquipmentInstance;

USTRUCT()
struct FHakEquipmentActorToSpawn
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
 * HakEquipementDefinition�� ���� �����ۿ� ���� ���� Ŭ����(��Ÿ ������)�̴�
 */
UCLASS(Blueprintable)
class HAKGAME_API UHakEquipmentDefinition : public UObject
{
	GENERATED_BODY()
public:
	UHakEquipmentDefinition(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get());

	/** �ش� ��Ÿ �����͸� ����ϸ�, � �ν��Ͻ��� Spawn���� �����ϴ� Ŭ���� */
	UPROPERTY(EditDefaultsOnly, Category = Equipment)
	TSubclassOf<UHakEquipmentInstance> InstanceType;

	/** �ش� ���� �������� ����ϸ�, � Actor�� Spawn�� �Ǵ��� ������ ��� �ִ� */
	UPROPERTY(EditDefaultsOnly, Category = Equipment)
	TArray<FHakEquipmentActorToSpawn> ActorsToSpawn;

	/** ������ ���� �ο� ������ Ability Set */
	UPROPERTY(EditDefaultsOnly, Category = Equipment)
	TArray<TObjectPtr<UHakAbilitySet>> AbilitySetsToGrant;
};
