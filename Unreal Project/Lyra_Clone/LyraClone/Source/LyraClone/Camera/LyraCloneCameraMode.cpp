// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraCloneCameraMode.h"
#include "LyraCloneCameraComponent.h"
#include "LyraClonePlayerCameraManager.h"

#include UE_INLINE_GENERATED_CPP_BY_NAME(LyraCloneCameraMode)

FLyraCloneCameraModeView::FLyraCloneCameraModeView()
	:	Location(ForceInit)
	,	Rotation(ForceInit)
	,	ControlRotation(ForceInit)
	,	FieldOfView(HAK_CAMERA_DEFAULT_FOV)
{
}

void FLyraCloneCameraModeView::Blend(const FLyraCloneCameraModeView& Other, float OtherWeight)
{
	if (OtherWeight <= 0.0f)
	{
		return;
	}
	else if (OtherWeight >= 1.0f)
	{
		// Weight�� 1.0�̸� Other�� ����� �ȴ�
		*this = Other;
		return;
	}

	// Location + OtherWeight * (Other.Location - Location);
	Location = FMath::Lerp(Location, Other.Location, OtherWeight);

	// Location�� ���� ��� Lerp (ControlRotation�� FieldOfView�� ����)
	const FRotator DeltaRotation = (Other.Rotation - Rotation).GetNormalized();
	Rotation = Rotation + (OtherWeight * DeltaRotation);

	const FRotator DeltaControlRotation = (Other.ControlRotation - ControlRotation).GetNormalized();
	ControlRotation = ControlRotation + (OtherWeight * DeltaControlRotation);

	FieldOfView = FMath::Lerp(FieldOfView, Other.FieldOfView, OtherWeight);
}




ULyraCloneCameraMode::ULyraCloneCameraMode(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
	FieldOfView = HAK_CAMERA_DEFAULT_FOV;
	ViewPitchMin = HAK_CAMERA_DEFAULT_PITCH_MIN;
	ViewPitchMax = HAK_CAMERA_DEFAULT_PITCH_MAX;

	BlendTime = 0.0f;
	BlendAlpha = 1.0f;
	BlendWeight = 1.0f;

	BlendFunction = EHakCameraModeBlendFunction::EaseOut;
	BlendExponent = 4.0f;
}

void ULyraCloneCameraMode::UpdateCameraMode(float DeltaTime)
{
	// Actor�� Ȱ���Ͽ�, Pivot[Location|Rotation]�� ����Ͽ�, View�� ������Ʈ
	UpdateView(DeltaTime);

	// BlendWeight�� DeltaTime�� Ȱ���Ͽ�, BlendAlpha ��� ��, BlendFunction�� �°� ��-�����Ͽ� ���� ���
	UpdateBlending(DeltaTime);
}

void ULyraCloneCameraMode::UpdateView(float DeltaTime)
{
	// CameraMode�� ������ �ִ� CameraComponent�� Owner�� Character(Pawn)�� Ȱ���Ͽ�, PivotLocation/Rotation�� ��ȯ��
	FVector PivotLocation = GetPivotLocation();
	FRotator PivotRotation = GetPivotRotation();

	// Pitch ���� ���� Min/Max�� Clamp��Ŵ
	PivotRotation.Pitch = FMath::ClampAngle(PivotRotation.Pitch, ViewPitchMin, ViewPitchMax);

	// FHakCameraModeView�� PivotLocation/Rotation ����
	View.Location = PivotLocation;
	View.Rotation = PivotRotation;

	// PivotRotation�� �Ȱ��� ControlRotation���� Ȱ��
	View.ControlRotation = View.Rotation;
	View.FieldOfView = FieldOfView;

	// �����ϸ�, Character�� Location�� ControlRotation�� Ȱ���Ͽ�, View�� ������Ʈ��
}

