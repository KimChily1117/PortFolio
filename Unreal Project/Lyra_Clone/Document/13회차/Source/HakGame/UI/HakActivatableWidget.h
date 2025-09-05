// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "CommonActivatableWidget.h"
#include "HakActivatableWidget.generated.h"

/**
 * Input ó�� ��� ����
 */
UENUM(BlueprintType)
enum class EHakWidgetInputMode : uint8
{
	Default,
	GameAndMenu,
	Game,
	Menu,
};

/**
 * CommonActivatableWidget�� �ּ��� ����, �����ϰ� ����Ǿ� �ִ�.
 * ���ڰ� ������ CommonActivatableWidget�� �ΰ��� Ư���� ������ �ִ�:
 * 1. Widget Layout�� Widget Instance�� �����ϱ� ���� �뵵�� CommonActivatableWidget�� Layout ����:
 *    - ��Ÿ�� Activate/Deactivate
 *    - WidgetTree���� ���Ű� �ƴ� ����/�״�(== Activate/Deactivate)
 * 2. Input(Mouse or Keyboard etc...) ó�� ��� ����
 */
UCLASS()
class HAKGAME_API UHakActivatableWidget : public UCommonActivatableWidget
{
	GENERATED_BODY()
public:
	UHakActivatableWidget(const FObjectInitializer& ObjectInitializer);

	/**
	 * UCommonActivatableWidget's interfaces
	 */
	virtual TOptional<FUIInputConfig> GetDesiredInputConfig() const override;

	/** Input ó�� ��� */
	UPROPERTY(EditDefaultsOnly, Category = Input)
	EHakWidgetInputMode InputConfig = EHakWidgetInputMode::Default;

	/** Mouse ó�� ��� */
	UPROPERTY(EditDefaultsOnly, Category = Input)
	EMouseCaptureMode GameMouseCaptureMode = EMouseCaptureMode::CapturePermanently;
};
