// Fill out your copyright notice in the Description page of Project Settings.


#include "HakActivatableWidget.h"

UHakActivatableWidget::UHakActivatableWidget(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{}

TOptional<FUIInputConfig> UHakActivatableWidget::GetDesiredInputConfig() const
{
	// �ռ� ���� �ߴ� WidgetInputMode�� ����, InputConfig�� �������ش�
	// - ECommonInputMode�� ���� Input�� �帧�� Game/Menu Ȥ�� �Ѵٷ� ���ϴ� ���� �����ϴ�
	// - ���� ���, �츮�� Menu�� ���� ���, ���̻� Game�� Input�� ������ ���� ���� ��� �ſ� �����ϴ�:
	//   - ����غ���, Menu ����ε� Space�� �����ų� MouseClick���� �Ѿ��� �����ٸ�... �츮�� �ǵ��� ��Ȳ�� �ƴ� ���̴�
	switch (InputConfig)
	{
	case EHakWidgetInputMode::GameAndMenu:
		return FUIInputConfig(ECommonInputMode::All, GameMouseCaptureMode);
	case EHakWidgetInputMode::Game:
		return FUIInputConfig(ECommonInputMode::Game, GameMouseCaptureMode);
	case EHakWidgetInputMode::Menu:
		return FUIInputConfig(ECommonInputMode::Menu, GameMouseCaptureMode);
	case EHakWidgetInputMode::Default:
	default:
		return TOptional<FUIInputConfig>();
	}
}