void ULyraCloneCameraMode::UpdateBlending(float DeltaTime)
{
	// BlendAlpha�� DeltaTime�� ���� ���
	if (BlendTime > 0.f)
	{
		// BlendTime�� Blending ���� �� �ð�(��)
		// - BlendAlpha�� 0 -> 1�� ��ȭ�ϴ� ����:
		// - DeltaTime�� Ȱ���Ͽ�, BlendTime�� 1�� �� ���, ���� ������ ����
		BlendAlpha += (DeltaTime / BlendTime);
	}
	else
	{
		BlendAlpha = 1.0f;
	}

	// BlendWeight�� [0,1]�� BlendFunction�� �°� �����
	const float Exponent = (BlendExponent > 0.0f) ? BlendExponent : 1.0f;
	switch (BlendFunction)
	{
	case EHakCameraModeBlendFunction::Linear:
		BlendWeight = BlendAlpha;
		break;
	case EHakCameraModeBlendFunction::EaseIn:
		BlendWeight = FMath::InterpEaseIn(0.0f, 1.0f, BlendAlpha, Exponent);
		break;
	case EHakCameraModeBlendFunction::EaseOut:
		BlendWeight = FMath::InterpEaseOut(0.0f, 1.0f, BlendAlpha, Exponent);
		break;
	case EHakCameraModeBlendFunction::EaseInOut:
		BlendWeight = FMath::InterpEaseInOut(0.0f, 1.0f, BlendAlpha, Exponent);
		break;
	default:
		checkf(false, TEXT("UpdateBlending: Invalid BlendFunction [%d]\n"), (uint8)BlendFunction);
		break;
	}
}

ULyraCloneCameraComponent* ULyraCloneCameraMode::GetLyraCloneCameraComponent() const
{
	// �츮�� �ռ� UHakCameraMode�� �����ϴ� ���� UHakCameraModeStack�̾���:
	// - �ش� �ڵ带 ����, GetOuter()�� HakCameraMode�� HakCameraComponent�� �����Ͽ���
	// - UHakCameraModeStack::GetCameraModeInstance() Ȯ��
	return CastChecked<ULyraCloneCameraComponent>(GetOuter());
}

AActor* ULyraCloneCameraMode::GetTargetActor() const
{
	const ULyraCloneCameraComponent* HakCameraComponent = GetLyraCloneCameraComponent();
	return HakCameraComponent->GetTargetActor();
}

FVector ULyraCloneCameraMode::GetPivotLocation() const
{
	const AActor* TargetActor = GetTargetActor();
	check(TargetActor);

	if (const APawn* TargetPawn = Cast<APawn>(TargetActor))
	{
		// BaseEyeHeight�� ����Ͽ�, ViewLocation�� ��ȯ��
		return TargetPawn->GetPawnViewLocation();
	}

	return TargetActor->GetActorLocation();
}

FRotator ULyraCloneCameraMode::GetPivotRotation() const
{
	const AActor* TargetActor = GetTargetActor();
	check(TargetActor);

	if (const APawn* TargetPawn = Cast<APawn>(TargetActor))
	{
		// GetViewRoation() Ȯ��, ���� Pawn�� ControlRotation�� ��ȯ
		return TargetPawn->GetViewRotation();
	}

	return TargetActor->GetActorRotation();
}


ULyraCloneCameraModeStack::ULyraCloneCameraModeStack(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
}

