// Fill out your copyright notice in the Description page of Project Settings.

#include "LyraCloneHealExecution.h"
#include "LyraClone/AbilitySystem/Attributes/LyraCloneCombatSet.h"
#include "LyraClone/AbilitySystem/Attributes/LyraCloneHealthSet.h"

/**
 * �ش� Struct�� ����Ͽ�, FGameplayEffectAttributeCaptureDefinition �ν��Ͻ�ȭ�Ͽ� �����Ѵ�
 */
struct FHealStatics
{
	/** AttributeSet�� � Attribute�� Capture�� �������� ��� Capture���� ���Ǹ� ��� �ִ� (�ѹ� ���� ����) */
	FGameplayEffectAttributeCaptureDefinition BaseHealDef;

	FHealStatics()
	{
		BaseHealDef = FGameplayEffectAttributeCaptureDefinition(ULyraCloneCombatSet::GetBaseHealAttribute(), EGameplayEffectAttributeCaptureSource::Source, true);
	}
};

static FHealStatics& HealStatics()
{
	// ��� FHealStatics�� �����ϴ� ���� �����̴�, �ѹ��� �����ϰ� �����Ѵ�
	static FHealStatics Statics;
	return Statics;
}

ULyraCloneHealExecution::ULyraCloneHealExecution() : Super()
{
	// Source�� (�Է°�) Attribute�� ĸ�ĸ� ��������
	// - CombatSet::BaseHeal�� ���� Healing ���� �����ϰ� ���� Execute�� ��, �ش� ���� �����ͼ� Health�� Healing�� �����Ѵ�
	RelevantAttributesToCapture.Add(HealStatics().BaseHealDef);
}

void ULyraCloneHealExecution::Execute_Implementation(const FGameplayEffectCustomExecutionParameters& ExecutionParams, FGameplayEffectCustomExecutionOutput& OutExecutionOutput) const
{
	// GameplayEffectSpec�� GameplayEffect�� �ڵ�� �����ϸ� �ȴ�
	const FGameplayEffectSpec& Spec = ExecutionParams.GetOwningSpec();

	float BaseHeal = 0.0f;
	{
		FAggregatorEvaluateParameters EvaluateParameters;

		// �ش� �Լ� ȣ���� ���� HakCombatSet�� BaseHeal ���� �����´� (Ȥ�� ���� Modifier �� �����Ǿ� �־��ٸ�, ���� ��� ����� ���´�)
		ExecutionParams.AttemptCalculateCapturedAttributeMagnitude(HealStatics().BaseHealDef, EvaluateParameters, BaseHeal);
	}

	// RelevantAttributesCapture�� ���� ���� ���� BaseHeal�� 0.0���ϰ� ���� �ʵ��� �Ѵ� (Healing�̴ϱ�!)
	const float HealingDone = FMath::Max(0.0f, BaseHeal);
	if (HealingDone > 0.0f)
	{
		// GameplayEffectCalculation ����, Modifier�μ�, �߰��Ѵ�:
		// - �ش� Modifier�� CombatSet���� ������ BaseHeal�� Ȱ���Ͽ�, HealthSet�� Healing�� �߰����ش�
		OutExecutionOutput.AddOutputModifier(FGameplayModifierEvaluatedData(ULyraCloneHealthSet::GetHealingAttribute(), EGameplayModOp::Additive, HealingDone));
	}
}