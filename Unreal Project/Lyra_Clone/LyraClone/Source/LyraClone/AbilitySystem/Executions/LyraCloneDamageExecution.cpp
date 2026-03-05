#include "LyraCloneDamageExecution.h"

#include "LyraClone/AbilitySystem/Attributes/LyraCloneHealthSet.h"
#include "GameplayTagsManager.h"

namespace LyraCloneDamageTags
{
	static FGameplayTag SetByCaller_Damage()
	{
		// GA에서 AssignTagSetByCallerMagnitude로 넣는 태그와 정확히 동일해야 함
		return FGameplayTag::RequestGameplayTag(TEXT("SetByCaller.Damage"));
	}
}

ULyraCloneDamageExecution::ULyraCloneDamageExecution()
	: Super()
{
	// 현재 버전은 SetByCaller 기반이라 캡처 필수 아님.
	// (추후 Armor/AttackPower 같은 걸 쓰고 싶으면 RelevantAttributesToCapture 추가하면 됨)
}

void ULyraCloneDamageExecution::Execute_Implementation(
	const FGameplayEffectCustomExecutionParameters& ExecutionParams,  
	FGameplayEffectCustomExecutionOutput& OutExecutionOutput
) const
{
	const FGameplayEffectSpec& Spec = ExecutionParams.GetOwningSpec();

	// GA에서 확정한 랜덤 데미지를 그대로 사용
	const float Damage =
		Spec.GetSetByCallerMagnitude(LyraCloneDamageTags::SetByCaller_Damage(), false, 0.0f);

#if !(UE_BUILD_SHIPPING || UE_BUILD_TEST)
	UE_LOG(LogTemp, Warning, TEXT("[DamageExec] Spec=%s  SBC(SetByCaller.Damage)=%.2f"),
		*GetNameSafe(Spec.Def),
		Damage);
#endif



	const float DamageDone = FMath::Max(0.0f, Damage);
	if (DamageDone <= 0.0f)
	{
		return;
	}

	// Lyra식 Meta Damage: HealthSet의 Damage(meta)에 +로 넣는다.
	// 실제 Health 감소는 너가 올린 HealthSet::PostGameplayEffectExecute가 수행함.
	OutExecutionOutput.AddOutputModifier(
		FGameplayModifierEvaluatedData(
			ULyraCloneHealthSet::GetDamageAttribute(),
			EGameplayModOp::Additive,
			DamageDone
		)
	);
}