void ULyraCloneCameraModeStack::PushCameraMode(TSubclassOf<ULyraCloneCameraMode>& CameraModeClass)
{
	if (!CameraModeClass)
	{
		return;
	}


	ULyraCloneCameraMode* CameraMode = GetCameraModeInstance(CameraModeClass);
	check(CameraMode);

	int32 StackSize = CameraModeStack.Num();
	if ((StackSize > 0) && (CameraModeStack[0] == CameraMode))
	{
		// CameraModeStack[0] ���� �ֱٿ� �̹� CameraMode�� Stacking�Ǿ����Ƿ� �׳� ����!
		return;
	}

	// ExistingStackIndex�� CameraModeStack���� CameraMode�� �´� Index�� ã��
	// ExistingStackContribution�� ������ �Ʒ��� ���� BlendWeight ���� ã�� ���� �ʱⰪ���� 1.0���� ����
	int32 ExistingStackIndex = INDEX_NONE;
	float ExistingStackContribution = 1.0f;

	/**
	 * BlendWeight    |    ExistingStackCOntribution    |    ExistingStackCOntribution (accumulated)
	 * 0.1f           |    (1.0f) * 0.1f = 0.1f         |    (1.0f - 0.1f) = 0.9f
	 * 0.3f           |    (0.9f) * 0.3f = 0.27f        |    (1.0f - 0.3f) * 0.9f = 0.63f
	 * 0.6f           |    (0.63f) * 0.6f = 0.378f      |    (1.0f - 0.6f) * 0.63f = 0.252f
	 * 1.0f           |    (0.252f) * 1.0f = 0.252f     |
	 *                |    0.1f + 0.27f + 0.378f + 0.252f = 1.0f!
	 */
	for (int32 StackIndex = 0; StackIndex < StackSize; ++StackIndex)
	{
		if (CameraModeStack[StackIndex] == CameraMode)
		{
			ExistingStackIndex = StackIndex;
			// BlendWeight�� CameraMode�� �߰�������:
			// - ���⼭ ExistingStackContribution �츮�� ã�� CameraMode�� ã�����ϱ�, ������ BlendWeight�� �Բ� BlendWeight�� ���Ͽ�, ������ �������´�
			ExistingStackContribution *= CameraMode->BlendWeight;
			break;
		}
		else
		{
			// �翬�� �츮�� ���ϴ� CamearMode�� �ƴϴϱ�, InvBlendWeight = (1.0 - BlendWeight)�� �������, ���� �����ǰ�¡?
			ExistingStackContribution *= (1.0f - CameraModeStack[StackIndex]->BlendWeight);
		}
	}
	// �ش� ������ ���� ������ �ռ� ������� ǥ�� Ȯ���غ��� �����غ���.

	// �츮�� CameraMode�� Top���� �ݿ��ϱ� ����, �翬�� �߰��� �־��ٸ�, �����Ͽ� �ٽ� Push ����� �Ѵ�
	if (ExistingStackIndex != INDEX_NONE)
	{
		CameraModeStack.RemoveAt(ExistingStackIndex);
		StackSize--;
	}
	else
	{
		// �������ϱ� �翬�� Contribution�� 0���� ��������߰�¡?
		ExistingStackContribution = 0.0f;
	}

	// BlendTime�� 0���� ũ�ٴ� ���� Blend�� �� �ð����� �������� �ǹ� ����, ExistingStackContribution�� ����
	// - ���� Blend���� �ʴ´ٸ�, BlendWeight�� 1.0�� �־� ���� �ִ� CameraMode�� ������ ���̴�
	const bool bShouldBlend = ((CameraMode->BlendTime > 0.f) && (StackSize > 0));
	const float BlendWeight = (bShouldBlend ? ExistingStackContribution : 1.0f);
	CameraMode->BlendWeight = BlendWeight;

	// ��.. Array�� Stackó�� ����ϴ°��� ������, Index 0�� �ִ°� ��ȿ�����ε�..
	// - Push, Pop �޼���� ���̱׳� last�� �ִ°�... ��� ����
	CameraModeStack.Insert(CameraMode, 0);

	// �ռ� �����ߵ��� �������� �׻� 1.0�� �Ǿ�� ��!
	CameraModeStack.Last()->BlendWeight = 1.0f;
}

