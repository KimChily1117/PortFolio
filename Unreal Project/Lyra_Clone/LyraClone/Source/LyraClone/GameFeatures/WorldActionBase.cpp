// Fill out your copyright notice in the Description page of Project Settings.


#include "WorldActionBase.h"

void UWorldActionBase::OnGameFeatureActivating(FGameFeatureActivatingContext& Context)
{
	// 월드를 순회하면서,
	for (const FWorldContext& WorldContext : GEngine->GetWorldContexts())
	{
		// 앞서, ExperienceManagerComponent에서 GameFeatureAction을 활성화하면서, Context에 World를 넣어줌
		// - 이를 통해 적용할 대상인지 판단
		if (Context.ShouldApplyToWorldContext(WorldContext))
		{
			// WorldActionBase의 Interface인 AddToWorld 호출
			AddToWorld(WorldContext, Context);
		}
	}
}
