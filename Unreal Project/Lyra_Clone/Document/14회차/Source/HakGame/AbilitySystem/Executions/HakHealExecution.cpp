// Fill out your copyright notice in the Description page of Project Settings.

#include "HakHealExecution.h"
#include "HakGame/AbilitySystem/Attributes/HakCombatSet.h"
#include "HakGame/AbilitySystem/Attributes/HakHealthSet.h"

/**
 * �ش� Struct�� ����Ͽ�, FGameplayEffectAttributeCaptureDefinition �ν��Ͻ�ȭ�Ͽ� �����Ѵ�
 */
struct FHealStatics
{
	/** AttributeSet�� � Attribute�� Capture�� �������� ��� Capture���� ���Ǹ� ��� �ִ� (�ѹ� ���� ����) */
	FGameplayEffectAttributeCaptureDefinition BaseHealDef;

	FHealStatics()
	{
		BaseHealDef = FGameplayEffectAttributeCaptureDefinition(UHakCombatSet::GetBaseHealAttribute(), EGameplayEffectAttributeCaptureSource::Source, true);
	}
};

static FHealStatics& HealStatics()
{
	// ��� FHealStatics�� �����ϴ� ���� �����̴�, �ѹ��� �����ϰ� �����Ѵ�
	static FHealStatics Statics;
	return Statics;
}

UHakHealExecution::UHakHealExecution() : Super()
{
	// Source�� (�Է°�) Attribute�� ĸ�ĸ� ��������
	// - CombatSet::BaseHeal�� ���� Healing ���� �����ϰ� ���� Execute�� ��, �ش� ���� �����ͼ� Health�� Healing�� �����Ѵ�
	RelevantAttributesToCapture.Add(HealStatics().BaseHealDef);
}

void UHakHealExecution::Execute_Implementation(const FGameplayEffectCustomExecutionParameters& ExecutionParams, FGameplayEffectCustomExecutionOutput& OutExecutionOutput) const
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
		OutExecutionOutput.AddOutputModifier(FGameplayModifierEvaluatedData(UHakHealthSet::GetHealingAttribute(), EGameplayModOp::Additive, HealingDone));
	}
}