// Fill out your copyright notice in the Description page of Project Settings.


#include "WorldActionBase.h"

void UWorldActionBase::OnGameFeatureActivating(FGameFeatureActivatingContext& Context)
{
	// ���带 ��ȸ�ϸ鼭,
	for (const FWorldContext& WorldContext : GEngine->GetWorldContexts())
	{
		// �ռ�, ExperienceManagerComponent���� GameFeatureAction�� Ȱ��ȭ�ϸ鼭, Context�� World�� �־���
		// - �̸� ���� ������ ������� �Ǵ�
		if (Context.ShouldApplyToWorldContext(WorldContext))
		{
			// WorldActionBase�� Interface�� AddToWorld ȣ��
			AddToWorld(WorldContext, Context);
		}
	}
}
