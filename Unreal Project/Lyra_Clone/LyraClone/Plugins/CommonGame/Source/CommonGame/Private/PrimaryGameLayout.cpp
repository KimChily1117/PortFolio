// Fill out your copyright notice in the Description page of Project Settings.
#include "PrimaryGameLayout.h"
#include "Widgets/CommonActivatableWidgetContainer.h"

UPrimaryGameLayout::UPrimaryGameLayout(const FObjectInitializer& obj = FObjectInitializer::Get()) : Super(obj)
{
}

void UPrimaryGameLayout::RegisterLayer(FGameplayTag LayerTag, UCommonActivatableWidgetContainerBase* LayerWidget)
{
	if (!IsDesignTime()) // �׸��� �ð��� �ƴ϶�� 
	{
		LayerWidget->SetTransitionDuration(0.0);
		Layers.Add(LayerTag, LayerWidget);
	}
}
