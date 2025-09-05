// Fill out your copyright notice in the Description page of Project Settings.
#include "PrimaryGameLayout.h"
#include "Widgets/CommonActivatableWidgetContainer.h"

UPrimaryGameLayout::UPrimaryGameLayout(const FObjectInitializer& obj = FObjectInitializer::Get()) : Super(obj)
{
}

UCommonActivatableWidgetContainerBase* UPrimaryGameLayout::GetLayerWidget(FGameplayTag LayerName)
{
	return Layers.FindRef(LayerName);
}

void UPrimaryGameLayout::RegisterLayer(FGameplayTag LayerTag, UCommonActivatableWidgetContainerBase* LayerWidget)
{
	if (!IsDesignTime()) // 그리는 시간이 아니라면 
	{
		LayerWidget->SetTransitionDuration(0.0);
		Layers.Add(LayerTag, LayerWidget);
	}
}