void ULyraCloneCameraModeStack::EvaluateStack(float DeltaTime, FLyraCloneCameraModeView& OutCameraModeView)
{
	// Actor�� Ȱ���Ͽ�, Pivot[Location|Rotation]�� ����Ͽ�, View�� ������Ʈ
	UpdateStack(DeltaTime);

	// BlendWeight�� DeltaTime�� Ȱ���Ͽ�, BlendAlpha ��� ��, BlendFunction�� �°� ��-�����Ͽ� ���� ���
	BlendStack(OutCameraModeView);
}

void ULyraCloneCameraModeStack::UpdateStack(float DeltaTime)
{
	const int32 StackSize = CameraModeStack.Num();
	if (StackSize <= 0)
	{
		return;
	}

	int32 RemoveCount = 0;
	int32 RemoveIndex = INDEX_NONE;
	// �ʱ�ȭ

	for (int32 StackIndex = 0; StackIndex < StackSize; ++StackIndex)
	{
		ULyraCloneCameraMode* CameraMode = CameraModeStack[StackIndex];
		check(CameraMode);



		CameraMode->UpdateCameraMode(DeltaTime);

		if (CameraMode->BlendWeight >= 1.0f)
		{
			RemoveIndex = (StackIndex + 1);
			RemoveCount = (StackSize - RemoveIndex);
			break;
		}
	}


	if (RemoveCount > 0)
	{
		// �����غ��� �̰� ������ Pop�ϰ� Push�� ���Ѱ�������?
		CameraModeStack.RemoveAt(RemoveIndex, RemoveCount);
	}


}

void ULyraCloneCameraModeStack::BlendStack(FLyraCloneCameraModeView& OutCameraModeView) const
{
	const int32 StackSize = CameraModeStack.Num();
	if (StackSize <= 0)
	{
		return;
	}

	// CameraModeStack�� Bottom -> Top ������ Blend�� �����Ѵ�
	const ULyraCloneCameraMode* CameraMode = CameraModeStack[StackSize - 1];
	check(CameraMode);

	// ����� ���� �Ʒ� (Bottom)�� BlendWeight�� 1.0�̴�!
	OutCameraModeView = CameraMode->View;

	// �̹� Index = [StackSize - 1] �̹� OutCameraModeView�� ���������Ƿ�, StackSize - 2���� ��ȸ�ϸ� �ȴ�
	for (int32 StackIndex = (StackSize - 2); StackIndex >= 0; --StackIndex)
	{
		CameraMode = CameraModeStack[StackIndex];
		check(CameraMode);

		// HakCameraModeView Blend �Լ��� ��������:
		OutCameraModeView.Blend(CameraMode->View, CameraMode->BlendWeight);
	}
}

ULyraCloneCameraMode* ULyraCloneCameraModeStack::GetCameraModeInstance(TSubclassOf<ULyraCloneCameraMode>& CameraModeClass)
{
	check(CameraModeClass);

	// CameraModeInstances���� ���� �����Ǿ��ִ��� Ȯ�� ��, ��ȯ�Ѵ�:
	for (ULyraCloneCameraMode* CameraMode : CameraModeInstances)
	{
		// CameraMode�� UClass�� ���Ѵ�:
		// - ��, CameraMode�� Ŭ���� Ÿ�Կ� �ϳ��� ����Եȴ�
		if ((CameraMode != nullptr) && (CameraMode->GetClass() == CameraModeClass))
		{
			return CameraMode;
		}
	}

	// CameraModeClass�� �˸´� CameraMode�� �ν��Ͻ��� ���ٸ� �����Ѵ�:
	ULyraCloneCameraMode* NewCameraMode = NewObject<ULyraCloneCameraMode>(GetOuter(), CameraModeClass, NAME_None, RF_NoFlags);
	check(NewCameraMode);

	// ���⼭ �츮�� CameraModeInstances�� CameraModeClass�� �´� �ν��Ͻ��� ������(�����ϴ�) �����̳ʶ�� ���� �� �� �ִ�
	CameraModeInstances.Add(NewCameraMode);

	return NewCameraMode;
}

